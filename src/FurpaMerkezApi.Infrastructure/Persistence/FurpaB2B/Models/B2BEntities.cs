namespace FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B.Models;

public sealed class B2BBulletinEntity
{
    public int Id { get; set; }

    public string? BultenDefination { get; set; }

    public string? BultenLink { get; set; }

    public DateTime BultenCreateDate { get; set; }
}

public sealed class B2BUserEntity
{
    public Guid UserId { get; set; }

    public string UserFullName { get; set; } = string.Empty;

    public string UserMail { get; set; } = string.Empty;

    public byte[] UserPasswordSalt { get; set; } = [];

    public byte[] UserPasswordHash { get; set; } = [];

    public bool Status { get; set; }

    public DateTime CreateDate { get; set; }

    public string? Menus { get; set; }

    public DateTime UserEndDate { get; set; }
}

public sealed class B2BUserAccountEntity
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public Guid AccountId { get; set; }

    public string Category { get; set; } = string.Empty;
}
