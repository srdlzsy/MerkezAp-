namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record CreateStockReceiptResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime MovementDate,
    DateTime DocumentDate,
    string DocumentNo,
    int WarehouseNo,
    string Creator,
    string Acceptor,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);
