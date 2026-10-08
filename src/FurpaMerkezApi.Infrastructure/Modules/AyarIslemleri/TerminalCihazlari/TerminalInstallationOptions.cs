namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.TerminalCihazlari;

public sealed class TerminalInstallationOptions
{
    public const string SectionName = "TerminalInstallations";

    public string VersionManifestUrl { get; set; } = "http://10.0.0.100:802/Terminal/version.json";
    public int ManifestCacheMinutes { get; set; } = 5;
}
