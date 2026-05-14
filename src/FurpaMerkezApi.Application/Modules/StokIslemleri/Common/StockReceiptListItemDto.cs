namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record StockReceiptListItemDto(
    DateTime? DocumentDate,
    DateTime MovementCreateDate,
    DateTime? MovementDate,
    string DocumentNo,
    string DocumentSerie,
    int DocumentOrderNo,
    int WarehouseNo,
    string WarehouseName,
    string Creator,
    string Acceptor,
    string WorkOrderExpenseCode,
    byte DocumentType,
    byte MovementType,
    byte MovementGenre,
    string Description,
    int LineCount,
    double TotalQuantity,
    double TotalAmount);
