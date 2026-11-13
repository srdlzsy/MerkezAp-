using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public sealed class WarehouseMovementMikroSnapshotReaderTests
{
    [Fact]
    public async Task ReadAsync_GroupsDocumentsAndResolvesPendingAndAcceptedTargets()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"warehouse-movement-snapshot-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new MikroDbContext(options);
        var sentUuid = Guid.NewGuid().ToString();
        dbContext.STOK_HAREKETLERIs.AddRange(
            CreateMovement("F50", 10, 0, 50, 60, 104, 0, true, sentUuid, 0),
            CreateMovement("F50", 10, 0, 50, 60, 104, 0, true, sentUuid, 1),
            CreateMovement("I104", 11, 1, 104, 50, 60, 1, true, sentUuid, 0),
            CreateMovement("F50", 12, 0, 50, 60, 104, 0, true, sentUuid, 0, isCancelled: true),
            CreateCompanyMovement("FC50", 20, 50, false, sentUuid),
            CreateCompanyMovement("FC50", 21, 50, true, sentUuid, isCancelled: true));
        await dbContext.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var reader = new WarehouseMovementMikroSnapshotReader(
            dbContext,
            cache,
            NullLogger<WarehouseMovementMikroSnapshotReader>.Instance);

        var result = await reader.ReadAsync(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 13),
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(2, result.Rows.Count);

        var pending = Assert.Single(result.Rows, row => row.DocumentOrderNo == 10);
        Assert.False(pending.IsAccepted);
        Assert.True(pending.IsEDespatchSent);
        Assert.Equal(104, pending.TargetWarehouseNo);

        var accepted = Assert.Single(result.Rows, row => row.DocumentOrderNo == 11);
        Assert.True(accepted.IsAccepted);
        Assert.Equal(50, accepted.TargetWarehouseNo);

        var companyMovement = Assert.Single(result.CompanyRows!);
        Assert.Equal(20, companyMovement.DocumentOrderNo);
        Assert.False(companyMovement.IsReturn);
        Assert.True(companyMovement.IsEDespatchSent);
    }

    private static STOK_HAREKETLERI CreateMovement(
        string serie,
        int orderNo,
        byte returnType,
        int sourceWarehouseNo,
        int entryWarehouseNo,
        int shippingWarehouseNo,
        byte shippingState,
        bool locked,
        string uuid,
        int lineNo,
        bool isCancelled = false) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_iptal = isCancelled,
            sth_evraktip = 17,
            sth_evrakno_seri = serie,
            sth_evrakno_sira = orderNo,
            sth_satirno = lineNo,
            sth_normal_iade = returnType,
            sth_cikis_depo_no = sourceWarehouseNo,
            sth_giris_depo_no = entryWarehouseNo,
            sth_nakliyedeposu = shippingWarehouseNo,
            sth_nakliyedurumu = shippingState,
            sth_kilitli = locked,
            sth_belge_no = "FRM2026000000001",
            sth_aciklama = uuid,
            sth_tarih = new DateTime(2026, 11, 13),
            sth_create_date = new DateTime(2026, 11, 13, 8, 0, 0),
            sth_lastup_date = new DateTime(2026, 11, 13, 8, 10, 0)
        };

    private static STOK_HAREKETLERI CreateCompanyMovement(
        string serie,
        int orderNo,
        int sourceWarehouseNo,
        bool isReturn,
        string uuid,
        bool isCancelled = false) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_iptal = isCancelled,
            sth_evraktip = 1,
            sth_tip = 1,
            sth_normal_iade = isReturn ? (byte)1 : (byte)0,
            sth_evrakno_seri = serie,
            sth_evrakno_sira = orderNo,
            sth_satirno = 0,
            sth_cikis_depo_no = sourceWarehouseNo,
            sth_kilitli = true,
            sth_belge_no = "FRM2026000000001",
            sth_aciklama = uuid,
            sth_tarih = new DateTime(2026, 11, 13),
            sth_belge_tarih = new DateTime(2026, 11, 13),
            sth_create_date = new DateTime(2026, 11, 13, 8, 0, 0),
            sth_lastup_date = new DateTime(2026, 11, 13, 8, 10, 0)
        };
}
