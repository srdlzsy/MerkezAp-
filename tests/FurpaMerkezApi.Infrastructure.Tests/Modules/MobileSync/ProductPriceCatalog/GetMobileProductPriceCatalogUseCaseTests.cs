using System.Data;
using FurpaMerkezApi.Infrastructure.Modules.MobileSync.ProductPriceCatalog;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.MobileSync.ProductPriceCatalog;

public sealed class GetMobileProductPriceCatalogUseCaseTests
{
    [Fact]
    public void ReadItem_SeparatesPackageMultiplierFromMatchedBarcodeMultiplier()
    {
        var table = new DataTable();
        table.Columns.Add("WarehouseNo", typeof(int));
        table.Columns.Add("Barcode", typeof(string));
        table.Columns.Add("LookupSource", typeof(string));
        table.Columns.Add("StockCode", typeof(string));
        table.Columns.Add("StockName", typeof(string));
        table.Columns.Add("Price", typeof(double));
        table.Columns.Add("PriceTypeCode", typeof(int));
        table.Columns.Add("UnitPointer", typeof(int));
        table.Columns.Add("UnitName", typeof(string));
        table.Columns.Add("MatchedUnitMultiplier", typeof(double));
        table.Columns.Add("PrimaryUnitMultiplier", typeof(double));
        table.Columns.Add("SecondaryUnitName", typeof(string));
        table.Columns.Add("SecondaryUnitMultiplier", typeof(double));
        table.Columns.Add("SalesBlockCode", typeof(int));
        table.Columns.Add("OrderBlockCode", typeof(int));
        table.Columns.Add("GoodsAcceptanceBlockCode", typeof(int));
        table.Columns.Add("IsPassive", typeof(bool));
        table.Columns.Add("IsDeleted", typeof(bool));
        table.Columns.Add("ProductManagerCode", typeof(string));
        table.Columns.Add("UpdatedAt", typeof(DateTime));
        table.Rows.Add(
            110,
            "8681324000796",
            "barcode",
            "034484",
            "SUTAS AYRAN 250ML GENC SISE",
            10d,
            1,
            1,
            "ADET",
            1d,
            1d,
            "KOLI",
            -4d,
            0,
            0,
            0,
            false,
            false,
            string.Empty,
            new DateTime(2026, 10, 9));

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var item = GetMobileProductPriceCatalogUseCase.ReadItem(reader);

        Assert.Equal(4d, item.UnitMultiplier);
        Assert.Equal(1d, item.MatchedUnitMultiplier);
        Assert.Equal(4d, item.SecondaryUnitMultiplier);
    }
}
