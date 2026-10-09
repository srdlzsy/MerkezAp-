using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanConversionSuggestionUseCaseTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_ReturnsSuggestionAtConfidenceThreshold()
    {
        await using var dbContext = CreateDbContext();
        AddStock(dbContext, "PACK", "SODA 6 LI");
        AddStock(dbContext, "SINGLE", "SODA TEKLI");
        AddStock(dbContext, "NOISE", "BASKA URUN");

        for (var index = 1; index <= 19; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "SINGLE", 6d);
        }
        AddPair(dbContext, 20, "PACK", 1d, "NOISE", 2d);
        await dbContext.SaveChangesAsync();

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            new VirmanConversionSuggestionRequest(" PACK ", 6d),
            CancellationToken.None);

        Assert.True(result.IsReliable);
        Assert.Equal("VirmanHistory", result.SuggestionSource);
        Assert.Equal("SINGLE", result.TargetStockCode);
        Assert.Equal(6d, result.Multiplier);
        Assert.Equal(36d, result.TargetQuantity);
        Assert.Equal(20, result.SampleCount);
        Assert.Equal(95d, result.TargetConfidencePercent);
        Assert.Equal(100d, result.MultiplierConfidencePercent);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSuggestWhenMultiplierConfidenceIsBelowThreshold()
    {
        await using var dbContext = CreateDbContext();
        AddStock(dbContext, "PACK", "SODA 6 LI");
        AddStock(dbContext, "SINGLE", "SODA TEKLI");

        for (var index = 1; index <= 18; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "SINGLE", 6d);
        }
        AddPair(dbContext, 19, "PACK", 1d, "SINGLE", 5d);
        AddPair(dbContext, 20, "PACK", 1d, "SINGLE", 5d);
        await dbContext.SaveChangesAsync();

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            new VirmanConversionSuggestionRequest("PACK", 2d),
            CancellationToken.None);

        Assert.False(result.IsReliable);
        Assert.Equal("None", result.SuggestionSource);
        Assert.Null(result.TargetStockCode);
        Assert.Null(result.Multiplier);
        Assert.Null(result.TargetQuantity);
        Assert.Equal(100d, result.TargetConfidencePercent);
        Assert.Equal(90d, result.MultiplierConfidencePercent);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSuggestWithFewerThanMinimumSamples()
    {
        await using var dbContext = CreateDbContext();
        AddStock(dbContext, "PACK", "SODA 6 LI");
        AddStock(dbContext, "SINGLE", "SODA TEKLI");

        for (var index = 1; index <= 9; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "SINGLE", 6d);
        }
        await dbContext.SaveChangesAsync();

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            new VirmanConversionSuggestionRequest("PACK", 2d),
            CancellationToken.None);

        Assert.False(result.IsReliable);
        Assert.Equal(9, result.SampleCount);
        Assert.Null(result.TargetStockCode);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSuggestInactiveTarget()
    {
        await using var dbContext = CreateDbContext();
        AddStock(dbContext, "PACK", "SODA 6 LI");
        AddStock(dbContext, "SINGLE", "DLS SODA TEKLI", isPassive: true);

        for (var index = 1; index <= 10; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "SINGLE", 6d);
        }
        await dbContext.SaveChangesAsync();

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            new VirmanConversionSuggestionRequest("PACK", 2d),
            CancellationToken.None);

        Assert.False(result.IsReliable);
        Assert.Null(result.TargetStockCode);
        Assert.Contains("pasif", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesFutureDatedAndSameStockPairs()
    {
        await using var dbContext = CreateDbContext();
        AddStock(dbContext, "PACK", "SODA 6 LI");
        AddStock(dbContext, "SINGLE", "SODA TEKLI");

        for (var index = 1; index <= 10; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "SINGLE", 6d);
        }

        AddPair(dbContext, 11, "PACK", 1d, "PACK", 1d);
        AddPair(dbContext, 12, "PACK", 1d, "SINGLE", 99d, NowUtc.AddDays(2));
        await dbContext.SaveChangesAsync();

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            new VirmanConversionSuggestionRequest("PACK", 2d),
            CancellationToken.None);

        Assert.True(result.IsReliable);
        Assert.Equal(10, result.SampleCount);
        Assert.Equal("SINGLE", result.TargetStockCode);
        Assert.Equal(6d, result.Multiplier);
        Assert.Equal(12d, result.TargetQuantity);
    }

    [Fact]
    public async Task ExecuteAsync_UsesOnlyMostRecentMaximumSampleCount()
    {
        await using var dbContext = CreateDbContext();
        AddStock(dbContext, "PACK", "SODA 6 LI");
        AddStock(dbContext, "SINGLE", "SODA TEKLI");
        AddStock(dbContext, "NOISE", "BASKA URUN");

        for (var index = 1; index <= 500; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "SINGLE", 6d, NowUtc.AddDays(-1));
        }

        for (var index = 501; index <= 520; index++)
        {
            AddPair(dbContext, index, "PACK", 1d, "NOISE", 2d, NowUtc.AddDays(-2));
        }

        await dbContext.SaveChangesAsync();

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            new VirmanConversionSuggestionRequest("PACK", 2d),
            CancellationToken.None);

        Assert.True(result.IsReliable);
        Assert.Equal(VirmanConversionSuggestionUseCase.MaximumSampleCount, result.SampleCount);
        Assert.Equal(VirmanConversionSuggestionUseCase.MaximumSampleCount, result.MaximumSampleCount);
        Assert.Equal("SINGLE", result.TargetStockCode);
        Assert.Equal(6d, result.Multiplier);
    }

    private static VirmanConversionSuggestionUseCase CreateUseCase(MikroDbContext dbContext) =>
        new(dbContext, new MemoryCache(new MemoryCacheOptions()), new FixedClock(NowUtc));

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"virman-conversion-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private static void AddStock(
        MikroDbContext dbContext,
        string stockCode,
        string stockName,
        bool isPassive = false)
    {
        dbContext.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_create_date = NowUtc,
            sto_kod = stockCode,
            sto_isim = stockName,
            sto_birim1_ad = "ADET",
            sto_iptal = false,
            sto_pasif_fl = isPassive
        });
    }

    private static void AddPair(
        MikroDbContext dbContext,
        int documentOrderNo,
        string sourceStockCode,
        double sourceQuantity,
        string targetStockCode,
        double targetQuantity,
        DateTime? movementDate = null)
    {
        dbContext.STOK_HAREKETLERIs.AddRange(
            CreateMovement(documentOrderNo, 0, 1, sourceStockCode, sourceQuantity, movementDate),
            CreateMovement(documentOrderNo, 1, 0, targetStockCode, targetQuantity, movementDate));
    }

    private static STOK_HAREKETLERI CreateMovement(
        int documentOrderNo,
        int rowNo,
        byte movementType,
        string stockCode,
        double quantity,
        DateTime? movementDate) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_create_date = NowUtc.AddDays(-1),
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = movementDate ?? NowUtc.AddDays(-1),
            sth_iptal = false,
            sth_evraktip = 6,
            sth_cins = 3,
            sth_normal_iade = 0,
            sth_tip = movementType,
            sth_evrakno_seri = "F110",
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_stok_kod = stockCode,
            sth_miktar = quantity,
            sth_giris_depo_no = 110,
            sth_cikis_depo_no = 110
        };

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
