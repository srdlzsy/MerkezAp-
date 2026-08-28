using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;

namespace FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed record MikroDocumentFieldCatalogDto(
    IReadOnlyCollection<MikroDocumentFieldSectionDto> Sections);

public sealed record MikroDocumentFieldSectionDto(
    string Code,
    string Title,
    string Endpoint,
    string RequestModel,
    IReadOnlyCollection<MikroDocumentFieldMappingDto> Fields);

public sealed record MikroDocumentFieldMappingDto(
    string ApiField,
    string DisplayName,
    string Scope,
    string ValueType,
    string MikroTable,
    string MikroColumn,
    bool Editable,
    string Description);

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
    string Special1,
    string Special2,
    string Special3,
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
    string? Special1,
    string? Special2,
    string? Special3,
    bool? SalesStopped,
    bool? OrderStopped,
    bool? ReceivingStopped,
    bool? IsPassive,
    bool? DiscountDisabled);

public sealed record UpdateStockCardRequest(
    string StockCode,
    StockCardPatchDto Patch,
    int CurrentUserWarehouseNo);

public sealed record StockCardWarehouseSettingsDto(
    string StockCode,
    int WarehouseNo,
    string WarehouseName,
    bool HasWarehouseDetail,
    bool HasAnyOverride,
    bool GlobalSalesStopped,
    bool GlobalOrderStopped,
    bool GlobalReceivingStopped,
    bool GlobalIsPassive,
    bool GlobalDiscountDisabled,
    bool SalesStopped,
    bool OrderStopped,
    bool ReceivingStopped,
    bool IsPassive,
    bool DiscountDisabled,
    DateTime? LastUpdatedAt);

public sealed record StockCardWarehousePatchDto(
    bool? SalesStopped,
    bool? OrderStopped,
    bool? ReceivingStopped,
    bool? IsPassive,
    bool? DiscountDisabled,
    bool ResetToGlobal);

public sealed record UpdateStockCardWarehouseSettingsRequest(
    string StockCode,
    int WarehouseNo,
    StockCardWarehousePatchDto Patch,
    int CurrentUserWarehouseNo);

public sealed record DeleteStockCardWarehouseSettingsRequest(
    string StockCode,
    int WarehouseNo,
    int CurrentUserWarehouseNo);

public sealed record WarehouseCardSearchRequest(
    string? SearchText,
    bool IncludePassive,
    int Take);

public sealed record WarehouseCardListItemDto(
    int WarehouseNo,
    string Name,
    string GroupCode,
    string RegionCode,
    byte WarehouseType,
    string City,
    string District,
    bool IsPassive,
    bool IsHidden,
    DateTime? LastUpdatedAt);

