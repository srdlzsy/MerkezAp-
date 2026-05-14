namespace FurpaMerkezApi.WebApi.Logging;

public sealed class FileLoggingOptions
{
    public bool Enabled { get; init; } = true;

    public string BasePath { get; init; } = "logs";

    public string ApplicationFilePrefix { get; init; } = "application";

    public string ErrorFilePrefix { get; init; } = "errors";

    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    public LogLevel ErrorFileMinimumLevel { get; init; } = LogLevel.Error;
}
