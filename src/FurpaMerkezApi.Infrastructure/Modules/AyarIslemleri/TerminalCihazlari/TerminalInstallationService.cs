using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.TerminalCihazlari;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.TerminalCihazlari;

public sealed class TerminalInstallationService(
    AuthDbContext dbContext,
    IClock clock,
    TerminalVersionManifestProvider manifestProvider) : ITerminalInstallationService
{
    private const int MaxTake = 1000;

    public async Task<TerminalInstallationDto> RecordHeartbeatAsync(
        TerminalHeartbeatRequest request,
        TerminalHeartbeatContext context,
        CancellationToken cancellationToken)
    {
        var deviceId = NormalizeRequired(context.DeviceId, nameof(context.DeviceId), 100);
        var supportedAbis = NormalizeAbis(request.SupportedAbis);
        var now = clock.UtcNow;

        var installation = await dbContext.TerminalInstallations
            .SingleOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken);
        var isNewInstallation = installation is null;

        if (installation is null)
        {
            installation = new TerminalInstallation(
                Guid.NewGuid(),
                deviceId,
                request.AppVersion,
                request.BuildNumber,
                context.WarehouseNo,
                context.UserId,
                request.Manufacturer,
                request.DeviceModel,
                request.AndroidVersion,
                request.AndroidSdk,
                supportedAbis,
                context.IpAddress,
                now);
            dbContext.TerminalInstallations.Add(installation);
        }
        else
        {
            installation.RecordHeartbeat(
                request.AppVersion,
                request.BuildNumber,
                context.WarehouseNo,
                context.UserId,
                request.Manufacturer,
                request.DeviceModel,
                request.AndroidVersion,
                request.AndroidSdk,
                supportedAbis,
                context.IpAddress,
                now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (isNewInstallation)
        {
            // Two first heartbeats may race. The unique device id remains the source of truth.
            dbContext.ChangeTracker.Clear();
            installation = await dbContext.TerminalInstallations
                .SingleOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken)
                ?? throw new InvalidOperationException("Terminal installation could not be recovered after a concurrent heartbeat.", exception);
            installation.RecordHeartbeat(
                request.AppVersion,
                request.BuildNumber,
                context.WarehouseNo,
                context.UserId,
                request.Manufacturer,
                request.DeviceModel,
                request.AndroidVersion,
                request.AndroidSdk,
                supportedAbis,
                context.IpAddress,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == installation.UserId)
            .Select(item => new UserProjection(item.Username, item.FirstName, item.LastName))
            .SingleOrDefaultAsync(cancellationToken);
        return Map(installation, user, manifest: null, now);
    }

    public async Task<IReadOnlyCollection<TerminalInstallationDto>> ListAsync(
        TerminalInstallationListRequest request,
        CancellationToken cancellationToken)
    {
        var query = BuildScopeQuery(request.WarehouseNo);
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.Installation.DeviceId.Contains(search) ||
                (item.Installation.DeviceModel != null && item.Installation.DeviceModel.Contains(search)) ||
                (item.Installation.Manufacturer != null && item.Installation.Manufacturer.Contains(search)) ||
                item.User.Username.Contains(search) ||
                item.User.FirstName.Contains(search) ||
                item.User.LastName.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(request.AppVersion))
        {
            var version = request.AppVersion.Trim();
            query = query.Where(item => item.Installation.AppVersion == version);
        }
        if (request.ActiveWithinDays is > 0)
        {
            var activeSince = clock.UtcNow.AddDays(-Math.Clamp(request.ActiveWithinDays.Value, 1, 3650));
            query = query.Where(item => item.Installation.LastSeenAtUtc >= activeSince);
        }

        var manifest = await manifestProvider.GetAsync(cancellationToken);
        if (request.IsCurrentVersion.HasValue && manifest?.BuildNumber is not null)
        {
            query = request.IsCurrentVersion.Value
                ? query.Where(item => item.Installation.BuildNumber >= manifest.BuildNumber.Value)
                : query.Where(item => item.Installation.BuildNumber < manifest.BuildNumber.Value);
        }

        var rows = await query
            .OrderByDescending(item => item.Installation.LastSeenAtUtc)
            .Take(Math.Clamp(request.Take, 1, MaxTake))
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        return rows.Select(item => Map(
                item.Installation,
                new UserProjection(item.User.Username, item.User.FirstName, item.User.LastName),
                manifest,
                now))
            .ToArray();
    }

    public async Task<TerminalInstallationDto> GetAsync(
        Guid id,
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var row = await BuildScopeQuery(warehouseNo)
            .SingleOrDefaultAsync(item => item.Installation.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Terminal installation was not found.");
        return Map(
            row.Installation,
            new UserProjection(row.User.Username, row.User.FirstName, row.User.LastName),
            await manifestProvider.GetAsync(cancellationToken),
            clock.UtcNow);
    }

    public async Task<TerminalInstallationSummaryDto> GetSummaryAsync(
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var installations = await dbContext.TerminalInstallations.AsNoTracking()
            .Where(item => !warehouseNo.HasValue || item.WarehouseNo == warehouseNo.Value)
            .ToListAsync(cancellationToken);
        var manifest = await manifestProvider.GetAsync(cancellationToken);
        var currentBuild = manifest?.BuildNumber;

        var versions = installations
            .GroupBy(item => new { item.AppVersion, item.BuildNumber })
            .Select(group => new TerminalVersionDistributionDto(
                group.Key.AppVersion,
                group.Key.BuildNumber,
                group.Count(),
                group.Count(item => item.LastSeenAtUtc >= now.AddDays(-7)),
                currentBuild.HasValue ? group.Key.BuildNumber >= currentBuild.Value : null))
            .OrderByDescending(item => item.BuildNumber)
            .ToArray();
        var warehouses = installations
            .GroupBy(item => item.WarehouseNo)
            .Select(group => new TerminalWarehouseDistributionDto(
                group.Key,
                group.Count(),
                group.Count(item => item.LastSeenAtUtc >= now.AddDays(-7)),
                currentBuild.HasValue ? group.Count(item => item.BuildNumber < currentBuild.Value) : null))
            .OrderBy(item => item.WarehouseNo)
            .ToArray();

        return new TerminalInstallationSummaryDto(
            now,
            manifest?.Version,
            currentBuild,
            manifest?.CheckedAtUtc,
            manifest is not null,
            installations.Count,
            installations.Count(item => item.LastSeenAtUtc >= now.AddHours(-24)),
            installations.Count(item => item.LastSeenAtUtc >= now.AddDays(-7)),
            installations.Count(item => item.LastSeenAtUtc >= now.AddDays(-30)),
            currentBuild.HasValue ? installations.Count(item => item.BuildNumber < currentBuild.Value) : null,
            installations.Count(item => item.WarehouseChangeCount > 0),
            versions,
            warehouses);
    }

    private IQueryable<InstallationWithUser> BuildScopeQuery(int? warehouseNo) =>
        from installation in dbContext.TerminalInstallations.AsNoTracking()
        join user in dbContext.Users.AsNoTracking() on installation.UserId equals user.Id
        where !warehouseNo.HasValue || installation.WarehouseNo == warehouseNo.Value
        select new InstallationWithUser(installation, user);

    private static TerminalInstallationDto Map(
        TerminalInstallation item,
        UserProjection? user,
        TerminalVersionManifest? manifest,
        DateTime now) =>
        new(
            item.Id,
            item.DeviceId,
            item.AppVersion,
            item.BuildNumber,
            item.WarehouseNo,
            item.UserId,
            user?.Username,
            user is null ? null : string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            item.Manufacturer,
            item.DeviceModel,
            item.AndroidVersion,
            item.AndroidSdk,
            SplitAbis(item.SupportedAbis),
            item.FirstSeenAtUtc,
            item.LastSeenAtUtc,
            item.LastIpAddress,
            item.PreviousWarehouseNo,
            item.WarehouseChangedAtUtc,
            item.WarehouseChangeCount,
            item.VersionChangedAtUtc,
            item.LastSeenAtUtc >= now.AddHours(-24),
            item.LastSeenAtUtc >= now.AddDays(-7),
            manifest?.BuildNumber is int currentBuild ? item.BuildNumber >= currentBuild : null);

    private static string NormalizeAbis(IReadOnlyCollection<string>? values) =>
        string.Join(',', values?.Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(20) ?? []);

    private static IReadOnlyCollection<string> SplitAbis(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} is required and can not exceed {maxLength} characters.", parameterName);
        }
        return normalized;
    }

    private sealed record InstallationWithUser(TerminalInstallation Installation, AppUser User);
    private sealed record UserProjection(string Username, string FirstName, string LastName);
}

