namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;

public sealed record BanknoteTrackListRequest(
    DateTime DateToGet,
    int WarehouseNo);

public sealed record BanknoteTrackDetailRequest(
    Guid BanknoteTrackId,
    int WarehouseNo);

public sealed record BanknoteTrackDto(
    Guid BanknoteTrackId,
    int WarehouseNo,
    string WarehouseName,
    DateTime BanknoteTrackDate,
    double TotalAmount,
    double DeliveryTotalAmount,
    double DifferenceAmount,
    string Deliverer,
    string Receiver,
    DateTime CreateDate);
