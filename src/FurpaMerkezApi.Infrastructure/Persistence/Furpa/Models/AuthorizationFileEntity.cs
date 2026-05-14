namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class AuthorizationFileEntity
{
    public int Id { get; set; }

    public DateTime UpdateDate { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Z { get; set; }

    public bool R { get; set; }

    public bool X { get; set; }
}
