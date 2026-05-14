using System.Net;
using System.Text;
using FurpaMerkezApi.WebApi.Configuration;
using FurpaMerkezApi.WebApi.HealthChecks;
using FurpaMerkezApi.WebApi.Logging;
using FurpaMerkezApi.WebApi.Middleware;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ApiDataProtectionOptions = FurpaMerkezApi.WebApi.Configuration.DataProtectionOptions;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var hostingOptions = builder.Configuration.GetSection("Hosting").Get<ApiHostingOptions>() ?? new ApiHostingOptions();
var reverseProxyOptions = builder.Configuration.GetSection("ReverseProxy").Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
var corsOptions = builder.Configuration.GetSection("Cors").Get<ApiCorsOptions>() ?? new ApiCorsOptions();
var dataProtectionOptions = builder.Configuration.GetSection("DataProtection").Get<ApiDataProtectionOptions>() ?? new ApiDataProtectionOptions();

ValidateProductionConfiguration(builder.Configuration, builder.Environment, hostingOptions, reverseProxyOptions);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.Configure<FileLoggingOptions>(builder.Configuration.GetSection("Logging:File"));
builder.Logging.Services.AddSingleton<ILoggerProvider, DailyFileLoggerProvider>();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// -------------------- SERVICES --------------------

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<CoreDependenciesHealthCheck>(
        "core_dependencies",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    options.RequireHeaderSymmetry = false;
    options.ForwardLimit = null;

    if (reverseProxyOptions.TrustAllNetworks)
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        return;
    }

    foreach (var proxyAddress in reverseProxyOptions.KnownProxies
                 .Where(value => !string.IsNullOrWhiteSpace(value))
                 .Select(value => value.Trim())
                 .Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (IPAddress.TryParse(proxyAddress, out var parsedAddress))
        {
            options.KnownProxies.Add(parsedAddress);
        }
    }
});

const string CorsPolicyName = "ConfiguredOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        var allowedOrigins = corsOptions.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();

        if (corsOptions.AllowCredentials)
        {
            policy.AllowCredentials();
        }
    });
});

// DataProtection
var dataProtectionKeysPath = string.IsNullOrWhiteSpace(dataProtectionOptions.KeysPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "AppDataKeys")
    : Path.IsPathRooted(dataProtectionOptions.KeysPath)
        ? dataProtectionOptions.KeysPath
        : Path.Combine(builder.Environment.ContentRootPath, dataProtectionOptions.KeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName(dataProtectionOptions.ApplicationName);

// Clean Architecture DI
builder.Services.AddCleanArchitecture(builder.Configuration);

var app = builder.Build();

// -------------------- PIPELINE --------------------

// DB init
await app.InitializeDatabaseAsync();

if (reverseProxyOptions.Enabled)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment() && hostingOptions.UseHsts)
{
    app.UseHsts();
}

if (hostingOptions.EnforceHttps)
{
    app.UseHttpsRedirection();
}

// Swagger
if (hostingOptions.EnableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FurpaMerkezApi v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// CORS (BURASI KRITIK - HER SEYDEN ONCE OLMALI)
app.UseCors(CorsPolicyName);

// Exception middleware (CORS'TAN SONRA OLMASI DAHA GUVENLI)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Default endpoint
app.MapGet("/", () =>
{
    if (!hostingOptions.ExposeDiagnosticsOnRoot)
    {
        return Results.Ok(new
        {
            service = "FurpaMerkezApi",
            status = "Running"
        });
    }

    return Results.Ok(new
    {
        service = "FurpaMerkezApi",
        architecture = "Clean Architecture",
        authDatabase = "FurpaMerkezDb",
        businessDatabase = "MikroDB_V16_FURPA_2024",
        swagger = hostingOptions.EnableSwagger ? "/swagger" : string.Empty,
        status = "Running"
    });
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthCheckResponseAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponseAsync
});

// Controllers
app.MapControllers();

app.Run();

static void ValidateProductionConfiguration(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ApiHostingOptions hostingOptions,
    ReverseProxyOptions reverseProxyOptions)
{
    if (!environment.IsProduction())
    {
        return;
    }

    ValidateRequiredSetting(configuration, "ConnectionStrings:AuthConnection");
    ValidateRequiredSetting(configuration, "ConnectionStrings:FurpaConnection");

    var (mikroReadConnectionKey, mikroWriteConnectionKey) = ResolveMikroConnectionSettingKeys(configuration);
    ValidateRequiredSetting(configuration, $"ConnectionStrings:{mikroReadConnectionKey}");
    ValidateRequiredSetting(configuration, $"ConnectionStrings:{mikroWriteConnectionKey}");

    var jwtSecret = configuration["Jwt:SecretKey"];

    if (string.IsNullOrWhiteSpace(jwtSecret) ||
        jwtSecret.Contains("Replace-Me", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Production JWT secret is missing or still using the placeholder value. Override Jwt__SecretKey before deployment.");
    }

    if (!hostingOptions.EnforceHttps && !reverseProxyOptions.Enabled)
    {
        throw new InvalidOperationException(
            "Production hosting must enforce HTTPS directly or enable reverse proxy forwarded headers support.");
    }
}

static void ValidateRequiredSetting(IConfiguration configuration, string key)
{
    if (!string.IsNullOrWhiteSpace(configuration[key]))
    {
        return;
    }

    throw new InvalidOperationException($"Required production setting '{key}' is missing.");
}

static (string ReadConnectionKey, string WriteConnectionKey) ResolveMikroConnectionSettingKeys(IConfiguration configuration)
{
    var profile = configuration["MikroDatabase:Profile"];

    if (string.IsNullOrWhiteSpace(profile) || profile.Equals("Split", StringComparison.OrdinalIgnoreCase))
    {
        var writeConnectionKey = string.IsNullOrWhiteSpace(configuration["ConnectionStrings:MikroWriteConnection"])
            ? "testMikroConnection"
            : "MikroWriteConnection";

        return ("MikroConnection", writeConnectionKey);
    }

    if (profile.Equals("Test", StringComparison.OrdinalIgnoreCase))
    {
        return ("testMikroConnection", "testMikroConnection");
    }

    if (profile.Equals("Live", StringComparison.OrdinalIgnoreCase))
    {
        return ("MikroConnection", "MikroConnection");
    }

    throw new InvalidOperationException(
        "Configuration value 'MikroDatabase:Profile' must be one of: Split, Test, Live.");
}

static Task WriteHealthCheckResponseAsync(HttpContext httpContext, HealthReport report)
{
    httpContext.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        durationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                durationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
            })
    };

    return httpContext.Response.WriteAsJsonAsync(response);
}
