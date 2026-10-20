using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.BakeryReport;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.BakeryReport;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.AlinanDepoSiparisleri;

public sealed class BakeryProductionReportUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_GroupsSupportedCategoriesAndExcludesInvalidRows()
    {
        await using var dbContext = CreateDbContext();
        dbContext.DEPOLARs.AddRange(
            CreateWarehouse(55, "UNLU URETIM"),
            CreateWarehouse(110, "KESTEL 1"));
        dbContext.STOKLARs.AddRange(
            CreateStock("A001", "ACMA", "33"),
            CreateStock("S001", "KAGIT KALIP", "22"),
            CreateStock("X001", "DESTEKLENMEYEN", "34"));
        dbContext.DEPOLAR_ARASI_SIPARISLERs.AddRange(
            CreateOrder("F110", 10, 0, "A001", 2, new DateTime(2026, 10, 20), 110, 55),
            CreateOrder("F110", 10, 1, "A001", 3, new DateTime(2026, 10, 20, 16, 30, 0), 110, 55),
            CreateOrder("F110", 11, 0, "S001", 4, new DateTime(2026, 10, 20), 110, 55),
            CreateOrder("F110", 12, 0, "X001", 8, new DateTime(2026, 10, 20), 110, 55),
            CreateOrder("F110", 13, 0, "S001", 9, new DateTime(2026, 10, 20), 110, 55, cancelled: true),
            CreateOrder("F110", 14, 0, "S001", 7, new DateTime(2026, 10, 21), 110, 55),
            CreateOrder("F110", 15, 0, "S001", 6, new DateTime(2026, 10, 20), 110, 62));
        await dbContext.SaveChangesAsync();

        var useCase = new GetBakeryProductionReportUseCase(dbContext);
        var result = await useCase.ExecuteAsync(
            new BakeryProductionReportRequest(new DateOnly(2026, 10, 20), 55),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 20), result.ReportDate);
        Assert.Equal(55, result.SourceWarehouseNo);
        Assert.Equal("UNLU URETIM", result.SourceWarehouseName);
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal("A001", item.ProductCode);
                Assert.Equal("33", item.TypeCode);
                Assert.Equal("Mayali Grup", item.TypeName);
                Assert.Equal(5d, item.Quantity);
            },
            item =>
            {
                Assert.Equal("S001", item.ProductCode);
                Assert.Equal("22", item.TypeCode);
                Assert.Equal("Sarf Pastane", item.TypeName);
                Assert.Equal(4d, item.Quantity);
            });
    }

    [Theory]
    [InlineData("22", "Sarf Pastane")]
    [InlineData("30", "Yas Pasta")]
    [InlineData("31", "Kuru Pasta")]
    [InlineData("32", "Baklava")]
    [InlineData("33", "Mayali Grup")]
    public void ResolveCategoryName_MapsEveryBakeryCategory(string code, string expectedName)
    {
        Assert.Equal(expectedName, GetBakeryProductionReportUseCase.ResolveCategoryName(code));
    }

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"bakery-report-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private static DEPOLAR CreateWarehouse(int warehouseNo, string warehouseName) =>
        new()
        {
            dep_Guid = Guid.NewGuid(),
            dep_no = warehouseNo,
            dep_adi = warehouseName,
            dep_create_date = DateTime.UtcNow
        };

    private static STOKLAR CreateStock(string stockCode, string stockName, string modelCode) =>
        new()
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = stockCode,
            sto_isim = stockName,
            sto_model_kodu = modelCode,
            sto_create_date = DateTime.UtcNow
        };

    private static DEPOLAR_ARASI_SIPARISLER CreateOrder(
        string documentSerie,
        int documentOrderNo,
        int lineNo,
        string stockCode,
        double quantity,
        DateTime documentDate,
        int inWarehouseNo,
        int outWarehouseNo,
        bool cancelled = false) =>
        new()
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_create_date = DateTime.UtcNow,
            ssip_iptal = cancelled,
            ssip_tarih = documentDate,
            ssip_evrakno_seri = documentSerie,
            ssip_evrakno_sira = documentOrderNo,
            ssip_satirno = lineNo,
            ssip_stok_kod = stockCode,
            ssip_miktar = quantity,
            ssip_girdepo = inWarehouseNo,
            ssip_cikdepo = outWarehouseNo
        };
}
