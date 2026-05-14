namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentListItemDto(
    int DocumentId,
    DateTime CreateDate,
    int WarehouseNo);
