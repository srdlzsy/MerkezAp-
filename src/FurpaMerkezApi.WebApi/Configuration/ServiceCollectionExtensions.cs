using System.Text;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace FurpaMerkezApi.WebApi.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCleanArchitecture(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration was not found.");

        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
        {
            throw new InvalidOperationException("JWT secret key was not found.");
        }

        services.AddInfrastructure(configuration);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(CreateSwaggerSchemaId);
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FurpaMerkezApi",
                Version = "v1",
                Description = "Authentication and permission management API for FurpaMerkez."
            });

            var bearerScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "JWT Bearer token. Example: Bearer {token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, bearerScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [bearerScheme] = []
            });
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var permissionCode in PermissionCatalog.Codes)
            {
                options.AddPolicy(permissionCode, policy => policy.RequireClaim("permission", permissionCode));
            }
        });

        return services;
    }

    private static string CreateSwaggerSchemaId(Type type)
    {
        if (!type.IsGenericType)
        {
            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        var typeName = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var backtickIndex = typeName.IndexOf('`', StringComparison.Ordinal);
        if (backtickIndex >= 0)
        {
            typeName = typeName[..backtickIndex];
        }

        var argumentNames = string.Join(".", type.GetGenericArguments().Select(CreateSwaggerSchemaId));
        return $"{typeName.Replace('+', '.')}.Of.{argumentNames}";
    }
}
