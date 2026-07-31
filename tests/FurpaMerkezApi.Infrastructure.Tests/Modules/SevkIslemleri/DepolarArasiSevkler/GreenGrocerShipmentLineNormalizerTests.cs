using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class GreenGrocerShipmentLineNormalizerTests
{
    [Fact]
    public async Task DetachWarehouseOrderLinksAsync_RemovesLinkedOrderGuidForGreenGrocerSourceAndStock()
    {
        await using var dbContext = CreateDbContext();
        dbContext.STOKLARs.Add(CreateStock("001082", "10"));
        await dbContext.SaveChangesAsync();
        var linkedGuid = Guid.Parse("44d8b818-0c9f-4a2d-9a38-e7f09df89ea9");
        var request = CreateRequest(
            sourceWarehouseNo: 56,
            new CreateInterWarehouseShipmentLineRequest("001082", 39.49d, linkedGuid));

        var lines = await GreenGrocerShipmentLineNormalizer.DetachWarehouseOrderLinksAsync(
            dbContext,
            request,
            request.Lines.ToArray(),
            orderLinkingEnabled: false,
            CancellationToken.None);

        Assert.Null(lines.Single().WarehouseOrderLineGuid);
    }

    [Fact]
    public async Task DetachWarehouseOrderLinksAsync_KeepsLinkedOrderGuidWhenOrderLinkingIsEnabled()
    {
        await using var dbContext = CreateDbContext();
        dbContext.STOKLARs.Add(CreateStock("001082", "10"));
        await dbContext.SaveChangesAsync();
        var linkedGuid = Guid.Parse("c494d331-1a8d-4d6b-bf26-635a76189d75");
        var request = CreateRequest(
            sourceWarehouseNo: 56,
            new CreateInterWarehouseShipmentLineRequest("001082", 39.49d, linkedGuid));

        var lines = await GreenGrocerShipmentLineNormalizer.DetachWarehouseOrderLinksAsync(
            dbContext,
            request,
            request.Lines.ToArray(),
            orderLinkingEnabled: true,
            CancellationToken.None);

        Assert.Equal(linkedGuid, lines.Single().WarehouseOrderLineGuid);
    }

    [Fact]
    public async Task DetachWarehouseOrderLinksAsync_KeepsLinkedOrderGuidForNonGreenGrocerStock()
    {
        await using var dbContext = CreateDbContext();
        dbContext.STOKLARs.Add(CreateStock("015550", "20"));
        await dbContext.SaveChangesAsync();
        var linkedGuid = Guid.Parse("4ba57c6d-7e6a-4e85-8d45-52287052016b");
        var request = CreateRequest(
            sourceWarehouseNo: 56,
            new CreateInterWarehouseShipmentLineRequest("015550", 10d, linkedGuid));

        var lines = await GreenGrocerShipmentLineNormalizer.DetachWarehouseOrderLinksAsync(
            dbContext,
            request,
            request.Lines.ToArray(),
            orderLinkingEnabled: false,
            CancellationToken.None);

        Assert.Equal(linkedGuid, lines.Single().WarehouseOrderLineGuid);
    }

    [Fact]
    public async Task DetachWarehouseOrderLinksAsync_KeepsLinkedOrderGuidForNonGreenGrocerSource()
    {
        await using var dbContext = CreateDbContext();
        dbContext.STOKLARs.Add(CreateStock("001082", "10"));
        await dbContext.SaveChangesAsync();
        var linkedGuid = Guid.Parse("8a86e5b7-c49c-46cf-a37e-0e27fc5dc82e");
        var request = CreateRequest(
            sourceWarehouseNo: 50,
            new CreateInterWarehouseShipmentLineRequest("001082", 39.49d, linkedGuid));

        var lines = await GreenGrocerShipmentLineNormalizer.DetachWarehouseOrderLinksAsync(
            dbContext,
            request,
            request.Lines.ToArray(),
            orderLinkingEnabled: false,
            CancellationToken.None);

        Assert.Equal(linkedGuid, lines.Single().WarehouseOrderLineGuid);
    }

    private static MikroWriteDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MikroWriteDbContext(options);
    }

    private static STOKLAR CreateStock(string stockCode, string modelCode) =>
        new()
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = stockCode,
            sto_isim = stockCode,
            sto_model_kodu = modelCode,
            sto_create_date = DateTime.Now
        };

    private static CreateInterWarehouseShipmentRequest CreateRequest(
        int sourceWarehouseNo,
        CreateInterWarehouseShipmentLineRequest line) =>
        new(
            sourceWarehouseNo,
            118,
            60,
            new DateTime(2026, 7, 30),
            new DateTime(2026, 7, 30),
            null,
            null,
            [line]);
}
