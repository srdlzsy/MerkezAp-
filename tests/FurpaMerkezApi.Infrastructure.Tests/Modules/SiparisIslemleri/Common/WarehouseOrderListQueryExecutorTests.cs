using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.Common;

public sealed class WarehouseOrderListQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_AxataScanIncludesBackdatedOrderCreatedInsideWindow()
    {
        await using var dbContext = CreateDbContext();
        var scanDate = new DateTime(2026, 10, 23);
        AddWarehouses(dbContext, scanDate);
        dbContext.DEPOLAR_ARASI_SIPARISLERs.Add(
            CreateOrderLine(
                documentDate: scanDate.AddDays(-1),
                createDate: scanDate.AddHours(8),
                lineNo: 0,
                sentFlag: "FROU"));
        await dbContext.SaveChangesAsync();

        var result = await new WarehouseOrderListQueryExecutor(dbContext).ExecuteAsync(
            new WarehouseOrderListRequest(50, scanDate, scanDate, ExcludeAxataSent: true),
            WarehouseOrderListDirection.Received,
            CancellationToken.None);

        var document = Assert.Single(result);
        Assert.Equal("F148", document.DocumentSerie);
        Assert.Equal(5410, document.DocumentOrderNo);
    }

    [Fact]
    public async Task ExecuteAsync_AxataScanKeepsAllLinesWhenOnlySomeLinesAreMarkedSent()
    {
        await using var dbContext = CreateDbContext();
        var scanDate = new DateTime(2026, 10, 23);
        AddWarehouses(dbContext, scanDate);
        dbContext.DEPOLAR_ARASI_SIPARISLERs.AddRange(
            CreateOrderLine(scanDate, scanDate, 0, "1"),
            CreateOrderLine(scanDate, scanDate, 1, "FROU"));
        await dbContext.SaveChangesAsync();

        var result = await new WarehouseOrderListQueryExecutor(dbContext).ExecuteAsync(
            new WarehouseOrderListRequest(50, scanDate, scanDate, ExcludeAxataSent: true),
            WarehouseOrderListDirection.Received,
            CancellationToken.None);

        var document = Assert.Single(result);
        Assert.Equal(2, document.LineCount);
        Assert.Equal(3d, document.TotalQuantity);
    }

    [Fact]
    public async Task ExecuteAsync_AxataScanExcludesFullyMarkedAndCancelledOrders()
    {
        await using var dbContext = CreateDbContext();
        var scanDate = new DateTime(2026, 10, 23);
        AddWarehouses(dbContext, scanDate);
        dbContext.DEPOLAR_ARASI_SIPARISLERs.AddRange(
            CreateOrderLine(scanDate, scanDate, 0, "1"),
            CreateOrderLine(scanDate, scanDate, 1, "1"),
            CreateOrderLine(scanDate, scanDate, 2, "FROU", cancelled: true));
        await dbContext.SaveChangesAsync();

        var result = await new WarehouseOrderListQueryExecutor(dbContext).ExecuteAsync(
            new WarehouseOrderListRequest(50, scanDate, scanDate, ExcludeAxataSent: true),
            WarehouseOrderListDirection.Received,
            CancellationToken.None);

        Assert.Empty(result);
    }

    private static DEPOLAR_ARASI_SIPARISLER CreateOrderLine(
        DateTime documentDate,
        DateTime createDate,
        int lineNo,
        string sentFlag,
        bool cancelled = false) =>
        new()
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_create_date = createDate,
            ssip_lastup_date = createDate,
            ssip_iptal = cancelled,
            ssip_tarih = documentDate,
            ssip_teslim_tarih = documentDate.AddDays(1),
            ssip_evrakno_seri = "F148",
            ssip_evrakno_sira = 5410,
            ssip_belgeno = "F148.5410",
            ssip_satirno = lineNo,
            ssip_stok_kod = $"STK{lineNo}",
            ssip_miktar = lineNo + 1,
            ssip_tutar = 10,
            ssip_girdepo = 148,
            ssip_cikdepo = 50,
            ssip_special1 = sentFlag
        };

    private static void AddWarehouses(MikroDbContext dbContext, DateTime createDate)
    {
        dbContext.DEPOLARs.AddRange(
            new DEPOLAR
            {
                dep_Guid = Guid.NewGuid(),
                dep_create_date = createDate,
                dep_no = 50,
                dep_adi = "MERKEZ DEPO"
            },
            new DEPOLAR
            {
                dep_Guid = Guid.NewGuid(),
                dep_create_date = createDate,
                dep_no = 148,
                dep_adi = "SUBE 148"
            });
    }

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"warehouse-order-list-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }
}
