namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

public sealed record AuthorizationFileDto
{
    public int Id { get; init; }

    public DateTime UpdateDate { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool Z { get; init; }

    public bool R { get; init; }

    public bool X { get; init; }
}