public sealed record WarehouseCardDetailDto(
    Guid WarehouseGuid,
    int WarehouseNo,
    string Name,
    string GroupCode,
    byte WarehouseType,
    byte ShipmentAutoPriceType,
    byte MovementType,
    string AccountingCode,
    string ResponsibilityCenter,
    string ProjectCode,
    string Special1,
    string Special2,
    string Special3,
    int ShipmentAppliedPriceNo,
    DateTime? LockDate,
    string Street,
    string Neighborhood,
    string Avenue,
    string Quarter,
    string ApartmentNo,
    string ApartmentUnitNo,
    string PostalCode,
    string District,
    string City,
    string Country,
    string AddressCode,
    double Latitude,
    double Longitude,
    string AuthorizedEmail,
    string PhoneCountryCode,
    string PhoneAreaCode,
    string PhoneNo1,
    string PhoneNo2,
    string FaxNo,
    bool ExcludedFromInventory,
    byte DetailTrackingType,
    string RegionCode,
    bool OutgoingEDespatchEnabled,
    bool IncomingEDespatchEnabled,
    bool IsPassive,
    bool IsHidden,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record WarehouseCardPatchDto(
    string? Name,
    string? GroupCode,
    byte? WarehouseType,
    byte? ShipmentAutoPriceType,
    byte? MovementType,
    string? AccountingCode,
    string? ResponsibilityCenter,
    string? ProjectCode,
    string? Special1,
    string? Special2,
    string? Special3,
    int? ShipmentAppliedPriceNo,
    DateTime? LockDate,
    string? Street,
    string? Neighborhood,
    string? Avenue,
    string? Quarter,
    string? ApartmentNo,
    string? ApartmentUnitNo,
    string? PostalCode,
    string? District,
    string? City,
    string? Country,
    string? AddressCode,
    double? Latitude,
    double? Longitude,
    string? AuthorizedEmail,
    string? PhoneCountryCode,
    string? PhoneAreaCode,
    string? PhoneNo1,
    string? PhoneNo2,
    string? FaxNo,
    bool? ExcludedFromInventory,
    byte? DetailTrackingType,
    string? RegionCode,
    bool? OutgoingEDespatchEnabled,
    bool? IncomingEDespatchEnabled,
    bool? IsPassive,
    bool? IsHidden,
    bool? IsLocked);

public sealed record UpdateWarehouseCardRequest(
    int WarehouseNo,
    WarehouseCardPatchDto Patch,
    int CurrentUserWarehouseNo);

public sealed record CustomerCardSearchRequest(
    string? SearchText,
    bool IncludePassive,
    int Take);

public sealed record CustomerCardListItemDto(
    string CustomerCode,
    string Title1,
    string Title2,
    string TaxOffice,
    string TaxNo,
    string GroupCode,
    string RegionCode,
    string RepresentativeCode,
    bool IsClosed,
    bool IsLocked,
    DateTime? LastUpdatedAt);

public sealed record CustomerCardDetailDto(
    Guid CustomerGuid,
    string CustomerCode,
    string Title1,
    string Title2,
    string Special1,
    string Special2,
    string Special3,
    byte MovementType,
    byte ConnectionType,
    byte PurchaseStockType,
    byte SalesStockType,
    string AccountingCode,
    string AccountingCode1,
    string AccountingCode2,
    byte CurrencyType,
    byte CurrencyType1,
    byte CurrencyType2,
    string TaxOffice,
    string TaxOfficeNo,
    string RegistryNo,
    string TaxNo,
    int SalesPriceListNo,
    byte PaymentType,
    byte PaymentDay,
    int PaymentPlanNo,
    int OptionDay,
    int InvoiceAddressNo,
    int ShippingAddressNo,
    string ParentCustomerCode,
    string SectorCode,
    string RegionCode,
    string GroupCode,
    string RepresentativeCode,
    bool IsClosed,
    bool IsLocked,
    bool EInvoiceEnabled,
    byte DefaultEInvoiceType,
    bool EDespatchEnabled,
    byte DefaultEDespatchType,
    string Website,
    string Email,
    string MobilePhone,
    int DefaultInputWarehouseNo,
    int DefaultOutputWarehouseNo,
    string KepAddress,
    string ReconciliationEmail,
    string MersisNo,
    string TaxOfficeCode,
    bool RetailCustomer,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record CustomerCardPatchDto(
    string? Title1,
    string? Title2,
    string? Special1,
    string? Special2,
    string? Special3,
    byte? MovementType,
    byte? ConnectionType,
    byte? PurchaseStockType,
    byte? SalesStockType,
    string? AccountingCode,
    string? AccountingCode1,
    string? AccountingCode2,
    byte? CurrencyType,
    byte? CurrencyType1,
    byte? CurrencyType2,
    string? TaxOffice,
    string? TaxOfficeNo,
    string? RegistryNo,
    string? TaxNo,
    int? SalesPriceListNo,
    byte? PaymentType,
    byte? PaymentDay,
    int? PaymentPlanNo,
    int? OptionDay,
    int? InvoiceAddressNo,
    int? ShippingAddressNo,
    string? ParentCustomerCode,
    string? SectorCode,
    string? RegionCode,
    string? GroupCode,
    string? RepresentativeCode,
    bool? IsClosed,
    bool? IsLocked,
    bool? EInvoiceEnabled,
    byte? DefaultEInvoiceType,
    bool? EDespatchEnabled,
    byte? DefaultEDespatchType,
    string? Website,
    string? Email,
    string? MobilePhone,
    int? DefaultInputWarehouseNo,
    int? DefaultOutputWarehouseNo,
    string? KepAddress,
    string? ReconciliationEmail,
    string? MersisNo,
    string? TaxOfficeCode,
    bool? RetailCustomer);

public sealed record UpdateCustomerCardRequest(
    string CustomerCode,
    CustomerCardPatchDto Patch,
    int CurrentUserWarehouseNo);

public sealed record StockSalesPriceDto(
    Guid PriceGuid,
    string StockCode,
    int PriceListNo,
    string PriceListName,
    int WarehouseNo,
    string WarehouseName,
    int PaymentPlanNo,
    byte UnitPointer,
    string UnitName,
    double Price,
    byte CurrencyType,
    byte ChangeReason,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record UpsertStockSalesPriceRequest(
    string StockCode,
    int WarehouseNo,
    int PriceListNo,
    int PaymentPlanNo,
    byte UnitPointer,
    double Price,
    byte CurrencyType,
    byte ChangeReason,
    int CurrentUserWarehouseNo);

public sealed record DeleteStockSalesPriceRequest(
    string StockCode,
    int WarehouseNo,
    int PriceListNo,
    int PaymentPlanNo,
    byte UnitPointer,
    int CurrentUserWarehouseNo);

public sealed record StockSalesPriceUpsertResponse(
    MikroDocumentUpdateSummary Summary,
    bool Created,
    double? PreviousPrice,
    StockSalesPriceDto SalesPrice);

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
    DateTime? GoodsAcceptanceDate,
    string DocumentNo,
    string CustomerCode,
    string CustomerTitle,
    int InputWarehouseNo,
    string InputWarehouseName,
    int OutputWarehouseNo,
    string OutputWarehouseName,
    int ShippingWarehouseNo,
    string ShippingWarehouseName,
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
    DateTime? GoodsAcceptanceDate,
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
    byte ExpenseTaxPointer,
    double ExpenseTaxAmount,
    byte TaxPointer,
    double TaxAmount,
    double NetWeight,
    double GrossWeight,
    string Description,
    string Special1,
    string Special2,
    string Special3,
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
    DateTime? GoodsAcceptanceDate,
    string? DocumentNo,
    string? CustomerCode,
    int? InputWarehouseNo,
    int? OutputWarehouseNo,
    int? ShippingWarehouseNo,
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
    DateTime? GoodsAcceptanceDate,
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
    byte? ExpenseTaxPointer,
    double? ExpenseTaxAmount,
    byte? TaxPointer,
    double? TaxAmount,
    double? NetWeight,
    double? GrossWeight,
    string? Description,
    string? Special1,
    string? Special2,
    string? Special3,
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

public sealed record DeleteStockMovementDocumentRequest(
    StockMovementDocumentLookupRequest Lookup,
    int CurrentUserWarehouseNo,
    bool HardDelete = false);

public sealed record InventoryCountDocumentLookupRequest(
    int WarehouseNo,
    int DocumentNo,
    DateTime DocumentDate);

public sealed record InventoryCountDocumentDto(
    InventoryCountDocumentHeaderDto Header,
    IReadOnlyCollection<InventoryCountDocumentLineDto> Lines);

public sealed record InventoryCountDocumentHeaderDto(
    DateTime? DocumentDate,
    DateTime CreatedAt,
    int DocumentNo,
    int WarehouseNo,
    string WarehouseName,
    string Name,
    int LineCount,
    double TotalQuantity,
    DateTime? LastUpdatedAt);

public sealed record InventoryCountDocumentLineDto(
    Guid CountGuid,
    int RowNo,
    string StockCode,
    string StockName,
    string Barcode,
    byte UnitPointer,
    string UnitName,
    double Quantity1,
    double Quantity2,
    double Quantity3,
    double Quantity4,
    double Quantity5,
    string RayonCode,
    string CorridorCode,
    string ShelfCode,
    string PartyCode,
    int LotNo,
    string SerialNo,
    string Special1,
    string Special2,
    string Special3,
    DateTime? LastUpdatedAt);

public sealed record InventoryCountHeaderPatchDto(
    DateTime? DocumentDate,
    int? WarehouseNo,
    string? Name);

public sealed record InventoryCountLinePatchDto(
    Guid CountGuid,
    int? RowNo,
    string? StockCode,
    string? Barcode,
    byte? UnitPointer,
    double? Quantity1,
    double? Quantity2,
    double? Quantity3,
    double? Quantity4,
    double? Quantity5,
    string? RayonCode,
    string? CorridorCode,
    string? ShelfCode,
    string? PartyCode,
    int? LotNo,
    string? SerialNo,
    string? Special1,
    string? Special2,
    string? Special3);

public sealed record UpdateInventoryCountDocumentRequest(
    InventoryCountDocumentLookupRequest Lookup,
    InventoryCountHeaderPatchDto? Header,
    IReadOnlyCollection<InventoryCountLinePatchDto> Lines,
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
    string Special1,
    string Special2,
    string Special3,
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
    string? Special1,
    string? Special2,
    string? Special3,
    string? SellerCode,
    string? ProjectCode,
    string? ResponsibilityCenter);

public sealed record UpdateCustomerMovementDocumentRequest(
    CustomerMovementDocumentLookupRequest Lookup,
    CustomerMovementHeaderPatchDto? Header,
    IReadOnlyCollection<CustomerMovementLinePatchDto> Lines,
    int CurrentUserWarehouseNo);

public sealed record DeleteCustomerMovementDocumentRequest(
    CustomerMovementDocumentLookupRequest Lookup,
    int CurrentUserWarehouseNo,
    bool HardDelete = false);

public sealed record CompanyOrderDocumentLookupRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    byte? OrderType,
    byte? OrderKind,
    int? WarehouseNo,
    string? CustomerCode);

public sealed record CompanyOrderDocumentDto(
    CompanyOrderDocumentHeaderDto Header,
    IReadOnlyCollection<CompanyOrderDocumentLineDto> Lines);

public sealed record CompanyOrderDocumentHeaderDto(
    string DocumentSerie,
    int DocumentOrderNo,
    byte OrderType,
    byte OrderKind,
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    DateTime? DocumentDate,
    string DocumentNo,
    int WarehouseNo,
    string WarehouseName,
    string CustomerCode,
    string CustomerTitle,
    string SellerCode,
    string Description1,
    string Description2,
    string DeliveryType,
    int AddressNo,
    byte CurrencyType,
    double CurrencyRate,
    double AlternativeCurrencyRate,
    bool CanBeCalled,
    bool IsClosed,
    string CloseReasonCode,
    string ProjectCode,
    string CustomerResponsibilityCenter,
    string StockResponsibilityCenter,
    int LineCount,
    double TotalQuantity,
    double TotalDeliveredQuantity,
    double TotalRemainingQuantity,
    double TotalAmount,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record CompanyOrderDocumentLineDto(
    Guid OrderGuid,
    int RowNo,
    DateTime? DeliveryDate,
    string StockCode,
    string StockName,
    byte UnitPointer,
    string UnitName,
    double Quantity,
    double DeliveredQuantity,
    double RemainingQuantity,
    double UnitPrice,
    double Amount,
    int PriceListNo,
    DateTime? ValidUntil,
    double ReservedQuantity,
    double DeliveredFromReservation,
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
    string Description1,
    string Description2,
    string Special1,
    string Special2,
    string Special3,
    string PackageCode,
    string PartyCode,
    int LotNo,
    string ProjectCode,
    string CustomerResponsibilityCenter,
    string StockResponsibilityCenter,
    bool CanBeCalled,
    bool IsClosed,
    string CloseReasonCode,
    DateTime? LastUpdatedAt);

public sealed record CompanyOrderHeaderPatchDto(
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    string? CustomerCode,
    int? WarehouseNo,
    string? SellerCode,
    string? Description1,
    string? Description2,
    string? DeliveryType,
    int? AddressNo,
    byte? CurrencyType,
    double? CurrencyRate,
    double? AlternativeCurrencyRate,
    bool? CanBeCalled,
    bool? IsClosed,
    string? CloseReasonCode,
    string? ProjectCode,
    string? CustomerResponsibilityCenter,
    string? StockResponsibilityCenter);

public sealed record CompanyOrderLinePatchDto(
    Guid OrderGuid,
    int? RowNo,
    DateTime? DeliveryDate,
    string? StockCode,
    byte? UnitPointer,
    double? Quantity,
    double? DeliveredQuantity,
    double? UnitPrice,
    double? Amount,
    int? PriceListNo,
    DateTime? ValidUntil,
    double? ReservedQuantity,
    double? DeliveredFromReservation,
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
    string? Description1,
    string? Description2,
    string? Special1,
    string? Special2,
    string? Special3,
    string? PackageCode,
    string? PartyCode,
    int? LotNo,
    string? ProjectCode,
    string? CustomerResponsibilityCenter,
    string? StockResponsibilityCenter,
    bool? CanBeCalled,
    bool? IsClosed,
    string? CloseReasonCode);

public sealed record UpdateCompanyOrderDocumentRequest(
    CompanyOrderDocumentLookupRequest Lookup,
    CompanyOrderHeaderPatchDto? Header,
    IReadOnlyCollection<CompanyOrderLinePatchDto> Lines,
    int CurrentUserWarehouseNo);

public sealed record DeleteCompanyOrderDocumentRequest(
    CompanyOrderDocumentLookupRequest Lookup,
    int CurrentUserWarehouseNo,
    bool HardDelete = false);

public sealed record WarehouseOrderDocumentLookupRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    int? WarehouseNo,
    int? InWarehouseNo,
    int? OutWarehouseNo);

public sealed record WarehouseOrderDocumentDto(
    WarehouseOrderDocumentHeaderDto Header,
    IReadOnlyCollection<WarehouseOrderDocumentLineDto> Lines);

public sealed record WarehouseOrderDocumentHeaderDto(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    DateTime? DocumentDate,
    string DocumentNo,
    int InWarehouseNo,
    string InWarehouseName,
    int OutWarehouseNo,
    string OutWarehouseName,
    string Description,
    bool IsClosed,
    string CloseReasonCode,
    string ProjectCode,
    string ResponsibilityCenter,
    int LineCount,
    double TotalQuantity,
    double TotalDeliveredQuantity,
    double TotalRemainingQuantity,
    double TotalAmount,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record WarehouseOrderDocumentLineDto(
    Guid OrderGuid,
    int RowNo,
    DateTime? DeliveryDate,
    string StockCode,
    string StockName,
    byte UnitPointer,
    string UnitName,
    double Quantity,
    double DeliveredQuantity,
    double RemainingQuantity,
    double UnitPrice,
    double Amount,
    string Description,
    int PriceListNo,
    DateTime? ValidUntil,
    double ReservedQuantity,
    double DeliveredFromReservation,
    string Special1,
    string Special2,
    string Special3,
    int InWarehouseNo,
    string InWarehouseName,
    int OutWarehouseNo,
    string OutWarehouseName,
    bool IsClosed,
    string CloseReasonCode,
    string PackageCode,
    string ProjectCode,
    string ResponsibilityCenter,
    DateTime? LastUpdatedAt);

public sealed record WarehouseOrderHeaderPatchDto(
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    DateTime? DocumentDate,
    string? DocumentNo,
    int? InWarehouseNo,
    int? OutWarehouseNo,
    string? Description,
    bool? IsClosed,
    string? CloseReasonCode,
    string? ProjectCode,
    string? ResponsibilityCenter);

public sealed record WarehouseOrderLinePatchDto(
    Guid OrderGuid,
    int? RowNo,
    DateTime? DeliveryDate,
    string? StockCode,
    byte? UnitPointer,
    double? Quantity,
    double? DeliveredQuantity,
    double? UnitPrice,
    double? Amount,
    string? Description,
    int? PriceListNo,
    DateTime? ValidUntil,
    double? ReservedQuantity,
    double? DeliveredFromReservation,
    string? Special1,
    string? Special2,
    string? Special3,
    int? InWarehouseNo,
    int? OutWarehouseNo,
    bool? IsClosed,
    string? CloseReasonCode,
    string? PackageCode,
    string? ProjectCode,
    string? ResponsibilityCenter);

public sealed record UpdateWarehouseOrderDocumentRequest(
    WarehouseOrderDocumentLookupRequest Lookup,
    WarehouseOrderHeaderPatchDto? Header,
    IReadOnlyCollection<WarehouseOrderLinePatchDto> Lines,
    int CurrentUserWarehouseNo);

public sealed record DeleteWarehouseOrderDocumentRequest(
    WarehouseOrderDocumentLookupRequest Lookup,
    int CurrentUserWarehouseNo,
    bool HardDelete = false);

public sealed record BanknoteTrackEditingLookupRequest(
    Guid BanknoteTrackId,
    int WarehouseNo);

public sealed record BanknoteTrackPatchDto(
    DateTime? BanknoteTrackDate,
    int? WarehouseNo,
    double? TotalAmount,
    double? DeliveryTotalAmount,
    string? Deliverer,
    string? Receiver);

public sealed record UpdateBanknoteTrackDocumentRequest(
    BanknoteTrackEditingLookupRequest Lookup,
    BanknoteTrackPatchDto Patch,
    int CurrentUserWarehouseNo);

public sealed record DeleteBanknoteTrackDocumentRequest(
    BanknoteTrackEditingLookupRequest Lookup,
    int CurrentUserWarehouseNo);

public sealed record MikroDocumentUpdateSummary(
    string Target,
    int UpdatedRowCount,
    DateTime UpdatedAt,
    short UpdateUser);

public sealed record MikroDocumentDeleteResponse(
    string Target,
    int DeletedRowCount,
    DateTime DeletedAt,
    short DeleteUser,
    string DeletionMode);

public sealed record StockCardUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    StockCardDetailDto StockCard);

public sealed record StockCardWarehouseUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    StockCardWarehouseSettingsDto WarehouseSettings);

public sealed record WarehouseCardUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    WarehouseCardDetailDto WarehouseCard);

public sealed record CustomerCardUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    CustomerCardDetailDto CustomerCard);

public sealed record StockMovementDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    StockMovementDocumentDto Document);

public sealed record InventoryCountDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    InventoryCountDocumentDto Document);

public sealed record CustomerMovementDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    CustomerMovementDocumentDto Document);

public sealed record CompanyOrderDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    CompanyOrderDocumentDto Document);

public sealed record WarehouseOrderDocumentUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    WarehouseOrderDocumentDto Document);

public sealed record BanknoteTrackUpdateResponse(
    MikroDocumentUpdateSummary Summary,
    BanknoteTrackDto BanknoteTrack);
