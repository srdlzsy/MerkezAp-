namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroWriteRoutingOptions
{
    public const string SectionName = "MikroWriteRouting";

    public MikroWriteMode InventoryCount { get; init; } = MikroWriteMode.Database;

    public MikroWriteMode IssuedWarehouseOrder { get; init; } = MikroWriteMode.Database;

    public MikroWriteMode IssuedCompanyOrder { get; init; } = MikroWriteMode.Database;

    public MikroWriteMode StockReceipt { get; init; } = MikroWriteMode.Database;

    public MikroWriteMode InterWarehouseShipment { get; init; } = MikroWriteMode.Database;

    public MikroWriteMode CompanyMovement { get; init; } = MikroWriteMode.Database;
}

public enum MikroWriteMode
{
    Database = 0,
    MikroApi = 1,
    DualShadow = 2
}
