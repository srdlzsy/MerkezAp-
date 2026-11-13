using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public sealed class AxataOrderMikroSnapshotReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsOnlyEnabledAxataScopeAndUsesMikroSentFlag()
    {
        var dbOptions = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"axata-order-snapshot-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new MikroDbContext(dbOptions);
        dbContext.DEPOLAR_ARASI_SIPARISLERs.AddRange(
            CreateWarehouseOrder("F104", 1, 104, 50, "1"),
            CreateWarehouseOrder("F105", 2, 105, 50, string.Empty),
            CreateWarehouseOrder("F50", 3, 50, 106, string.Empty),
            CreateWarehouseOrder("F107", 4, 107, 108, string.Empty),
            CreateWarehouseOrder("F109", 5, 109, 50, string.Empty, isCancelled: true));
        dbContext.SIPARISLERs.AddRange(
            CreateCompanyOrder("C120", 6, 120, string.Empty),
            CreateCompanyOrder("C121", 7, 121, "1"));
        await dbContext.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var synchronizationOptions = new AxataSynchronizationOptions
        {
            Enabled = true,
            Tasks = new Dictionary<string, AxataSynchronizationTaskOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["issued-warehouse-order-sync"] = new() { Enabled = true, DefaultWarehouseNo = 50 },
                ["warehouse-inbound-order-sync"] = new() { Enabled = true, DefaultWarehouseNo = 50 },
                ["received-company-order-sync"] = new() { Enabled = true }
            }
        };
        var reader = new AxataOrderMikroSnapshotReader(
            dbContext,
            cache,
            new StaticOptionsMonitor<AxataSynchronizationOptions>(synchronizationOptions),
            NullLogger<AxataOrderMikroSnapshotReader>.Instance);

        var result = await reader.ReadAsync(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 13),
            CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(5, result.Rows.Count);
        Assert.Contains(result.Rows, row =>
            row.DocumentType == DocumentFlowType.IssuedWarehouseOrder &&
            row.DocumentOrderNo == 1 &&
            row.IsSent);
        Assert.Contains(result.Rows, row =>
            row.DocumentType == DocumentFlowType.IssuedWarehouseOrder &&
            row.DocumentOrderNo == 2 &&
            !row.IsSent);
        Assert.Contains(result.Rows, row =>
            row.DocumentType == DocumentFlowType.ReceivedWarehouseOrder &&
            row.DocumentOrderNo == 3 &&
            !row.IsSent);
        Assert.DoesNotContain(result.Rows, row => row.DocumentOrderNo is 4 or 5);
        Assert.Contains(result.Rows, row =>
            row.DocumentType == DocumentFlowType.ReceivedCompanyOrder &&
            row.SourceWarehouseNo == 120 &&
            row.DocumentOrderNo == 6 &&
            !row.IsSent);
        Assert.Contains(result.Rows, row =>
            row.DocumentType == DocumentFlowType.ReceivedCompanyOrder &&
            row.SourceWarehouseNo == 121 &&
            row.DocumentOrderNo == 7 &&
            row.IsSent);
    }

    private static DEPOLAR_ARASI_SIPARISLER CreateWarehouseOrder(
        string serie,
        int orderNo,
        int inWarehouseNo,
        int outWarehouseNo,
        string sentFlag,
        bool isCancelled = false) =>
        new()
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_iptal = isCancelled,
            ssip_create_date = new DateTime(2026, 11, 13, 8, 0, 0),
            ssip_lastup_date = new DateTime(2026, 11, 13, 8, 5, 0),
            ssip_special1 = sentFlag,
            ssip_tarih = new DateTime(2026, 11, 13),
            ssip_evrakno_seri = serie,
            ssip_evrakno_sira = orderNo,
            ssip_satirno = 0,
            ssip_stok_kod = "000001",
            ssip_miktar = 10,
            ssip_teslim_miktar = 0,
            ssip_girdepo = inWarehouseNo,
            ssip_cikdepo = outWarehouseNo
        };

    private static SIPARISLER CreateCompanyOrder(
        string serie,
        int orderNo,
        int warehouseNo,
        string sentFlag) =>
        new()
        {
            sip_Guid = Guid.NewGuid(),
            sip_create_date = new DateTime(2026, 11, 13, 8, 0, 0),
            sip_lastup_date = new DateTime(2026, 11, 13, 8, 5, 0),
            sip_iptal = false,
            sip_kapat_fl = false,
            sip_tarih = new DateTime(2026, 11, 13),
            sip_tip = 0,
            sip_cins = 0,
            sip_evrakno_seri = serie,
            sip_evrakno_sira = orderNo,
            sip_satirno = 0,
            sip_stok_kod = "000001",
            sip_miktar = 10,
            sip_teslim_miktar = 0,
            sip_depono = warehouseNo,
            sip_special1 = sentFlag
        };

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
