using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common.CompanyMovements;

public sealed class CompanyMovementDetailQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsDelivererAndReceiverForPurchaseReturn()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        var documentDate = new DateTime(2026, 9, 28);

        mikroDbContext.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, documentDate, null, null),
            CreateMovement(1, documentDate, "Teslim Eden Kisi", "Teslim Alan Kisi"));
        await mikroDbContext.SaveChangesAsync();

        var executor = new CompanyMovementDetailQueryExecutor(mikroDbContext);
        var request = new CompanyMovementDetailRequest(155, "F155", 4502);

        var result = await executor.ExecuteAsync(
            request,
            CompanyMovementKind.PurchaseReturn,
            CancellationToken.None);

        Assert.Equal("Teslim Eden Kisi", result.Header.Deliverer);
        Assert.Equal("Teslim Alan Kisi", result.Header.Receiver);
    }

    [Fact]
    public async Task ExecuteAsync_TreatsPartiallyMarkedEDespatchRowsAsOneDocument()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        var documentDate = new DateTime(2026, 10, 10);
        var documentNo = "FRM2026600137723";
        var uuid = "acf2cfbb-1395-4d4a-b54b-9e8459c15873";
        var markedRow = CreateMovement(0, documentDate, "Teslim Eden Kisi", "Teslim Alan Kisi");
        markedRow.sth_kilitli = true;
        markedRow.sth_belge_no = documentNo;
        markedRow.sth_aciklama = uuid;

        mikroDbContext.STOK_HAREKETLERIs.AddRange(
            markedRow,
            CreateMovement(1, documentDate, "Teslim Eden Kisi", "Teslim Alan Kisi"));
        await mikroDbContext.SaveChangesAsync();

        var executor = new CompanyMovementDetailQueryExecutor(mikroDbContext);
        var result = await executor.ExecuteAsync(
            new CompanyMovementDetailRequest(155, "F155", 4502),
            CompanyMovementKind.PurchaseReturn,
            CancellationToken.None);

        Assert.Equal(2, result.Header.LineCount);
        Assert.Equal(documentNo, result.Header.DocumentNo);
        Assert.Equal(uuid, result.Header.Description);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsItemsInLineNumberOrder()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        var documentDate = new DateTime(2026, 10, 10);

        mikroDbContext.STOK_HAREKETLERIs.AddRange(
            CreateMovement(2, documentDate, null, null),
            CreateMovement(0, documentDate, null, null),
            CreateMovement(1, documentDate, null, null));
        await mikroDbContext.SaveChangesAsync();

        var executor = new CompanyMovementDetailQueryExecutor(mikroDbContext);
        var result = await executor.ExecuteAsync(
            new CompanyMovementDetailRequest(155, "F155", 4502),
            CompanyMovementKind.PurchaseReturn,
            CancellationToken.None);

        Assert.Equal(new[] { 0, 1, 2 }, result.Items.Select(item => item.LineNo).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ReversesItemsForIncomingCompanyReceiving()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        var documentDate = new DateTime(2026, 10, 10);
        var rows = new[]
        {
            CreateMovement(0, documentDate, null, null),
            CreateMovement(1, documentDate, null, null),
            CreateMovement(2, documentDate, null, null)
        };

        foreach (var row in rows)
        {
            row.sth_evraktip = 13;
            row.sth_tip = 0;
            row.sth_normal_iade = 0;
            row.sth_giris_depo_no = 101;
        }

        mikroDbContext.STOK_HAREKETLERIs.AddRange(rows);
        await mikroDbContext.SaveChangesAsync();

        var executor = new CompanyMovementDetailQueryExecutor(mikroDbContext);
        var result = await executor.ExecuteAsync(
            new CompanyMovementDetailRequest(101, "F155", 4502),
            CompanyMovementKind.IncomingShipment,
            CancellationToken.None);

        Assert.Equal(new[] { 2, 1, 0 }, result.Items.Select(item => item.LineNo).ToArray());
    }

    private static STOK_HAREKETLERI CreateMovement(
        int lineNo,
        DateTime documentDate,
        string? deliverer,
        string? receiver) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_date = documentDate.AddHours(8).AddMinutes(lineNo),
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_evraktip = 1,
            sth_tip = 1,
            sth_normal_iade = 1,
            sth_evrakno_seri = "F155",
            sth_evrakno_sira = 4502,
            sth_satirno = lineNo,
            sth_stok_kod = $"STOK-{lineNo}",
            sth_miktar = 1d,
            sth_tutar = 10d,
            sth_cari_kodu = "32000001",
            sth_giris_depo_no = 1,
            sth_cikis_depo_no = 155,
            sth_HareketGrupKodu2 = deliverer,
            sth_HareketGrupKodu3 = receiver
        };

    private static MikroDbContext CreateMikroDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"company-movement-detail-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }
}
