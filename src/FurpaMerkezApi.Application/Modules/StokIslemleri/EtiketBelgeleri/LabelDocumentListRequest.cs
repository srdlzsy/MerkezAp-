namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentListRequest(
    int? WarehouseNo,
    int? Take = null);
