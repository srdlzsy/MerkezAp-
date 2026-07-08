namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiWriteAuditOptions
{
    public const string SectionName = "MikroApiWriteAudit";

    public bool Enabled { get; init; } = true;

    public int MaxResponseLength { get; init; } = 8000;
}
