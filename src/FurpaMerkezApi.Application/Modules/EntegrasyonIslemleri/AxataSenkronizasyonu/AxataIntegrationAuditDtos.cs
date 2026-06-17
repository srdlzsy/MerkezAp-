namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public interface IAxataIntegrationAuditService
{
    Task<AxataIntegrationAuditDto> GetOverviewAsync(
        AxataIntegrationAuditRequest request,
        CancellationToken cancellationToken);
}

public sealed record AxataIntegrationAuditRequest(
    DateTime? StartDate,
    DateTime? EndDate,
    int? WarehouseNo,
    int? Take,
    string? DocumentSerie,
    int? DocumentOrderNo,
    string? Statuses);

public sealed record AxataIntegrationAuditDto(
    bool IsInSync,
    DateTime GeneratedAtUtc,
    DateTime StartDate,
    DateTime EndDate,
    int? WarehouseNo,
    AxataIntegrationAuditSummaryDto Summary,
    IReadOnlyCollection<AxataOutboundDeliveryMovementSummaryDto> OutboundDeliverySummaries,
    IReadOnlyCollection<AxataUnsyncedWarehouseOrderDto> UnsyncedWarehouseOrders,
    IReadOnlyCollection<AxataSentWarehouseOrderMissingShipmentDto> SentWarehouseOrdersMissingMikroShipments,
    IReadOnlyCollection<AxataSentWarehouseOrderMissingShipmentDto> SentWarehouseOrdersWithShipmentDifferences,
    IReadOnlyCollection<AxataPendingOutboundDeliveryDto> PendingOutboundDeliveries,
    IReadOnlyCollection<AxataPendingOutboundDeliveryDto> InterventionCandidates,
    IReadOnlyCollection<AxataIntegrationAuditOperationDto> Operations,
    IReadOnlyCollection<string> Notes);

public sealed record AxataIntegrationAuditSummaryDto(
    int MikroWarehouseOrderDocumentCount,
    int SentWarehouseOrderDocumentCount,
    int PartiallySentWarehouseOrderDocumentCount,
    int UnsentWarehouseOrderDocumentCount,
    int SentWarehouseOrderMissingMikroShipmentDocumentCount,
    int SentWarehouseOrderMissingMikroShipmentLineCount,
    double SentWarehouseOrderMissingMikroShipmentQuantity,
    int SentWarehouseOrderShipmentDifferenceDocumentCount,
    int SentWarehouseOrderShipmentDifferenceLineCount,
    double SentWarehouseOrderShipmentDifferenceQuantity,
    int PendingOutboundDeliveryDocumentCount,
    int PendingOutboundDeliveryLineCount,
    double PendingOutboundDeliveryQuantity,
    int C01PendingDocumentCount,
    int C01MissingInMikroDocumentCount,
    int C01MikroExistsPendingAckDocumentCount);

public sealed record AxataOutboundDeliveryMovementSummaryDto(
    string MovementType,
    string PendingStatus,
    int PendingDocumentCount,
    int PendingLineCount,
    double PendingQuantity,
    int MikroMissingDocumentCount,
    int MikroExistsPendingAckDocumentCount,
    string CheckLevel);

public sealed record AxataIntegrationAuditOperationDto(
    string Code,
    string Title,
    string State,
    string Severity,
    int DocumentCount,
    int LineCount,
    double Quantity,
    string? ListRoute,
    string? PreviewRoute,
    string? ExecuteRoute,
    bool CanExecute,
    bool WritesData,
    string Description);

public sealed record AxataUnsyncedWarehouseOrderDto(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime DocumentDate,
    int InWarehouseNo,
    int OutWarehouseNo,
    int LineCount,
    int SentLineCount,
    int UnsentLineCount,
    double TotalQuantity,
    double SentQuantity,
    double UnsentQuantity,
    string State,
    DateTime? LastUpdateDate,
    string Warning);

public sealed record AxataSentWarehouseOrderMissingShipmentDto(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime DocumentDate,
    int InWarehouseNo,
    int OutWarehouseNo,
    int LineCount,
    int SentLineCount,
    int MissingMovementLinkLineCount,
    double TotalQuantity,
    double SentQuantity,
    double MissingMovementLinkQuantity,
    double DeliveredQuantity,
    int LinkedMovementLineCount,
    int DifferenceLineCount,
    double DifferenceQuantity,
    string DifferenceReason,
    string State,
    DateTime? LastUpdateDate,
    string Warning);

public sealed record AxataPendingOutboundDeliveryDto(
    string MovementType,
    string Status,
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string DocumentSerie,
    int? DocumentOrderNo,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    DateTime? AxataDate,
    int LineCount,
    double Quantity,
    int MikroOrderLineCount,
    double MikroOrderQuantity,
    double MikroDeliveredQuantity,
    int ExistingLinkedMovementLineCount,
    string MikroCheckState,
    bool CanIntervene,
    string? Warning);
