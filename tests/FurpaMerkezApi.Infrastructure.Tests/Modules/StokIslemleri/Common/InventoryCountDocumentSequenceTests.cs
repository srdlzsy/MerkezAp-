using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Common;

public sealed class InventoryCountDocumentSequenceTests
{
    [Fact]
    public async Task GetNextDocumentNo_UsesDocumentDateAndWarehouseScope()
    {
        await using var db = CreateDbContext();
        var documentDate = new DateTime(2026, 10, 26);
        db.SAYIM_SONUCLARIs.AddRange(
            Result(documentDate, warehouseNo: 158, documentNo: 1),
            Result(documentDate, warehouseNo: 158, documentNo: 3),
            Result(documentDate.AddDays(-1), warehouseNo: 158, documentNo: 152),
            Result(documentDate, warehouseNo: 159, documentNo: 200));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetNextDocumentNoAsync(
            warehouseNo: 158,
            documentDate,
            CancellationToken.None);

        Assert.Equal(4, result);
    }

    [Fact]
    public async Task GetNextDocumentNo_StartsAtOneForANewDate()
    {
        await using var db = CreateDbContext();
        db.SAYIM_SONUCLARIs.Add(
            Result(new DateTime(2026, 10, 25), warehouseNo: 158, documentNo: 152));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetNextDocumentNoAsync(
            warehouseNo: 158,
            new DateTime(2026, 10, 26),
            CancellationToken.None);

        Assert.Equal(1, result);
    }

    private static MikroWriteDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<MikroWriteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

    private static InventoryCountWriteService CreateService(MikroWriteDbContext db) =>
        new(
            db,
            null!,
            null!,
            null!,
            null!,
            NullLogger<InventoryCountWriteService>.Instance);

    private static SAYIM_SONUCLARI Result(
        DateTime documentDate,
        int warehouseNo,
        int documentNo) =>
        new()
        {
            sym_Guid = Guid.NewGuid(),
            sym_tarihi = documentDate,
            sym_depono = warehouseNo,
            sym_evrakno = documentNo,
            sym_satirno = 0
        };
}
