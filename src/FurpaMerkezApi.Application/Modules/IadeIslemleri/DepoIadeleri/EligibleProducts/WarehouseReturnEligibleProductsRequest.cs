namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;

public sealed record WarehouseReturnEligibleProductsRequest(
    int SourceWarehouseNo,
    int? TargetWarehouseNo,
    string? Search);
