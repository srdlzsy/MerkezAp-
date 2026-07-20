namespace FurpaMerkezApi.WebApi.Configuration;

public sealed class ApiHostingOptions
{
    public bool EnableSwagger { get; init; }

    public bool ExposeDiagnosticsOnRoot { get; init; }

    public bool EnforceHttps { get; init; } = true;

    public bool UseHsts { get; init; } = true;
}

public sealed class ApiAuthOptions
{
    public bool AllowSelfRegistration { get; init; }
}

public sealed class ReverseProxyOptions
{
    public bool Enabled { get; init; }

    public bool TrustAllNetworks { get; init; }

    public string[] KnownProxies { get; init; } = [];
}

public sealed class ApiCorsOptions
{
    public string[] AllowedOrigins { get; init; } = [];

    public bool AllowCredentials { get; init; } = true;
}

public sealed class DataProtectionOptions
{
    public string KeysPath { get; init; } = string.Empty;

    public string ApplicationName { get; init; } = "FurpaMerkezApi";
}

public sealed class StartupTasksOptions
{
    public bool ApplyAuthMigrations { get; init; }

    public bool SynchronizePermissionCatalog { get; init; }

    public bool SynchronizeWarehouseUsers { get; init; }
}
