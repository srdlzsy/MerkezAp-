namespace FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

public sealed record CompanyMovementLineItemDto(
    int LineNo,
    string StockCode,
    string StockName,
    string UnitName,
    byte UnitPointer,
    double Quantity,
    double SecondaryQuantity,
    double UnitPrice,
    double LineAmount,
    double DiscountAmount,
    double ExpenseAmount,
    double TaxAmount,
    double NetWeight,
    double GrossWeight,
    string Description,
    string PartyCode,
    int LotNo,
    string ProjectCode,
    Guid? OrderGuid);
