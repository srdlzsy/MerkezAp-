namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record CreateLabelDocumentResponse(
    int DocumentId,
    DateTime CreateDate,
    int WarehouseNo,
    int LineCount);
