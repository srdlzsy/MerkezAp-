namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record VirmanHeaderDto(
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
    int IncomingLineCount,
    int OutgoingLineCount,
    double IncomingQuantity,
    double OutgoingQuantity,
    double TotalQuantity,
    double TotalAmount);
