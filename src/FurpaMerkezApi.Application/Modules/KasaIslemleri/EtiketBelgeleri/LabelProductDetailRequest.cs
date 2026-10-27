namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelProductDetailRequest(
    int WarehouseNo,
    string ProductCode);
