namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

public sealed record CreateIssuedWarehouseOrderResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime OrderDate,
    DateTime DeliveryDate,
    int InWarehouseNo,
    int OutWarehouseNo,
    int LineCount,
    double TotalQuantity,
    string WriteConnectionName);
