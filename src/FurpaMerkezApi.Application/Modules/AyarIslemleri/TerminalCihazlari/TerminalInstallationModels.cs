namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.TerminalCihazlari;

public sealed record TerminalHeartbeatRequest(
    string AppVersion,
    int BuildNumber,
    string? Manufacturer,
    string? DeviceModel,
    string? AndroidVersion,
    int? AndroidSdk,
    IReadOnlyCollection<string>? SupportedAbis);

public sealed record TerminalHeartbeatContext(
    string DeviceId,
    Guid UserId,
    int WarehouseNo,
    string? IpAddress);

public sealed record TerminalInstallationListRequest(
    int? WarehouseNo,
    string? Search,
    string? AppVersion,
    bool? IsCurrentVersion,
    int? ActiveWithinDays,
    int Take = 200);

public sealed record TerminalInstallationDto(
    Guid Id,
    string DeviceId,
    string AppVersion,
    int BuildNumber,
    int WarehouseNo,
    Guid UserId,
    string? Username,
    string? UserFullName,
    string? Manufacturer,
    string? DeviceModel,
    string? AndroidVersion,
    int? AndroidSdk,
    IReadOnlyCollection<string> SupportedAbis,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    string? LastIpAddress,
    int? PreviousWarehouseNo,
    DateTime? WarehouseChangedAtUtc,
    int WarehouseChangeCount,
    DateTime? VersionChangedAtUtc,
    bool IsActiveLast24Hours,
    bool IsActiveLast7Days,
    bool? IsCurrentVersion);

public sealed record TerminalVersionDistributionDto(
    string AppVersion,
    int BuildNumber,
    int InstallationCount,
    int ActiveLast7DaysCount,
    bool? IsCurrentVersion);

public sealed record TerminalWarehouseDistributionDto(
    int WarehouseNo,
    int InstallationCount,
    int ActiveLast7DaysCount,
    int? OutdatedCount);

public sealed record TerminalInstallationSummaryDto(
    DateTime GeneratedAtUtc,
    string? CurrentAppVersion,
    int? CurrentBuildNumber,
    DateTime? VersionManifestCheckedAtUtc,
    bool IsVersionManifestAvailable,
    int RegisteredInstallationCount,
    int ActiveLast24HoursCount,
    int ActiveLast7DaysCount,
    int ActiveLast30DaysCount,
    int? OutdatedInstallationCount,
    int WarehouseChangedInstallationCount,
    IReadOnlyCollection<TerminalVersionDistributionDto> Versions,
    IReadOnlyCollection<TerminalWarehouseDistributionDto> Warehouses);

public interface ITerminalInstallationService
{
    Task<TerminalInstallationDto> RecordHeartbeatAsync(
        TerminalHeartbeatRequest request,
        TerminalHeartbeatContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TerminalInstallationDto>> ListAsync(
        TerminalInstallationListRequest request,
        CancellationToken cancellationToken);

    Task<TerminalInstallationDto> GetAsync(Guid id, int? warehouseNo, CancellationToken cancellationToken);

    Task<TerminalInstallationSummaryDto> GetSummaryAsync(int? warehouseNo, CancellationToken cancellationToken);
}
