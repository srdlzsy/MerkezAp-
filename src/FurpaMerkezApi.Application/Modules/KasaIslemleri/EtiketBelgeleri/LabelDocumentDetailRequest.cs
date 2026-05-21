namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentDetailRequest(
    int WarehouseNo,
    int DocumentId);
