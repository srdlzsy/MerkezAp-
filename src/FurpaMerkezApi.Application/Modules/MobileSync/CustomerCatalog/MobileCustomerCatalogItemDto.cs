namespace FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;

public sealed record MobileCustomerCatalogItemDto(
    string CustomerCode,
    string CustomerName,
    string CustomerTitle,
    string CustomerDisplayName,
    string TaxNumber,
    string RepresentativeCode,
    string RepresentativeName,
    int? InvoiceAddressNo,
    int? ShippingAddressNo,
    bool IsLocked,
    bool IsClosed,
    bool IsDeleted,
    DateTime UpdatedAt);
