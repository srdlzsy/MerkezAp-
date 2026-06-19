namespace FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed record StockCardSearchRequest(
    string? SearchText,
    bool IncludePassive,
    int Take);

public sealed record StockCardListItemDto(
    string StockCode,
    string Name,
    string ShortName,
    string SupplierCode,
    string Unit1Name,
    string MainGroupCode,
    string SubGroupCode,
    string CategoryCode,
    bool IsPassive,
    DateTime? LastUpdatedAt);

public sealed record StockCardDetailDto(
    string StockCode,
    string Name,
    string ShortName,
    string ForeignName,
    string SupplierCode,
    byte StockType,
    byte CurrencyType,
    byte TrackingType,
    string Unit1Name,
    string Unit2Name,
    string Unit3Name,
    string Unit4Name,
    byte RetailTaxPointer,
    byte WholesaleTaxPointer,
    string CategoryCode,
    string MainGroupCode,
    string SubGroupCode,
    string BrandCode,
    string SectorCode,
    string RayonCode,
    string ManufacturerCode,
    string ResponsibilityCode,
    string ShelfCode,
    bool SalesStopped,
    bool OrderStopped,
    bool ReceivingStopped,
    bool IsPassive,
    bool DiscountDisabled,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record StockCardPatchDto(
    string? Name,
    string? ShortName,
    string? ForeignName,
    string? SupplierCode,
    byte? StockType,
    byte? CurrencyType,
    byte? TrackingType,
    string? Unit1Name,
    string? Unit2Name,
    string? Unit3Name,
    string? Unit4Name,
    byte? RetailTaxPointer,
    byte? WholesaleTaxPointer,
    string? CategoryCode,
    string? MainGroupCode,
    string? SubGroupCode,
    string? BrandCode,
    string? SectorCode,
    string? RayonCode,
    string? ManufacturerCode,
    string? ResponsibilityCode,
    string? ShelfCode,
    bool? SalesStopped,
    bool? OrderStopped,
    bool? ReceivingStopped,
    bool? IsPassive,
    bool? DiscountDisabled);

public sealed record UpdateStockCardRequest(
    string StockCode,
    StockCardPatchDto Patch,
    int CurrentUserWarehouseNo);

public sealed record StockMovementDocumentLookupRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    byte? DocumentType,
    byte? MovementType,
    byte? MovementKind,
    byte? NormalReturn,
    int? WarehouseNo);

public sealed record StockMovementDocumentDto(
    StockMovementDocumentHeaderDto Header,
    IReadOnlyCollection<StockMovementDocumentLineDto> Lines);

public sealed record StockMovementDocumentHeaderDto(
    string DocumentSerie,
    int DocumentOrderNo,
    byte DocumentType,
    IReadOnlyCollection<byte> MovementTypes,
    byte MovementKind,
    byte NormalReturn,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string DocumentNo,
    string CustomerCode,
    string CustomerTitle,
    int InputWarehouseNo,
    string InputWarehouseName,
    int OutputWarehouseNo,
    string OutputWarehouseName,
    string Description,
    string MovementGroupCode1,
    string MovementGroupCode2,
    string MovementGroupCode3,
    string CustomerResponsibilityCenter,
    string StockResponsibilityCenter,
    string ProjectCode,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record StockMovementDocumentLineDto(
    Guid MovementGuid,
    int RowNo,
    string StockCode,
    string StockName,
    byte UnitPointer,
    string UnitName,
    double Quantity,
    double SecondaryQuantity,
    double UnitPrice,
    double Amount,
    double Discount1,
    double Discount2,
    double Discount3,
    double Discount4,
    double Discount5,
    double Discount6,
    double Expense1,
    double Expense2,
    double Expense3,
    double Expense4,
    byte TaxPointer,
    double TaxAmount,
    double NetWeight,
    double GrossWeight,
    string Description,
    string PartyCode,
    int LotNo,
    string ProjectCode,
    string CustomerResponsibilityCenter,
    string StockResponsibilityCenter,
    int InputWarehouseNo,
    int OutputWarehouseNo,
    DateTime? LastUpdatedAt);

public sealed record StockMovementHeaderPatchDto(
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? CustomerCode,
    int? InputWarehouseNo,
    int? OutputWarehouseNo,
    string? Description,
    string? MovementGroupCode1,
    string? MovementGroupCode2,
    string? MovementGroupCode3,
    string? CustomerResponsibilityCenter,
    string? StockResponsibilityCenter,
    string? ProjectCode);

public sealed record StockMovementLinePatchDto(
    Guid MovementGuid,
    int? RowNo,
    string? StockCode,
    byte? UnitPointer,
    double? Quantity,
    double? SecondaryQuantity,
    double? Amount,
    double? Discount1,
    double? Discount2,
    double? Discount3,
    double? Discount4,
    double? Discount5,
    double? Discount6,
    double? Expense1,
    double? Expense2,
    double? Expense3,
    double? Expense4,
    byte? TaxPointer,
    double? TaxAmount,
    double? NetWeight,
    double? GrossWeight,
    string? Description,
    string? PartyCode,
    int? LotNo,
    string? ProjectCode,
    string? CustomerResponsibilityCenter,
    string? StockResponsibilityCenter,
    int? InputWarehouseNo,
    int? OutputWarehouseNo);

public sealed record UpdateStockMovementDocumentRequest(
    StockMovementDocumentLookupRequest Lookup,
    StockMovementHeaderPatchDto? Header,
    IReadOnlyCollection<StockMovementLinePatchDto> Lines,
    int CurrentUserWarehouseNo);

public sealed record CustomerMovementDocumentLookupRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    byte? DocumentType,
    byte? MovementType,
    byte? MovementKind,
    byte? NormalReturn,
    string? CustomerCode);

