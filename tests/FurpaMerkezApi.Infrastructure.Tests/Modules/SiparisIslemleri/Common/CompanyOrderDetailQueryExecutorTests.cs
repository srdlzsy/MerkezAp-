using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.Common;

public sealed class CompanyOrderDetailQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SelectsBarcodeMatchingOrderUnitPointer()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SIPARISLERs.Add(CreateOrderLine(unitPointer: 2));
        dbContext.BARKOD_TANIMLARIs.AddRange(
            CreateBarcode("8690000000001", unitPointer: 1, isMaster: true),
            CreateBarcode("8690000000002", unitPointer: 2, isMaster: false));
        await dbContext.SaveChangesAsync();

        var result = await new CompanyOrderDetailQueryExecutor(dbContext).ExecuteAsync(
            new CompanyOrderDetailRequest(50, "A", 1930),
            CompanyOrderListDirection.Issued,
            CancellationToken.None);

        Assert.Equal("8690000000002", Assert.Single(result.Items).Barcode);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToPrimaryUnitBarcodeWhenOrderUnitHasNoBarcode()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SIPARISLERs.Add(CreateOrderLine(unitPointer: 2));
        dbContext.BARKOD_TANIMLARIs.Add(CreateBarcode("8690000000001", unitPointer: 1, isMaster: true));
        await dbContext.SaveChangesAsync();

        var result = await new CompanyOrderDetailQueryExecutor(dbContext).ExecuteAsync(
            new CompanyOrderDetailRequest(50, "A", 1930),
            CompanyOrderListDirection.Issued,
            CancellationToken.None);

        Assert.Equal("8690000000001", Assert.Single(result.Items).Barcode);
    }

    private static SIPARISLER CreateOrderLine(byte unitPointer) =>
        new()
        {
            sip_Guid = Guid.NewGuid(),
            sip_create_date = new DateTime(2026, 11, 5),
            sip_lastup_date = new DateTime(2026, 11, 5),
            sip_iptal = false,
            sip_tarih = new DateTime(2026, 11, 5),
            sip_teslim_tarih = new DateTime(2026, 11, 6),
            sip_tip = 1,
            sip_cins = 0,
            sip_evrakno_seri = "A",
            sip_evrakno_sira = 1930,
            sip_satirno = 0,
            sip_musteri_kod = "12001970",
            sip_stok_kod = "015650",
            sip_birim_pntr = unitPointer,
            sip_miktar = 3,
            sip_teslim_miktar = 0,
            sip_b_fiyat = 100,
            sip_tutar = 300,
            sip_depono = 50,
            sip_kapat_fl = false
        };

    private static BARKOD_TANIMLARI CreateBarcode(string barcode, byte unitPointer, bool isMaster) =>
        new()
        {
            bar_Guid = Guid.NewGuid(),
            bar_create_date = new DateTime(2026, 11, 5),
            bar_iptal = false,
            bar_kodu = barcode,
            bar_stokkodu = "015650",
            bar_birimpntr = unitPointer,
            bar_master = isMaster
        };

    private static MikroDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"company-order-detail-{Guid.NewGuid():N}")
            .Options);
}
