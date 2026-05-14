namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;

public sealed record CashTurnoverDetailRequest(
    int WarehouseNo,
    DateTime BusinessDate,
    int ShiftNo,
    string CashierCode,
    CashTurnoverSource Source);
