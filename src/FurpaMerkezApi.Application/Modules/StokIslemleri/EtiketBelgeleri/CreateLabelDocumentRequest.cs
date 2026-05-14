namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record CreateLabelDocumentRequest(
    int WarehouseNo,
    IReadOnlyCollection<CreateLabelDocumentLineRequest> Lines);

public sealed record CreateLabelDocumentLineRequest(
    string ProductCode);
