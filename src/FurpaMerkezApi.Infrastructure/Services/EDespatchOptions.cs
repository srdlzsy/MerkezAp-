namespace FurpaMerkezApi.Infrastructure.Services;

public sealed record EDespatchOptions(
    string EndpointUrl,
    string Username,
    string Password,
    string SupplierCustomerCode,
    string ProfileId,
    string DespatchAdviceTypeCode,
    string CountryCode,
    string CountryName);
