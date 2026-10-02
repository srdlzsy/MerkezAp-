using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class StockMovementReadbackTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "shipment", "return", "outage", "expense", "virman" }
        from scenario in new[] { "complete", "partial", "changed", "extra-untraced" }
        select new object[] { operation, scenario };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OfflineRetry_OnlyCompletesForTheEntireMatchingDocument(string operation, string scenario)
    {
        await using var auth = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await using var mikro = new MikroWriteDbContext(new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var sync = new MobileOfflineSyncService(auth, new TestClock());
        var user = Guid.NewGuid();
        var id = StockMovementRecoveryMatcherTests.RequestId;
        var rows = new List<STOK_HAREKETLERI> { StockMovementRecoveryMatcherTests.Row(), StockMovementRecoveryMatcherTests.Row(1) };
        var writeOptions = Options.Create(new MikroWriteOptions("", "Test", "0032"));
        Func<Task> run;
        if (operation is "shipment" or "return")
        {
            var request = StockMovementRecoveryMatcherTests.CompanyRequest with { RequestedByUserId = user };
            var isReturn = operation == "return";
            foreach (var row in rows) { row.sth_cins = isReturn ? (byte)0 : (byte)1; row.sth_normal_iade = isReturn ? (byte)1 : (byte)0; }
            await Reserve(sync, isReturn ? "iade-islemleri.firma-iadeleri.create" : "sevk-islemleri.giden-firma-sevkleri.create", user, id, request);
            var service = new CompanyMovementWriteService(mikro, writeOptions, null!, null!, sync, NullLogger<CompanyMovementWriteService>.Instance);
            run = async () => { await service.ExecuteAsync(request, isReturn ? CompanyMovementKind.PurchaseReturn : CompanyMovementKind.OutgoingShipment, CancellationToken.None); };
        }
        else if (operation == "virman")
        {
            var request = new CreateVirmanRequest(56, null, null, null, null, [new("A", 2, 2)], id, user);
            rows[1].sth_stok_kod = "A"; rows[1].sth_miktar = 2; rows[1].sth_tip = 0;
            foreach (var row in rows) { row.sth_evraktip = 6; row.sth_cins = 3; row.sth_cari_kodu = ""; row.sth_giris_depo_no = 56; }
            await Reserve(sync, "stok-islemleri.virmanlar.create", user, id, request);
            var service = new VirmanWriteService(mikro, writeOptions, null!, null!, sync, NullLogger<VirmanWriteService>.Instance);
            run = async () => { await service.ExecuteAsync(request, CancellationToken.None); };
        }
        else
        {
            var request = new CreateStockReceiptRequest(56, "Creator", "Acceptor", null, null, null, null,
                [new("A", 2), new("B", 3)], id, user);
            var isExpense = operation == "expense";
            foreach (var row in rows) { row.sth_evraktip = 0; row.sth_cins = isExpense ? (byte)5 : (byte)4; row.sth_cari_kodu = ""; }
            await Reserve(sync, isExpense ? "stok-islemleri.masraf-fisleri.create" : "stok-islemleri.zayiat-fisleri.create", user, id, request);
            var service = new StockReceiptWriteService(mikro, writeOptions, null!, null!, sync, NullLogger<StockReceiptWriteService>.Instance);
            run = async () => { await service.ExecuteAsync(request, isExpense ? StockReceiptKind.ExpenseReceipt : StockReceiptKind.OutageReceipt, CancellationToken.None); };
        }
        if (scenario == "partial") rows.RemoveAt(1);
        if (scenario == "changed") rows[1].sth_miktar = 999;
        if (scenario == "extra-untraced")
        {
            var extra = StockMovementRecoveryMatcherTests.Row(2);
            extra.sth_evraktip = rows[0].sth_evraktip;
            extra.sth_normal_iade = rows[0].sth_normal_iade;
            extra.sth_cins = rows[0].sth_cins;
            extra.sth_eticaret_kanal_kodu = "";
            rows.Add(extra);
        }
        mikro.STOK_HAREKETLERIs.AddRange(rows);
        await mikro.SaveChangesAsync();
        if (scenario == "complete")
        {
            await run();
            await run();
            Assert.Equal(MobileOfflineSyncRequestStatus.Completed, (await auth.MobileOfflineSyncRequests.SingleAsync()).Status);
        }
        else
        {
            var error = await Assert.ThrowsAsync<OperationConflictException>(run);
            Assert.Equal(scenario == "partial", error.Retryable);
            Assert.NotEqual(MobileOfflineSyncRequestStatus.Completed, (await auth.MobileOfflineSyncRequests.SingleAsync()).Status);
            await Assert.ThrowsAsync<OperationConflictException>(run);
        }
        Assert.Equal(rows.Count, await mikro.STOK_HAREKETLERIs.CountAsync());
    }

    private static Task<MobileOfflineSyncAcquireResult<string>> Reserve<T>(MobileOfflineSyncService service,
        string operation, Guid user, Guid id, T request) => service.AcquireAsync<T, string>(
            operation, user, 56, id, request, (_, _) => Task.FromResult<string?>(null), CancellationToken.None, true);

    private sealed class TestClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
}
