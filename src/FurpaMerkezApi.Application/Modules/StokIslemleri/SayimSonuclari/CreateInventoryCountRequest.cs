using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record CreateInventoryCountRequest(
    int WarehouseNo,
    Guid RequestedByUserId,
    Guid? ClientRequestId,
    string? Name,
    DateTime? DocumentDate,
    IReadOnlyCollection<CreateInventoryCountLineRequest> Lines,
    bool ForceCreateNew = false) : IExplicitNewCreateRequest;

public sealed record CreateInventoryCountLineRequest(
    string StockCode,
    double Quantity,
    string? Barcode = null,
    int UnitPointer = 1);
