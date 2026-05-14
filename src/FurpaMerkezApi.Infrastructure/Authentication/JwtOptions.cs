namespace FurpaMerkezApi.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public int ExpiryMinutes { get; init; } = 120;
}
