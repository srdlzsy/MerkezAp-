using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class WarehouseShippingDetailQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_AcceptsPartiallyMarkedSingleDocument()
    {
        await using var db = CreateContext();
        db.STOK_HAREKETLERIs.AddRange(CreateMovement(0, "FRM2026600132349"), CreateMovement(1, null));
        await db.SaveChangesAsync();

        var detail = await new WarehouseShippingDetailQueryExecutor(db).ExecuteAsync(
            new WarehouseShippingDetailRequest(56, "F56", 88049),
            WarehouseShippingDirection.Outgoing,
            false,
            CancellationToken.None);

        Assert.Equal("FRM2026600132349", detail.Header.DocumentNo);
        Assert.Equal(2, detail.Items.Count);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsDifferentSentDocumentNumbers()
    {
        await using var db = CreateContext();
        db.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, "FRM2026600132349"),
            CreateMovement(1, "FRM2026600132350"));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WarehouseShippingDetailQueryExecutor(db).ExecuteAsync(
                new WarehouseShippingDetailRequest(56, "F56", 88049),
                WarehouseShippingDirection.Outgoing,
                false,
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotExposeLineDescriptionAsEDespatchUuid()
    {
        await using var db = CreateContext();
        db.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, null, "PILIC KANAT KG"),
            CreateMovement(1, null, "ULKER BIZIM MARGARIN"));
        await db.SaveChangesAsync();

        var detail = await new WarehouseShippingDetailQueryExecutor(db).ExecuteAsync(
            new WarehouseShippingDetailRequest(56, "F56", 88049),
            WarehouseShippingDirection.Outgoing,
            false,
            CancellationToken.None);

        Assert.Equal(string.Empty, detail.Header.DescriptionEttn);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidEDespatchUuidFromMovementDescription()
    {
        await using var db = CreateContext();
        var eDespatchUuid = Guid.NewGuid();
        db.STOK_HAREKETLERIs.AddRange(
            CreateMovement(0, "FRM2026600132349", eDespatchUuid.ToString()),
            CreateMovement(1, "FRM2026600132349", eDespatchUuid.ToString()));
        await db.SaveChangesAsync();

        var detail = await new WarehouseShippingDetailQueryExecutor(db).ExecuteAsync(
            new WarehouseShippingDetailRequest(56, "F56", 88049),
            WarehouseShippingDirection.Outgoing,
            false,
            CancellationToken.None);

        Assert.Equal(eDespatchUuid.ToString(), detail.Header.DescriptionEttn);
    }

    private static MikroDbContext CreateContext() => new(
        new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"warehouse-shipping-detail-{Guid.NewGuid():N}")
            .Options);

    private static STOK_HAREKETLERI CreateMovement(
        int rowNo,
        string? documentNo,
        string? description = null) => new()
    {
        sth_Guid = Guid.NewGuid(),
        sth_evraktip = 17,
        sth_tip = 2,
        sth_cins = 6,
        sth_normal_iade = 0,
        sth_evrakno_seri = "F56",
        sth_evrakno_sira = 88049,
        sth_satirno = rowNo,
        sth_stok_kod = $"STOK-{rowNo}",
        sth_cikis_depo_no = 56,
        sth_giris_depo_no = 60,
        sth_nakliyedeposu = 118,
        sth_nakliyedurumu = 0,
        sth_tarih = new DateTime(2026, 9, 30),
        sth_belge_tarih = new DateTime(2026, 9, 30),
        sth_belge_no = documentNo,
        sth_aciklama = description,
        sth_miktar = 1d,
        sth_tutar = 10d,
        sth_birim_pntr = 1
    };
}
