namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;

public sealed record CashSummaryDateRequest(
    DateTime SummaryDate,
    int? WarehouseNo = null);
