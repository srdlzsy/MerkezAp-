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

    async Task RecordManyAsync(
        IReadOnlyCollection<RecordDocumentFlowRequest> requests,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            await RecordAsync(request, cancellationToken);
        }
    }
}

public enum DocumentFlowDirection
{
    All = 0,
    Incoming = 1,
    Outgoing = 2
}

public enum DocumentFlowCategory
{
    All = 0,
    ShipmentsAndReceiving = 1,
    Returns = 2,
    Orders = 3,
    CashAndStore = 4,
    StockAndCounting = 5
}

public enum DocumentFlowPreset
{
    All = 0,
    ActionRequired = 1,
    PendingReceiving = 2,
    UnacceptedOutgoing = 3,
    PendingEDespatch = 4,
    FailedEDespatch = 5,
    OpenOrders = 6,
    FailedOnly = 7,
    TodayShipments = 8,
    TodayReturns = 9,
    TodayReceivings = 10,
    OtherFailedOperations = 11
}

public sealed record DocumentFlowListRequest(
    int? WarehouseNo,
    DateTime? StartDate,
    DateTime? EndDate,
    DocumentFlowType? DocumentType,
    DocumentFlowStatus? Status,
    string? Search,
    int Skip = 0,
    int Take = 100,
    DocumentFlowDirection Direction = DocumentFlowDirection.All,
    DocumentFlowStep? CurrentStep = null,
    DocumentFlowCategory Category = DocumentFlowCategory.All,
    DocumentFlowPreset Preset = DocumentFlowPreset.All);

public sealed record DocumentFlowSummaryDto(
    int TotalCount,
    int SucceededCount,
    int FailedCount,
    int ActionRequiredCount,
    int PendingReceivingCount,
    int UnacceptedOutgoingCount,
    int PendingEDespatchCount,
    int FailedEDespatchCount,
    int OpenOrderCount);

public sealed record DocumentFlowListResponse(
    bool TrackingEnabled,
    int TotalCount,
    IReadOnlyCollection<DocumentFlowListItemDto> Items,
    DocumentFlowSummaryDto? Summary = null);

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
