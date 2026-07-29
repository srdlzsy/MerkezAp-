namespace FurpaMerkezApi.Application.Modules.OrtakIslemler.Duyurular;

public sealed record AnnouncementActorContext(
    Guid UserId,
    string Username,
    string FullName,
    int WarehouseNo,
    string WarehouseName,
    bool CanTargetAllWarehouses);

public sealed record CreateAnnouncementRequest(
    string Title,
    string Message,
    string? Priority,
    string TargetType,
    IReadOnlyCollection<int>? TargetWarehouseNos,
    IReadOnlyCollection<Guid>? TargetUserIds,
    DateTime? StartsAtUtc,
    DateTime? ExpiresAtUtc,
    AnnouncementActorContext Actor);

public sealed record UpdateAnnouncementRequest(
    string Title,
    string Message,
    string? Priority,
    string TargetType,
    IReadOnlyCollection<int>? TargetWarehouseNos,
    IReadOnlyCollection<Guid>? TargetUserIds,
    DateTime? StartsAtUtc,
    DateTime? ExpiresAtUtc,
    AnnouncementActorContext Actor);

public sealed record AnnouncementInboxRequest(
    Guid UserId,
    int WarehouseNo,
    bool IncludeRead,
    int? Take);

public sealed record AnnouncementManagementListRequest(
    string? Status,
    string? TargetType,
    int? TargetWarehouseNo,
    Guid? TargetUserId,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IncludeArchived,
    int? Take,
    AnnouncementActorContext Actor);

public sealed record AnnouncementSummaryDto(
    int ActiveCount,
    int UnreadCount,
    Guid? LatestAnnouncementId,
    DateTime? LatestPublishedAtUtc);

public sealed record AnnouncementDto(
    Guid Id,
    string Title,
    string Message,
    string Priority,
    string PriorityName,
    string Status,
    string StatusName,
    Guid CreatedByUserId,
    string CreatedByUsername,
    string CreatedByFullName,
    DateTime? StartsAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime PublishedAtUtc,
    DateTime? ArchivedAtUtc,
    Guid? ArchivedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? ReadAtUtc,
    IReadOnlyCollection<AnnouncementTargetDto> Targets);

public sealed record AnnouncementTargetDto(
    Guid Id,
    string Type,
    string TypeName,
    int? WarehouseNo,
    string? WarehouseName,
    Guid? UserId,
    string? Username,
    string? UserFullName);
