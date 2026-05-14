using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.WebApi.Logging;

public sealed class DailyFileLoggerProvider(
    IOptionsMonitor<FileLoggingOptions> optionsMonitor,
    IHostEnvironment hostEnvironment) : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, DailyFileLogger> loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();
    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, name => new DailyFileLogger(name, this));

    public void Dispose() => loggers.Clear();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        this.scopeProvider = scopeProvider;

    internal bool IsEnabled(LogLevel logLevel)
    {
        var options = optionsMonitor.CurrentValue;
        return options.Enabled && logLevel != LogLevel.None && logLevel >= options.MinimumLevel;
    }

    internal void WriteLog<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var options = optionsMonitor.CurrentValue;

        if (!options.Enabled || logLevel == LogLevel.None || logLevel < options.MinimumLevel)
        {
            return;
        }

        var message = formatter(state, exception);

        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        var timestamp = DateTimeOffset.Now;
        var scopeText = CollectScopeText();
        var entry = FormatEntry(timestamp, categoryName, logLevel, eventId, message, exception, scopeText);
        var basePath = ResolveBasePath(options.BasePath);
        var applicationLogPath = BuildFilePath(basePath, options.ApplicationFilePrefix, timestamp);

        lock (syncRoot)
        {
            Append(applicationLogPath, entry);

            if (logLevel >= options.ErrorFileMinimumLevel)
            {
                var errorLogPath = BuildFilePath(basePath, options.ErrorFilePrefix, timestamp);
                Append(errorLogPath, entry);
            }
        }
    }

    private string ResolveBasePath(string configuredPath) =>
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath);

    private static string BuildFilePath(string basePath, string filePrefix, DateTimeOffset timestamp)
    {
        var monthFolder = timestamp.ToString("yyyy-MM");
        var fileName = $"{filePrefix}-{timestamp:yyyy-MM-dd}.log";
        return Path.Combine(basePath, monthFolder, fileName);
    }

    private static void Append(string filePath, string contents)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(filePath, contents, Encoding.UTF8);
    }

    private string? CollectScopeText()
    {
        var scopeValues = new List<string>();

        scopeProvider.ForEachScope((scope, values) =>
        {
            switch (scope)
            {
                case IEnumerable<KeyValuePair<string, object?>> pairs:
                    foreach (var pair in pairs)
                    {
                        if (string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        values.Add($"{pair.Key}={pair.Value}");
                    }

                    break;

                default:
                    if (!string.IsNullOrWhiteSpace(scope?.ToString()))
                    {
                        values.Add(scope!.ToString()!);
                    }

                    break;
            }
        }, scopeValues);

        return scopeValues.Count == 0
            ? null
            : string.Join(", ", scopeValues.Distinct(StringComparer.Ordinal));
    }

    private static string FormatEntry(
        DateTimeOffset timestamp,
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception,
        string? scopeText)
    {
        var builder = new StringBuilder();
        builder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
        builder.Append(" [");
        builder.Append(logLevel.ToString().ToUpperInvariant());
        builder.Append("] ");
        builder.Append(categoryName);

        if (eventId.Id != 0)
        {
            builder.Append(" EventId=");
            builder.Append(eventId.Id);
        }

        if (!string.IsNullOrWhiteSpace(scopeText))
        {
            builder.Append(" Scope=");
            builder.Append(scopeText);
        }

        builder.AppendLine();
        builder.Append("Message: ");
        builder.AppendLine(message);

        if (exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(exception.ToString());
        }

        builder.AppendLine(new string('-', 120));
        return builder.ToString();
    }

    private sealed class DailyFileLogger(string categoryName, DailyFileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider.scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.WriteLog(categoryName, logLevel, eventId, state, exception, formatter);
        }
    }
}
