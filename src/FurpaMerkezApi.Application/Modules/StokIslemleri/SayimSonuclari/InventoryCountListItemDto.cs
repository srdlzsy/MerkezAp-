namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record InventoryCountListItemDto(
    DateTime? DocumentDate,
    DateTime CreatedAt,
    int DocumentNo,
    int WarehouseNo,
    string WarehouseName,
    string Name,
    int LineCount,
    double TotalQuantity);
