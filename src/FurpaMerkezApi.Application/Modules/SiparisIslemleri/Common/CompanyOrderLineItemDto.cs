namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record CompanyOrderLineItemDto(
    int LineNo,
    string StockCode,
    string StockName,
    string UnitName,
    byte UnitPointer,
    double Quantity,
    double DeliveredQuantity,
    double RemainingQuantity,
    double UnitPrice,
    double LineAmount,
    bool IsClosed,
    string Description,
    string PackageCode,
    string ProjectCode,
    Guid OrderGuid);
