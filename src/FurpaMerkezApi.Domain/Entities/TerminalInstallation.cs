namespace FurpaMerkezApi.Domain.Entities;

public sealed class TerminalInstallation
{
    private TerminalInstallation()
    {
        DeviceId = string.Empty;
        AppVersion = string.Empty;
        SupportedAbis = string.Empty;
    }

    public Guid Id { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string AppVersion { get; private set; } = string.Empty;
    public int BuildNumber { get; private set; }
    public int WarehouseNo { get; private set; }
    public Guid UserId { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? DeviceModel { get; private set; }
    public string? AndroidVersion { get; private set; }
    public int? AndroidSdk { get; private set; }
    public string SupportedAbis { get; private set; } = string.Empty;
    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public string? LastIpAddress { get; private set; }
    public int? PreviousWarehouseNo { get; private set; }
    public DateTime? WarehouseChangedAtUtc { get; private set; }
    public int WarehouseChangeCount { get; private set; }
    public DateTime? VersionChangedAtUtc { get; private set; }

    public TerminalInstallation(
        Guid id,
        string deviceId,
        string appVersion,
        int buildNumber,
        int warehouseNo,
        Guid userId,
        string? manufacturer,
        string? deviceModel,
        string? androidVersion,
        int? androidSdk,
        string supportedAbis,
        string? ipAddress,
        DateTime seenAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Terminal installation id can not be empty.", nameof(id));
        }

        Id = id;
        DeviceId = NormalizeRequired(deviceId, nameof(deviceId), 100);
        FirstSeenAtUtc = NormalizeUtc(seenAtUtc);
        LastSeenAtUtc = FirstSeenAtUtc;
        ApplyHeartbeat(
            appVersion,
            buildNumber,
            warehouseNo,
            userId,
            manufacturer,
            deviceModel,
            androidVersion,
            androidSdk,
            supportedAbis,
            ipAddress,
            FirstSeenAtUtc,
            isInitial: true);
    }

    public void RecordHeartbeat(
        string appVersion,
        int buildNumber,
        int warehouseNo,
        Guid userId,
        string? manufacturer,
        string? deviceModel,
        string? androidVersion,
        int? androidSdk,
        string supportedAbis,
        string? ipAddress,
        DateTime seenAtUtc) =>
        ApplyHeartbeat(
            appVersion,
            buildNumber,
            warehouseNo,
            userId,
            manufacturer,
            deviceModel,
            androidVersion,
            androidSdk,
            supportedAbis,
            ipAddress,
            NormalizeUtc(seenAtUtc),
            isInitial: false);

    private void ApplyHeartbeat(
        string appVersion,
        int buildNumber,
        int warehouseNo,
        Guid userId,
        string? manufacturer,
        string? deviceModel,
        string? androidVersion,
        int? androidSdk,
        string supportedAbis,
        string? ipAddress,
        DateTime seenAtUtc,
        bool isInitial)
    {
        if (buildNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(buildNumber));
        }
        if (warehouseNo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warehouseNo));
        }
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(userId));
        }

        var normalizedVersion = NormalizeRequired(appVersion, nameof(appVersion), 40);
        if (!isInitial && (BuildNumber != buildNumber || !string.Equals(AppVersion, normalizedVersion, StringComparison.Ordinal)))
        {
            VersionChangedAtUtc = seenAtUtc;
        }
        if (!isInitial && WarehouseNo != warehouseNo)
        {
            PreviousWarehouseNo = WarehouseNo;
            WarehouseChangedAtUtc = seenAtUtc;
            WarehouseChangeCount++;
        }

        AppVersion = normalizedVersion;
        BuildNumber = buildNumber;
        WarehouseNo = warehouseNo;
        UserId = userId;
        Manufacturer = NormalizeOptional(manufacturer, 100);
        DeviceModel = NormalizeOptional(deviceModel, 150);
        AndroidVersion = NormalizeOptional(androidVersion, 40);
        AndroidSdk = androidSdk is > 0 ? androidSdk : null;
        SupportedAbis = NormalizeOptional(supportedAbis, 500) ?? string.Empty;
        LastIpAddress = NormalizeOptional(ipAddress, 64);
        LastSeenAtUtc = seenAtUtc > LastSeenAtUtc ? seenAtUtc : LastSeenAtUtc;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} is required and can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return null;
        }
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.");
        }
        return normalized;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
