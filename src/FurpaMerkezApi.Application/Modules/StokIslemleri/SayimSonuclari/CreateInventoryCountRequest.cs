namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record CreateInventoryCountRequest(
    int WarehouseNo,
    Guid RequestedByUserId,
    Guid? ClientRequestId,
    string? Name,
    DateTime? DocumentDate,
    IReadOnlyCollection<CreateInventoryCountLineRequest> Lines);

public sealed record CreateInventoryCountLineRequest(
    string StockCode,
    double Quantity,
    string? Barcode = null,
    int UnitPointer = 1);
