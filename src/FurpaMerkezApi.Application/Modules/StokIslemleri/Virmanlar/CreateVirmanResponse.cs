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
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);
