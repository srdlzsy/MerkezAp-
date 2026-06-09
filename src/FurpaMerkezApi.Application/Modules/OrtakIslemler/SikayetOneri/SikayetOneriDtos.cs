namespace FurpaMerkezApi.Application.Modules.OrtakIslemler.SikayetOneri;

public sealed record CreateFeedbackItemRequest(
    string Type,
    string Title,
    string Message,
    string? Priority,
    Guid UserId,
    string Username,
    string FullName,
    int WarehouseNo,
    string WarehouseName);

public sealed record FeedbackManagementListRequest(
    string? Status,
    string? Type,
    int? WarehouseNo,
    DateTime? StartDate,
    DateTime? EndDate,
    int? Take,
    bool CanViewAll,
    int CurrentUserWarehouseNo);

public sealed record FeedbackManagementScope(
    bool CanViewAll,
    int CurrentUserWarehouseNo);

public sealed record FeedbackManagementActionContext(
    Guid UserId,
    bool CanViewAll,
    int CurrentUserWarehouseNo);

public sealed record ChangeFeedbackStatusRequest(
    string Status,
    string? AdminNote);

public sealed record FeedbackItemDto(
    Guid Id,
    string Type,
    string TypeName,
    string Title,
    string Message,
    string Status,
    string StatusName,
    string Priority,
    string PriorityName,
    Guid CreatedByUserId,
    string CreatedByUsername,
    string CreatedByFullName,
    int WarehouseNo,
    string WarehouseName,
    string? AdminNote,
    DateTime? ReadAtUtc,
    Guid? ReadByUserId,
    DateTime? StatusChangedAtUtc,
    Guid? StatusChangedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? ClosedAtUtc);

public sealed record FeedbackSummaryDto(
    int MyOpenCount,
    int MyResolvedCount,
    string? LatestStatus,
    DateTime? LatestCreatedAtUtc);
