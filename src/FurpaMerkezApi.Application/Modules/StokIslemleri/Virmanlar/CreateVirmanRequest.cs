namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record CreateVirmanRequest(
    int WarehouseNo,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? Description,
    IReadOnlyCollection<CreateVirmanLineRequest> Lines);

public sealed record CreateVirmanLineRequest(
    string StockCode,
    byte MovementType,
    double Quantity,
    int UnitPointer = 1,
    string? Description = null,
    string? PartyCode = null,
    int LotNo = 0,
    string? ProjectCode = null);
