namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelTagListRequest(
    int WarehouseNo,
    DateTime DateToGet);
