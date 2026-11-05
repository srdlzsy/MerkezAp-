using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanListQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_AggregatesIncomingAndOutgoingLinesByDocument()
    {
        await using var dbContext = CreateDbContext();
        var documentDate = new DateTime(2026, 11, 5);

        dbContext.DEPOLARs.Add(new DEPOLAR
        {
            dep_Guid = Guid.NewGuid(),
            dep_create_date = documentDate,
            dep_no = 50,
            dep_adi = "MERKEZ DEPO"
        });
        dbContext.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, 0, 2d, 20d, documentDate),
            CreateMovement(1, 0, 3d, 30d, documentDate),
            CreateMovement(0, 1, 5d, 50d, documentDate));
        await dbContext.SaveChangesAsync();

        var executor = new VirmanListQueryExecutor(dbContext);
        var result = await executor.ExecuteAsync(
            new VirmanListRequest(50, documentDate, documentDate),
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("V50", item.DocumentSerie);
        Assert.Equal(123, item.DocumentOrderNo);
        Assert.Equal(new byte[] { 0, 1 }, item.MovementTypes);
        Assert.Equal(3, item.LineCount);
        Assert.Equal(2, item.IncomingLineCount);
        Assert.Equal(1, item.OutgoingLineCount);
        Assert.Equal(5d, item.IncomingQuantity);
        Assert.Equal(5d, item.OutgoingQuantity);
        Assert.Equal(10d, item.TotalQuantity);
        Assert.Equal(100d, item.TotalAmount);
    }

    private static STOK_HAREKETLERI CreateMovement(
        int lineNo,
        byte movementType,
        double quantity,
        double amount,
        DateTime documentDate) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_date = documentDate.AddHours(8).AddSeconds(lineNo),
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_belge_no = "VRM-123",
            sth_evraktip = 6,
            sth_tip = movementType,
            sth_cins = 3,
            sth_normal_iade = 0,
            sth_evrakno_seri = "V50",
            sth_evrakno_sira = 123,
            sth_satirno = lineNo,
            sth_stok_kod = $"STK-{movementType}-{lineNo}",
            sth_miktar = quantity,
            sth_tutar = amount,
            sth_cikis_depo_no = 50,
            sth_giris_depo_no = 51,
            sth_aciklama = "Test virman"
        };

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"virman-list-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }
}
