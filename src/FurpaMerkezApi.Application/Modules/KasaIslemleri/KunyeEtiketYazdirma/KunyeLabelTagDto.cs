namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed record KunyeLabelTagDto(
    int BranchNo,
    string BranchName,
    string ProductionCity,
    string StockCode,
    string StockName,
    double SalesPrice,
    string ProductionDistrict,
    string ProductName,
    string GoodsType,
    string GoodsGenus,
    double Quantity,
    string TakenTag,
    string Buyer,
    DateTime ProductionDate,
    double BuyingPrice,
    DateTime ShippingDate,
    string Manufacturer,
    string ProductUnit);
