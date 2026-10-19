using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class WarehouseShippingListQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_GroupsLinkedOrderLinesWhenDescriptionsDiffer()
    {
        await using var db = CreateContext();
        var documentDate = new DateTime(2026, 10, 19);

        db.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, "PILIC KANAT KG", 6.61d, documentDate),
            CreateMovement(1, "ULKER BIZIM MARGARIN", 48d, documentDate));
        await db.SaveChangesAsync();

        var result = await new WarehouseShippingListQueryExecutor(db).ExecuteAsync(
            new WarehouseShippingListRequest(53, documentDate, documentDate),
            WarehouseShippingDirection.Outgoing,
            false,
            CancellationToken.None);

        var shipment = Assert.Single(result);
        Assert.Equal("F53", shipment.DocumentSerie);
        Assert.Equal(120696, shipment.DocumentOrderNo);
        Assert.Equal(2, shipment.LineCount);
        Assert.Equal(54.61d, shipment.TotalQuantity, 6);
        Assert.Equal(string.Empty, shipment.DescriptionEttn);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUuidOnlyWhenDescriptionContainsEDespatchUuid()
    {
        await using var db = CreateContext();
        var documentDate = new DateTime(2026, 10, 19);
        var eDespatchUuid = Guid.NewGuid();

        db.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, eDespatchUuid.ToString(), 12d, documentDate),
            CreateMovement(1, eDespatchUuid.ToString(), 18d, documentDate));
        await db.SaveChangesAsync();

        var result = await new WarehouseShippingListQueryExecutor(db).ExecuteAsync(
            new WarehouseShippingListRequest(53, documentDate, documentDate),
            WarehouseShippingDirection.Outgoing,
            false,
            CancellationToken.None);

        var shipment = Assert.Single(result);
        Assert.Equal(eDespatchUuid.ToString(), shipment.DescriptionEttn);
    }

    [Fact]
    public async Task ExecuteAsync_GroupsPartiallyMarkedEDespatchAsSingleDocument()
    {
        await using var db = CreateContext();
        var documentDate = new DateTime(2026, 10, 19);

        db.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, "PILIC KANAT KG", 12d, documentDate, "FRM2026600132349"),
            CreateMovement(1, "ULKER BIZIM MARGARIN", 18d, documentDate));
        await db.SaveChangesAsync();

        var result = await new WarehouseShippingListQueryExecutor(db).ExecuteAsync(
            new WarehouseShippingListRequest(53, documentDate, documentDate),
            WarehouseShippingDirection.Outgoing,
            false,
            CancellationToken.None);

        var shipment = Assert.Single(result);
        Assert.Equal("FRM2026600132349", shipment.DocumentNo);
        Assert.Equal(2, shipment.LineCount);
        Assert.Equal(30d, shipment.TotalQuantity);
    }

    private static MikroDbContext CreateContext() => new(
        new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"warehouse-shipping-list-{Guid.NewGuid():N}")
            .Options);

    private static STOK_HAREKETLERI CreateMovement(
        int rowNo,
        string description,
        double quantity,
        DateTime documentDate,
        string documentNo = "") => new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_date = documentDate.AddHours(13).AddMilliseconds(rowNo),
            sth_evraktip = 17,
            sth_tip = 2,
            sth_cins = 6,
            sth_normal_iade = 0,
            sth_evrakno_seri = "F53",
            sth_evrakno_sira = 120696,
            sth_satirno = rowNo,
            sth_stok_kod = $"STOK-{rowNo}",
            sth_cikis_depo_no = 53,
            sth_giris_depo_no = 60,
            sth_nakliyedeposu = 149,
            sth_nakliyedurumu = 0,
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_belge_no = documentNo,
            sth_miktar = quantity,
            sth_tutar = 0d,
            sth_birim_pntr = 1,
            sth_aciklama = description
        };
}
