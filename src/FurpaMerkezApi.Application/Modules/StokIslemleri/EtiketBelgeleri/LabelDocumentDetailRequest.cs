namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentDetailRequest(
    int WarehouseNo,
    int DocumentId);
