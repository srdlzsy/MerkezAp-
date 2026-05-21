namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentListRequest(
    int? WarehouseNo,
    int? Take = null);
