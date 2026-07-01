namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;

public sealed class DocumentFlowTrackingOptions
{
    public const string SectionName = "DocumentFlowTracking";

    public bool Enabled { get; init; } = true;
}
