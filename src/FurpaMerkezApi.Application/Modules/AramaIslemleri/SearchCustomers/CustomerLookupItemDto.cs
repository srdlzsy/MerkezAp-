namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchCustomers;

public sealed record CustomerLookupItemDto(
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
    bool IsClosed);
