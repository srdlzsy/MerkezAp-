namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;

public sealed class WarehouseReturnProductOptions
{
    public const string SectionName = "WarehouseReturnProducts";

    public WarehouseReturnRouteOptions[] Routes { get; init; } =
    [
        new() { ProductSourceWarehouseNo = 50, ReturnWarehouseNo = 51 },
        new() { ProductSourceWarehouseNo = 53, ReturnWarehouseNo = 53 },
        new() { ProductSourceWarehouseNo = 55, ReturnWarehouseNo = 55 },
        new() { ProductSourceWarehouseNo = 56, ReturnWarehouseNo = 56 },
        new() { ProductSourceWarehouseNo = 58, ReturnWarehouseNo = 58 }
    ];
}

public sealed class WarehouseReturnRouteOptions
{
    public int ProductSourceWarehouseNo { get; init; }

    public int ReturnWarehouseNo { get; init; }
}
