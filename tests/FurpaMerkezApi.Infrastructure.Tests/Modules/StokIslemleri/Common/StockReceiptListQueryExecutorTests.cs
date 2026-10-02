using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Common;

public sealed class StockReceiptListQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_GroupsLinesByDocumentWhenCreateDatesAndDescriptionsDiffer()
    {
        await using var dbContext = CreateDbContext();
        var documentDate = new DateTime(2026, 9, 30);

        dbContext.DEPOLARs.Add(new DEPOLAR
        {
            dep_Guid = Guid.NewGuid(),
            dep_create_date = documentDate,
            dep_no = 120,
            dep_adi = "YUNUSELI 1"
        });
        dbContext.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, "008368", 2d, 18.3618d, "Ilk satir", documentDate, 0),
            CreateMovement(1, "008373", 3d, 24.4824d, "Ikinci satir", documentDate, 5));
        await dbContext.SaveChangesAsync();

        var executor = new StockReceiptListQueryExecutor(dbContext);
        var result = await executor.ExecuteAsync(
            new StockReceiptListRequest(120, documentDate, documentDate),
            StockReceiptKind.OutageReceipt,
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("F120", item.DocumentSerie);
        Assert.Equal(2687, item.DocumentOrderNo);
        Assert.Equal(documentDate.AddHours(9).AddMinutes(50), item.MovementCreateDate);
        Assert.Equal(2, item.LineCount);
        Assert.Equal(5d, item.TotalQuantity);
        Assert.Equal(42.8442d, item.TotalAmount, 4);
        Assert.Equal(string.Empty, item.Description);
    }

    private static STOK_HAREKETLERI CreateMovement(
        int rowNo,
        string stockCode,
        double quantity,
        double amount,
        string description,
        DateTime documentDate,
        int createSecond)
    {
        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_date = documentDate.AddHours(9).AddMinutes(50).AddSeconds(createSecond),
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_belge_no = string.Empty,
            sth_evraktip = 0,
            sth_tip = 1,
            sth_cins = 4,
            sth_normal_iade = 0,
            sth_evrakno_seri = "F120",
            sth_evrakno_sira = 2687,
            sth_satirno = rowNo,
            sth_stok_kod = stockCode,
            sth_miktar = quantity,
            sth_tutar = amount,
            sth_cikis_depo_no = 120,
            sth_HareketGrupKodu1 = "mustafa soydan",
            sth_HareketGrupKodu2 = "Zeynep Akdag",
            sth_isemri_gider_kodu = "0032",
            sth_aciklama = description
        };
    }

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"stock-receipt-list-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }
}
