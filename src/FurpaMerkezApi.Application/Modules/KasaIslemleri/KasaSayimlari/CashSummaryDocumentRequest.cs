namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;

public sealed record CashSummaryDocumentRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);
