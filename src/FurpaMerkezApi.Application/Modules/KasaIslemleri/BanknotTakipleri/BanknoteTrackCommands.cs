namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;

public sealed record CreateBanknoteTrackRequest(
    int WarehouseNo,
    DateTime BanknoteTrackDate,
    double TotalAmount,
    double DeliveryTotalAmount,
    string Deliverer,
    string Receiver);

public sealed record CreateBanknoteTrackResponse(
    Guid BanknoteTrackId,
    DateTime BanknoteTrackDate,
    int WarehouseNo,
    bool Created);
