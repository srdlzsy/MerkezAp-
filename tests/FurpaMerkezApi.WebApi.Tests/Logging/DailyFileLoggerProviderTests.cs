using FurpaMerkezApi.WebApi.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Logging;

public sealed class DailyFileLoggerProviderTests
{
    [Fact]
    public void Logger_WritesEachLevelToItsOwnDailyFile()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"furpa-log-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            var options = new FileLoggingOptions
            {
                BasePath = rootPath,
                InformationFilePrefix = "information",
                WarningFilePrefix = "warnings",
                ErrorFilePrefix = "errors",
                MinimumLevel = LogLevel.Information
            };
            using var provider = new DailyFileLoggerProvider(
                new StaticOptionsMonitor<FileLoggingOptions>(options),
                new TestHostEnvironment(rootPath));
            var logger = provider.CreateLogger("LogSeparationTest");

            logger.LogInformation("information-entry");
            logger.LogWarning("warning-entry");
            logger.LogError("error-entry");
            logger.LogCritical("critical-entry");

            var information = ReadSingleLog(rootPath, "information-*.log");
            var warnings = ReadSingleLog(rootPath, "warnings-*.log");
            var errors = ReadSingleLog(rootPath, "errors-*.log");

            Assert.Contains("information-entry", information);
            Assert.DoesNotContain("warning-entry", information);
            Assert.DoesNotContain("error-entry", information);
            Assert.Contains("warning-entry", warnings);
            Assert.DoesNotContain("information-entry", warnings);
            Assert.DoesNotContain("error-entry", warnings);
            Assert.Contains("error-entry", errors);
            Assert.Contains("critical-entry", errors);
            Assert.DoesNotContain("warning-entry", errors);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string ReadSingleLog(string rootPath, string pattern)
    {
        var path = Assert.Single(Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories));
        return File.ReadAllText(path);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "FurpaMerkezApi.WebApi.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
