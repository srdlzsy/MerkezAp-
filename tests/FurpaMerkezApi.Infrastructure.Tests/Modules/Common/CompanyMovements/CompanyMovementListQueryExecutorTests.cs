using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common.CompanyMovements;

public sealed class CompanyMovementListQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_FiltersIncomingShipmentByMovementDate()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        var movementDate = new DateTime(2026, 8, 26);
        var documentDate = new DateTime(2026, 8, 25);

        mikroDbContext.CARI_HESAPLARs.Add(new CARI_HESAPLAR
        {
            cari_Guid = Guid.NewGuid(),
            cari_create_date = movementDate,
            cari_kod = "32007602",
            cari_unvan1 = "ERASLAN AKAR",
            cari_unvan2 = "TORKU SARKUTERI"
        });
        mikroDbContext.DEPOLARs.Add(new DEPOLAR
        {
            dep_Guid = Guid.NewGuid(),
            dep_create_date = movementDate,
            dep_no = 120,
            dep_adi = "DEPO 120"
        });
        mikroDbContext.STOK_HAREKETLERIs.Add(new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_date = movementDate.AddHours(13),
            sth_tarih = movementDate,
            sth_belge_tarih = documentDate,
            sth_belge_no = "EAK2026000013460",
            sth_evraktip = 13,
            sth_tip = 0,
            sth_normal_iade = 0,
            sth_evrakno_seri = "EAK2026",
            sth_evrakno_sira = 13460,
            sth_satirno = 0,
            sth_stok_kod = "082152",
            sth_miktar = 28d,
            sth_tutar = 0d,
            sth_cari_kodu = "32007602",
            sth_giris_depo_no = 120,
            sth_cikis_depo_no = 120
        });
        await mikroDbContext.SaveChangesAsync();

        var executor = new CompanyMovementListQueryExecutor(mikroDbContext);
        var request = new CompanyMovementListRequest(120, movementDate, movementDate);

        var result = await executor.ExecuteAsync(request, CompanyMovementKind.IncomingShipment, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("EAK2026", item.DocumentSerie);
        Assert.Equal(13460, item.DocumentOrderNo);
        Assert.Equal(documentDate, item.DocumentDate);
        Assert.Equal(movementDate, item.MovementDate);
    }

    [Fact]
    public async Task ExecuteAsync_GroupsPurchaseReturnByDocumentWhenLineDescriptionsDiffer()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        var documentDate = new DateTime(2026, 8, 11);

        mikroDbContext.CARI_HESAPLARs.Add(new CARI_HESAPLAR
        {
            cari_Guid = Guid.NewGuid(),
            cari_create_date = documentDate,
            cari_kod = "32000693",
            cari_unvan1 = "ZIRVE GIDA",
            cari_unvan2 = "ULKER"
        });
        mikroDbContext.DEPOLARs.AddRange(
            new DEPOLAR
            {
                dep_Guid = Guid.NewGuid(),
                dep_create_date = documentDate,
                dep_no = 101,
                dep_adi = "KAPLIKAYA"
            },
            new DEPOLAR
            {
                dep_Guid = Guid.NewGuid(),
                dep_create_date = documentDate,
                dep_no = 1,
                dep_adi = "MERKEZ OFIS"
            });
        mikroDbContext.STOK_HAREKETLERIs.AddRange(
            CreatePurchaseReturnMovement(0, "AUTO IADE FMK101/0 S0", 5d, documentDate),
            CreatePurchaseReturnMovement(2, "AUTO IADE FMK101/0 S2", 5d, documentDate));
        await mikroDbContext.SaveChangesAsync();

        var executor = new CompanyMovementListQueryExecutor(mikroDbContext);
        var request = new CompanyMovementListRequest(101, documentDate, documentDate);

        var result = await executor.ExecuteAsync(request, CompanyMovementKind.PurchaseReturn, CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("F101", item.DocumentSerie);
        Assert.Equal(1, item.DocumentOrderNo);
        Assert.Equal(2, item.LineCount);
        Assert.Equal(10d, item.TotalQuantity);
        Assert.Equal(string.Empty, item.Description);
    }

    private static STOK_HAREKETLERI CreatePurchaseReturnMovement(
        int lineNo,
        string description,
        double quantity,
        DateTime documentDate)
    {
        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_date = documentDate.AddHours(17).AddMinutes(6).AddMilliseconds(lineNo),
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_evraktip = 1,
            sth_tip = 1,
            sth_normal_iade = 1,
            sth_evrakno_seri = "F101",
            sth_evrakno_sira = 1,
            sth_satirno = lineNo,
            sth_stok_kod = lineNo == 0 ? "016955" : "008376",
            sth_miktar = quantity,
            sth_tutar = 0d,
            sth_cari_kodu = "32000693",
            sth_giris_depo_no = 1,
            sth_cikis_depo_no = 101,
            sth_aciklama = description
        };
    }

    private static MikroDbContext CreateMikroDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"company-movement-list-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }
}
