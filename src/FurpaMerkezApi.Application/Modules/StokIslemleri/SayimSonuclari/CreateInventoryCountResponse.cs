namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record CreateInventoryCountResponse(
    int DocumentNo,
    DateTime DocumentDate,
    int WarehouseNo,
    string Name,
    int LineCount,
    double TotalQuantity,
    string WriteConnectionName);
