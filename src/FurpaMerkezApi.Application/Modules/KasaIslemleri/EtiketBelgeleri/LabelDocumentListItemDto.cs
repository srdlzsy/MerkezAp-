namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentListItemDto(
    int DocumentId,
    DateTime CreateDate,
    int WarehouseNo);
