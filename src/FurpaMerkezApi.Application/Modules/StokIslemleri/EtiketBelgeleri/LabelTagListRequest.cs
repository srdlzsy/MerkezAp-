namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record LabelTagListRequest(
    int WarehouseNo,
    DateTime DateToGet);