public sealed record TerminalVersionManifest(string Version, int? BuildNumber, DateTime CheckedAtUtc);

public sealed class TerminalVersionManifestProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IClock clock,
    IOptionsMonitor<TerminalInstallationOptions> options)
{
    private const string CacheKey = "terminal-installations:version-manifest";
    private const string LastKnownCacheKey = "terminal-installations:version-manifest:last-known";

    public async Task<TerminalVersionManifest?> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out TerminalVersionManifest? cached) && cached is not null)
        {
            return cached;
        }

        var settings = options.CurrentValue;
        if (!Uri.TryCreate(settings.VersionManifestUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return LastKnown();
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await httpClientFactory.CreateClient().GetAsync(uri, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return LastKnown();
            }
            var payload = await response.Content.ReadFromJsonAsync<ManifestPayload>(cancellationToken: timeout.Token);
            if (string.IsNullOrWhiteSpace(payload?.Version))
            {
                return LastKnown();
            }

            var result = new TerminalVersionManifest(payload.Version.Trim(), payload.BuildNumber, clock.UtcNow);
            cache.Set(CacheKey, result, TimeSpan.FromMinutes(Math.Clamp(settings.ManifestCacheMinutes, 1, 60)));
            cache.Set(LastKnownCacheKey, result);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return LastKnown();
        }
    }

    private TerminalVersionManifest? LastKnown() =>
        cache.TryGetValue(LastKnownCacheKey, out TerminalVersionManifest? lastKnown)
            ? lastKnown
            : null;

    private sealed record ManifestPayload(
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("buildNumber")] int? BuildNumber);
}
