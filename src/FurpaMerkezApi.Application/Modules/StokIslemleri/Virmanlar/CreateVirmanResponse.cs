namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record CreateVirmanResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime MovementDate,
    DateTime DocumentDate,
    string DocumentNo,
    int WarehouseNo,
    IReadOnlyCollection<byte> MovementTypes,
    int LineCount,
    int IncomingLineCount,
    int OutgoingLineCount,
    double IncomingQuantity,
    double OutgoingQuantity,
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);
