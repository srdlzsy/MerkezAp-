namespace FurpaMerkezApi.Application.Modules.Home.DepoOncelikleri;

public interface IHomeWarehousePrioritiesService
{
    Task<HomeWarehousePrioritiesDto> GetAsync(
        HomeWarehousePrioritiesRequest request,
        CancellationToken cancellationToken);
}

public sealed record HomeWarehousePrioritiesRequest(
    DateOnly Date,
    int? WarehouseNo,
    string? WarehouseName,
    Guid UserId);

public sealed record HomeWarehousePrioritiesDto(
    DateOnly Date,
    DateTime GeneratedAtUtc,
    int? WarehouseNo,
    string WarehouseName,
    string OverallStatus,
    string Headline,
    IReadOnlyCollection<HomePriorityMetricDto> Metrics,
    IReadOnlyCollection<HomePriorityItemDto> Priorities,
    IReadOnlyCollection<HomeQuickActionDto> QuickActions);

public sealed record HomePriorityMetricDto(
    string Code,
    string Label,
    int Value,
    string Severity,
    string? Route);

public sealed record HomePriorityItemDto(
    string Code,
    string Severity,
    string Title,
    string Description,
    int Count,
    string Route);

public sealed record HomeQuickActionDto(
    string Code,
    string Label,
    string Route,
    string? PermissionCode);
