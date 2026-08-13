namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record UpdateWarehouseShippingDocumentRequest(
    int SourceWarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    bool IsReturn,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    int? TargetWarehouseNo,
    int? TransitWarehouseNo,
    string? Description,
    IReadOnlyCollection<UpdateWarehouseShippingDocumentLineRequest> Lines,
    Guid? RequestedByUserId = null);

public sealed record UpdateWarehouseShippingDocumentLineRequest(
    Guid MovementGuid,
    int? RowNo = null,
    string? StockCode = null,
    double? Quantity = null,
    double? UnitPrice = null,
    double? Amount = null,
    int? UnitPointer = null,
    string? Description = null,
    string? PartyCode = null,
    int? LotNo = null,
    string? ProjectCode = null,
    string? CustomerResponsibilityCenter = null,
    string? ProductResponsibilityCenter = null);