public sealed record CustomerMovementDocumentDto(
    CustomerMovementDocumentHeaderDto Header,
    IReadOnlyCollection<CustomerMovementDocumentLineDto> Lines);

public sealed record CustomerMovementDocumentHeaderDto(
    string DocumentSerie,
    int DocumentOrderNo,
    byte DocumentType,
    IReadOnlyCollection<byte> MovementTypes,
    byte MovementKind,
    byte NormalReturn,
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string DocumentNo,
    string CustomerCode,
    string TurnoverCustomerCode,
    string CustomerTitle,
    string Description,
    string SellerCode,
    string ProjectCode,
    string ResponsibilityCenter,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    double TotalSubAmount,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record CustomerMovementDocumentLineDto(
    Guid MovementGuid,
    int RowNo,
    string CustomerCode,
    string TurnoverCustomerCode,
    string CustomerTitle,
    byte MovementType,
    byte MovementKind,
    byte NormalReturn,
    double Quantity,
    double Amount,
    double SubAmount,
    int DueDay,
    double Discount1,
    double Discount2,
    double Discount3,
    double Discount4,
    double Discount5,
    double Discount6,
    double Expense1,
    double Expense2,
    double Expense3,
    double Expense4,
    double Tax1,
    double Tax2,
    double Tax3,
    double Tax4,
    double Tax5,
    string Description,
    string SellerCode,
    string ProjectCode,
    string ResponsibilityCenter,
    DateTime? LastUpdatedAt);

public sealed record CustomerMovementHeaderPatchDto(
    DateTime? MovementDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? CustomerCode,
    string? TurnoverCustomerCode,
    string? Description,
    string? SellerCode,
    string? ProjectCode,
    string? ResponsibilityCenter);

public sealed record CustomerMovementLinePatchDto(
    Guid MovementGuid,
    int? RowNo,
    string? CustomerCode,
    string? TurnoverCustomerCode,
    double? Quantity,
    double? Amount,
    double? SubAmount,
    int? DueDay,
    double? Discount1,
    double? Discount2,
    double? Discount3,
    double? Discount4,
    double? Discount5,
    double? Discount6,
    double? Expense1,
    double? Expense2,
    double? Expense3,
    double? Expense4,
    double? Tax1,
    double? Tax2,
    double? Tax3,
    double? Tax4,
    double? Tax5,
    string? Description,
    string? SellerCode,
    string? ProjectCode,
    string? ResponsibilityCenter);

public sealed record UpdateCustomerMovementDocumentRequest(
    CustomerMovementDocumentLookupRequest Lookup,
    CustomerMovementHeaderPatchDto? Header,
    IReadOnlyCollection<CustomerMovementLinePatchDto> Lines,
    int CurrentUserWarehouseNo);

public sealed record MikroDocumentUpdateSummary(
    string Target,
    int UpdatedRowCount,
    DateTime UpdatedAt,
    short UpdateUser);

public sealed record StockCardUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    StockCardDetailDto StockCard);

public sealed record StockMovementDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    StockMovementDocumentDto Document);

public sealed record CustomerMovementDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    CustomerMovementDocumentDto Document);
