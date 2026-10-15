using System.Text.Json;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Infrastructure;

public sealed class MikroWriteRoutingConfigurationTests
{
    private static readonly string[] InsertRoutes =
    [
        "InventoryCount",
        "IssuedWarehouseOrder",
        "IssuedCompanyOrder",
        "StockReceipt",
        "Virman",
        "InterWarehouseShipment",
        "WarehouseReturn",
        "CompanyMovement",
        "CompanyReceiving",
        "GreenGrocerOperations",
        "ProductDistribution",
        "GreenGrocerGoodsReceipt",
        "PosAccountingSlip",
        "AxataDynamicCensus"
    ];

    private static readonly string[] MutationRoutes =
    [
        "WarehouseOrderSentFlag",
        "CompanyOrderSentFlag",
        "WarehouseReceivingAcceptance",
        "WarehouseShippingUpdate",
        "GreenGrocerOrderDelete",
        "MicroDocumentEditing",
        "ProductDistributionStockUpdate",
        "InvoiceSendingMarkAsSent",
        "InvoiceReturnReference",
        "EDespatchMarkAsSent"
    ];

    [Fact]
    public void DefaultConfiguration_RoutesOnlyDocumentInsertsToMikroApi()
    {
        var root = FindRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "src",
            "FurpaMerkezApi.WebApi",
            "appsettings.json")));
        var routing = document.RootElement.GetProperty("MikroWriteRouting");

        Assert.All(InsertRoutes, route =>
            Assert.Equal("MikroApi", routing.GetProperty(route).GetString()));
        Assert.All(MutationRoutes, route =>
            Assert.Equal("Database", routing.GetProperty(route).GetString()));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "FurpaMerkezApi.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Repository root not found.");
    }
}
