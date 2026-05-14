namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

public sealed record CreateIssuedCompanyOrderResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime OrderDate,
    DateTime DeliveryDate,
    int WarehouseNo,
    string CustomerCode,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    string WriteConnectionName);
