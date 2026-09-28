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
