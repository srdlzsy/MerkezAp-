using System.Globalization;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

public static class OperationsExportPathResolver
{
    public static string ResolveBasePath(OperationsExportOptions options) =>
        string.IsNullOrWhiteSpace(options.BasePath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "OperationsExports")
            : options.BasePath.Trim();

    public static string ResolveOperationDirectory(
        OperationsExportOptions options,
        int warehouseNo,
        Guid jobId,
        string operationFolder) =>
        Path.Combine(
            ResolveBasePath(options),
            warehouseNo.ToString(CultureInfo.InvariantCulture),
            operationFolder,
            jobId.ToString("N"));
}
