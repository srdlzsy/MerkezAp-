namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record CreateStockReceiptRequest(
    int WarehouseNo,
    string Creator,
    string Acceptor,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? Description,
    IReadOnlyCollection<CreateStockReceiptLineRequest> Lines);

public sealed record CreateStockReceiptLineRequest(
    string StockCode,
    double Quantity,
    int UnitPointer = 1,
    string? Description = null,
    string? PartyCode = null,
    int LotNo = 0,
    string? ProjectCode = null);
