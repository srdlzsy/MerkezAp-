namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record InventoryCountListRequest(
    int WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);
