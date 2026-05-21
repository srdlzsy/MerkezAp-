namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelPriceChangedProductRequest(
    int WarehouseNo,
    DateTime DateTimeFilter);
