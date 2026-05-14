namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record VirmanListItemDto(
    DateTime? DocumentDate,
    DateTime MovementCreateDate,
    DateTime? MovementDate,
    string DocumentNo,
    string DocumentSerie,
    int DocumentOrderNo,
    int WarehouseNo,
    string WarehouseName,
    byte DocumentType,
    byte MovementGenre,
    IReadOnlyCollection<byte> MovementTypes,
    string Description,
    int LineCount,
    double TotalQuantity,
    double TotalAmount);
