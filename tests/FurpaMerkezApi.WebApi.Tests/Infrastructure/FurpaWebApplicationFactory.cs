using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FurpaMerkezApi.WebApi.Tests.Infrastructure;

public sealed class FurpaWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowSelfRegistration"] = "false",
                ["Hosting:EnableSwagger"] = "false",
                ["Hosting:ExposeDiagnosticsOnRoot"] = "false",
                ["Hosting:EnforceHttps"] = "false",
                ["Hosting:UseHsts"] = "false",
                ["StartupTasks:ApplyAuthMigrations"] = "false",
                ["StartupTasks:SynchronizePermissionCatalog"] = "false",
                ["StartupTasks:SynchronizeWarehouseUsers"] = "false",
                ["AxataSynchronization:Enabled"] = "false",
                ["AxataSynchronization:WorkerEnabled"] = "false",
                ["AxataSynchronization:SchedulerEnabled"] = "false",
                ["ConnectionStrings:AuthConnection"] = FakeSqlServerConnection,
                ["ConnectionStrings:FurpaConnection"] = FakeSqlServerConnection,
                ["ConnectionStrings:MikroConnection"] = FakeSqlServerConnection,
                ["ConnectionStrings:MikroWriteConnection"] = FakeSqlServerConnection,
                ["Jwt:Issuer"] = "FurpaMerkezApi.Tests",
                ["Jwt:Audience"] = "FurpaMerkezApi.Tests",
                ["Jwt:SecretKey"] = "0123456789012345678901234567890123456789",
                ["Jwt:ExpiryMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IListKunyeLabelTagsUseCase>();

            services.AddSingleton<IAuthService, FakeAuthService>();
            services.AddSingleton<IListKunyeLabelTagsUseCase, FakeKunyeLabelTagsUseCase>();
            services.AddControllers()
                .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(FallbackTestController).Assembly));
        });
    }

    private const string FakeSqlServerConnection =
        "Server=localhost;Database=FurpaMerkezApiTests;User Id=fake;Password=fake;TrustServerCertificate=True";
}

internal sealed class FakeKunyeLabelTagsUseCase : IListKunyeLabelTagsUseCase
{
    public Task<IReadOnlyCollection<KunyeLabelTagDto>> ExecuteAsync(
        KunyeLabelTagListRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<KunyeLabelTagDto>>(
        [
            new KunyeLabelTagDto(
                request.WarehouseNo,
                "TEST BRANCH",
                "ISTANBUL",
                "STK-001",
                "Test Stock",
                10.5,
                "KADIKOY",
                "Test Product",
                "Fruit",
                "Apple",
                1,
                "TAG-001",
                "Buyer",
                new DateTime(2026, 1, 1),
                7.5,
                new DateTime(2026, 1, 2),
                "Producer",
                "KG")
        ]);
}

internal sealed class FakeAuthService : IAuthService
{
    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new AuthResponse("fake-token", DateTime.UtcNow.AddHours(1), CreateUser()));

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new AuthResponse("fake-token", DateTime.UtcNow.AddHours(1), CreateUser()));

    public Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(CreateUser());

    private static UserDto CreateUser() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "test",
            "test@example.local",
            "Test",
            "User",
            "101",
            "TEST BRANCH",
            true,
            [],
            [],
            [],
            DateTime.UtcNow,
            null);
}

[ApiController]
[Route("api/test/fallback")]
public sealed class FallbackTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
