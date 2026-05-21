namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record CreateLabelDocumentRequest(
    int WarehouseNo,
    IReadOnlyCollection<CreateLabelDocumentLineRequest> Lines);

public sealed record CreateLabelDocumentLineRequest(
    string ProductCode);
