namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public sealed record CreateCompanyReceivingRequest(
    int WarehouseNo,
    Guid RequestedByUserId,
    Guid? ClientRequestId,
    string CustomerCode,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? Deliverer,
    string? Receiver,
    string? Description,
    bool AllowOrderOverReceiving,
    bool AutoCreateReturnForPartialAcceptance,
    IReadOnlyCollection<CreateCompanyReceivingLineRequest> Lines);

public sealed record CreateCompanyReceivingLineRequest(
    string StockCode,
    double Quantity,
    double? DispatchQuantity = null,
    double? AcceptedQuantity = null,
    double UnitPrice = 0d,
    int UnitPointer = 1,
    DateTime? LastConsumingDate = null,
    Guid? OrderGuid = null,
    string? Description = null,
    string? PartyCode = null,
    int LotNo = 0,
    string? ProjectCode = null,
    string? CustomerResponsibilityCenter = null,
    string? ProductResponsibilityCenter = null);
