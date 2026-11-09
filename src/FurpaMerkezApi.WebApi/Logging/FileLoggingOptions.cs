namespace FurpaMerkezApi.WebApi.Logging;

public sealed class FileLoggingOptions
{
    public bool Enabled { get; init; } = true;

    public string BasePath { get; init; } = "logs";

    public string InformationFilePrefix { get; init; } = "information";

    public string WarningFilePrefix { get; init; } = "warnings";

    public string ErrorFilePrefix { get; init; } = "errors";

    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
}
