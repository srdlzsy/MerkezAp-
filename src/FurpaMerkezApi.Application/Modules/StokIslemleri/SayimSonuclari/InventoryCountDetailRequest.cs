namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record InventoryCountDetailRequest(
    int WarehouseNo,
    int DocumentNo,
    DateTime DocumentDate);
