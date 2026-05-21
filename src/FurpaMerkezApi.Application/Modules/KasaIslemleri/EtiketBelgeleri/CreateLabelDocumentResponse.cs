namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record CreateLabelDocumentResponse(
    int DocumentId,
    DateTime CreateDate,
    int WarehouseNo,
    int LineCount);
