namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record DeleteWarehouseShippingDocumentRequest(
    int SourceWarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    Guid? RequestedByUserId = null);
