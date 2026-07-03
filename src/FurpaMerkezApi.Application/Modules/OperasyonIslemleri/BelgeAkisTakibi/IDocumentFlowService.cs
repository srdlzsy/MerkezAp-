using FurpaMerkezApi.Domain.Entities;

namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;

public interface IDocumentFlowService
{
    Task<DocumentFlowListResponse> ListAsync(
        DocumentFlowListRequest request,
        CancellationToken cancellationToken);

    Task<DocumentFlowDetailDto> GetAsync(
        Guid id,
        int? allowedWarehouseNo,
        CancellationToken cancellationToken);

    Task RecordAsync(
        RecordDocumentFlowRequest request,
        CancellationToken cancellationToken);
}

public sealed record DocumentFlowListRequest(
    int? WarehouseNo,
    DateTime? StartDate,
    DateTime? EndDate,
    DocumentFlowType? DocumentType,
    DocumentFlowStatus? Status,
    string? Search,
    int Take = 100);

public sealed record DocumentFlowListResponse(
    bool TrackingEnabled,
    int TotalCount,
    IReadOnlyCollection<DocumentFlowListItemDto> Items);

public sealed record DocumentFlowListItemDto(
    Guid Id,
    string DocumentType,
    int SourceWarehouseNo,
    int? TargetWarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    string? DocumentNo,
    string? ExternalDocumentNo,
    string? ExternalUuid,
    string Status,
    string CurrentStep,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DocumentFlowDetailDto(
    Guid Id,
    string FlowKey,
    string DocumentType,
    int SourceWarehouseNo,
    int? TargetWarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    string? DocumentNo,
    string? ExternalDocumentNo,
    string? ExternalUuid,
    string Status,
    string CurrentStep,
    string? LastError,
    Guid? LastChangedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<DocumentFlowEventDto> Events);

public sealed record DocumentFlowEventDto(
    Guid Id,
    string Step,
    string Status,
    string Message,
    string? Error,
    Guid? ChangedByUserId,
    DateTime OccurredAtUtc);

public sealed record RecordDocumentFlowRequest(
    string FlowKey,
    DocumentFlowType DocumentType,
    int SourceWarehouseNo,
    int? TargetWarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    DocumentFlowStep Step,
    DocumentFlowStatus Status,
    string Message,
    string? Error = null,
    Guid? ChangedByUserId = null,
    string? DocumentNo = null,
    string? ExternalDocumentNo = null,
    string? ExternalUuid = null);

public static class DocumentFlowKeys
{
    public static string Create(
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        string documentSerie,
        int documentOrderNo) =>
        $"{documentType}:{sourceWarehouseNo}:{documentSerie.Trim().ToUpperInvariant()}:{documentOrderNo}";

    public static string CreateEntity(
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        string entityKind,
        string entityKey) =>
        $"{documentType}:{sourceWarehouseNo}:{entityKind.Trim().ToUpperInvariant()}:{entityKey.Trim().ToUpperInvariant()}";
}
