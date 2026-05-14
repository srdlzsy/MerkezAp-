namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;

public sealed record CashTurnoverOverviewRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    CashTurnoverSource Source);
