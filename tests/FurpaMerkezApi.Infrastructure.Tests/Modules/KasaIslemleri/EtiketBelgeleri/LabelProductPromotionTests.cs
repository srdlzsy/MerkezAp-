using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class LabelProductPromotionTests
{
    [Fact]
    public async Task PriceChangedProducts_LoadPromotionsOnceForAllPluNumbers()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTime(2026, 10, 6, 10, 0, 0);
        AddProduct(dbContext, "STK001", 101, 100d, now);
        AddProduct(dbContext, "STK002", 102, 50d, now);
        await dbContext.SaveChangesAsync();

        var promotionLookup = new RecordingPromotionLookup();
        var executor = new LabelProductQueryExecutor(dbContext, promotionLookup);

        var result = await executor.ListPriceChangedProductsAsync(
            149,
            now.AddMinutes(-1),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, promotionLookup.CallCount);
        Assert.Equal(100d, promotionLookup.LastPricesByPlu[101]);
        Assert.Equal(50d, promotionLookup.LastPricesByPlu[102]);
        Assert.All(result, item => Assert.NotNull(item.Promotion));
    }

    [Fact]
    public async Task LabelDocumentProducts_IncludeBulkResolvedPromotion()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTime(2026, 10, 6, 10, 0, 0);
        AddProduct(dbContext, "STK001", 101, 100d, now);
        await dbContext.SaveChangesAsync();

        var promotionLookup = new RecordingPromotionLookup();
        var executor = new LabelProductQueryExecutor(dbContext, promotionLookup);

        var result = await executor.ExecuteAsync(
            149,
            ["STK001"],
            42,
            CancellationToken.None);

        var product = Assert.Single(result).Value;
        Assert.Equal(1, promotionLookup.CallCount);
        Assert.NotNull(product.Promotion);
        Assert.Equal(80d, product.Promotion.PromotionPrice);
    }

    private static void AddProduct(
        MikroDbContext dbContext,
        string stockCode,
        int pluNo,
        double price,
        DateTime now)
    {
        dbContext.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_create_date = now.AddYears(-1),
            sto_kod = stockCode,
            sto_isim = $"Product {stockCode}",
            sto_plu_no = pluNo,
            sto_satis_dursun = 0,
            sto_birim1_ad = "ADET"
        });
        dbContext.STOK_FIYAT_DEGISIKLIKLERIs.Add(new STOK_FIYAT_DEGISIKLIKLERI
        {
            fid_Guid = Guid.NewGuid(),
            fid_create_date = now,
            fid_lastup_date = now,
            fid_stok_kod = stockCode,
            fid_depo_no = 149,
            fid_yapildi_fl = 1,
            fid_eskifiy_tutar = price + 10,
            fid_yenifiy_tutar = price
        });
        dbContext.STOK_SATIS_FIYAT_LISTELERIs.Add(new STOK_SATIS_FIYAT_LISTELERI
        {
            sfiyat_Guid = Guid.NewGuid(),
            sfiyat_create_date = now,
            sfiyat_stokkod = stockCode,
            sfiyat_deposirano = 149,
            sfiyat_birim_pntr = 1,
            sfiyat_listesirano = 1,
            sfiyat_fiyati = price
        });
    }

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"label-product-promotion-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private sealed class RecordingPromotionLookup : ILabelPromotionLookup
    {
        public int CallCount { get; private set; }

        public IReadOnlyDictionary<int, double> LastPricesByPlu { get; private set; } =
            new Dictionary<int, double>();

        public Task<IReadOnlyDictionary<int, LabelPromotionDto>> GetActiveCardPromotionsAsync(
            int warehouseNo,
            IReadOnlyDictionary<int, double> pricesByPlu,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastPricesByPlu = pricesByPlu;

            return Task.FromResult<IReadOnlyDictionary<int, LabelPromotionDto>>(
                pricesByPlu.ToDictionary(
                    item => item.Key,
                    item => new LabelPromotionDto
                    {
                        IsActive = true,
                        PromotionCode = $"P-{item.Key}",
                        PromotionType = "P2",
                        NormalPrice = item.Value,
                        PromotionPrice = item.Value * 0.8,
                        DiscountRate = 20
                    }));
        }
    }
}
