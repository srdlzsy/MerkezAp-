namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppRefreshToken
{
    private AppRefreshToken()
    {
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public AppUser User { get; private set; } = null!;

    public AppRefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Refresh token id can not be empty.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(userId));
        }

        Id = id;
        UserId = userId;
        TokenHash = NormalizeTokenHash(tokenHash);
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
    }

    public bool IsActive(DateTime nowUtc) =>
        RevokedAtUtc is null && ExpiresAtUtc > DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

    public void Revoke(DateTime revokedAtUtc, string? replacedByTokenHash = null)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = DateTime.SpecifyKind(revokedAtUtc, DateTimeKind.Utc);
        ReplacedByTokenHash = string.IsNullOrWhiteSpace(replacedByTokenHash)
            ? null
            : NormalizeTokenHash(replacedByTokenHash);
    }

    private static string NormalizeTokenHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Token hash is required.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > 128)
        {
            throw new ArgumentException("Token hash can not exceed 128 characters.", nameof(value));
        }

        return normalized;
    }
}
