namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record LabelPriceChangedProductRequest(
    int WarehouseNo,
    DateTime DateTimeFilter);
