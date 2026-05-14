namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;

public sealed record CashTurnoverListRequest(
    int WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    CashTurnoverSource Source);
