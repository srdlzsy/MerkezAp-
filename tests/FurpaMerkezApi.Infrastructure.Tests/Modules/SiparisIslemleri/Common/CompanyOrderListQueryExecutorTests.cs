using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.Common;

public sealed class CompanyOrderListQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_AxataScanIncludesBackdatedOrderCreatedInsideWindow()
    {
        await using var dbContext = CreateDbContext();
        var scanDate = new DateTime(2026, 10, 26);
        dbContext.SIPARISLERs.Add(CreateOrderLine(
            documentDate: scanDate.AddDays(-10),
            createDate: scanDate.AddHours(8),
            sentFlag: string.Empty));
        await dbContext.SaveChangesAsync();

        var result = await new CompanyOrderListQueryExecutor(dbContext).ExecuteAsync(
            new CompanyOrderListRequest(
                50,
                scanDate,
                scanDate,
                OnlyOpen: true,
                ExcludeAxataSent: true),
            CompanyOrderListDirection.Received,
            CancellationToken.None);

        var document = Assert.Single(result);
        Assert.Equal("A", document.DocumentSerie);
        Assert.Equal(1930, document.DocumentOrderNo);
    }

    [Fact]
    public async Task ExecuteAsync_NormalListDoesNotExpandDateRangeUsingCreateDate()
    {
        await using var dbContext = CreateDbContext();
        var scanDate = new DateTime(2026, 10, 26);
        dbContext.SIPARISLERs.Add(CreateOrderLine(
            documentDate: scanDate.AddDays(-10),
            createDate: scanDate.AddHours(8),
            sentFlag: string.Empty));
        await dbContext.SaveChangesAsync();

        var result = await new CompanyOrderListQueryExecutor(dbContext).ExecuteAsync(
            new CompanyOrderListRequest(50, scanDate, scanDate),
            CompanyOrderListDirection.Received,
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_AxataScanExcludesOrderAlreadyMarkedAsSent()
    {
        await using var dbContext = CreateDbContext();
        var scanDate = new DateTime(2026, 10, 26);
        dbContext.SIPARISLERs.Add(CreateOrderLine(
            documentDate: scanDate,
            createDate: scanDate.AddHours(8),
            sentFlag: "1"));
        await dbContext.SaveChangesAsync();

        var result = await new CompanyOrderListQueryExecutor(dbContext).ExecuteAsync(
            new CompanyOrderListRequest(
                50,
                scanDate,
                scanDate,
                OnlyOpen: true,
                ExcludeAxataSent: true),
            CompanyOrderListDirection.Received,
            CancellationToken.None);

        Assert.Empty(result);
    }

    private static SIPARISLER CreateOrderLine(
        DateTime documentDate,
        DateTime createDate,
        string sentFlag) =>
        new()
        {
            sip_Guid = Guid.NewGuid(),
            sip_create_date = createDate,
            sip_lastup_date = createDate,
            sip_iptal = false,
            sip_tarih = documentDate,
            sip_teslim_tarih = documentDate.AddDays(1),
            sip_tip = 0,
            sip_cins = 0,
            sip_evrakno_seri = "A",
            sip_evrakno_sira = 1930,
            sip_satirno = 0,
            sip_belgeno = "A.1930",
            sip_musteri_kod = "0012120063",
            sip_stok_kod = "007921",
            sip_miktar = 757,
            sip_teslim_miktar = 0,
            sip_tutar = 10,
            sip_depono = 50,
            sip_kapat_fl = false,
            sip_special1 = sentFlag
        };

    private static MikroDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"company-order-list-{Guid.NewGuid():N}")
            .Options);
}
