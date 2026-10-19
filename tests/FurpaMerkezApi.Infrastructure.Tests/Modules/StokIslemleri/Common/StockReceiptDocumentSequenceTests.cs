using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Common;

public sealed class StockReceiptDocumentSequenceTests
{
    [Fact]
    public async Task GetNextDocumentOrderNo_UsesTheEntireUniqueIndexScopeAcrossMovementGenres()
    {
        await using var db = new MikroWriteDbContext(
            new DbContextOptionsBuilder<MikroWriteDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        db.STOK_HAREKETLERIs.AddRange(
            Movement(documentType: 0, serie: "F167", orderNo: 1, movementGenre: 5),
            Movement(documentType: 0, serie: "F167", orderNo: 14, movementGenre: 4),
            Movement(documentType: 0, serie: "F168", orderNo: 50, movementGenre: 5),
            Movement(documentType: 1, serie: "F167", orderNo: 60, movementGenre: 5));
        await db.SaveChangesAsync();

        var service = new StockReceiptWriteService(
            db,
            null!,
            null!,
            null!,
            null!,
            NullLogger<StockReceiptWriteService>.Instance);

        var result = await service.GetNextDocumentOrderNoAsync("F167", CancellationToken.None);

        Assert.Equal(15, result);
    }

    private static STOK_HAREKETLERI Movement(
        byte documentType,
        string serie,
        int orderNo,
        byte movementGenre) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_evraktip = documentType,
            sth_evrakno_seri = serie,
            sth_evrakno_sira = orderNo,
            sth_satirno = 0,
            sth_cins = movementGenre
        };
}
