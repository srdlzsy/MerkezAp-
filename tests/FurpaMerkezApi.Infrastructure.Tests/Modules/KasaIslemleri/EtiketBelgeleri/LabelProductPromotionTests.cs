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
    public async Task PriceChangedProducts_LoadPromotionsOnceForAllStockCodes()
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
        Assert.Equal(100d, promotionLookup.LastPricesByStockCode["STK001"]);
        Assert.Equal(50d, promotionLookup.LastPricesByStockCode["STK002"]);
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

    [Fact]
    public async Task ActivePromotionProducts_IncludeProductWithoutRecentPriceChangeRequirement()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTime(2026, 10, 6, 10, 0, 0);
        AddProduct(dbContext, "STK001", 101, 100d, now);
        await dbContext.SaveChangesAsync();

        var promotionLookup = new RecordingPromotionLookup();
        var executor = new LabelProductQueryExecutor(dbContext, promotionLookup);

        var result = await executor.ListActivePromotionProductsAsync(
            149,
            CancellationToken.None);

        var product = Assert.Single(result);
        Assert.Equal("STK001", product.ProductCode);
        Assert.Equal(100d, product.Price);
        Assert.Equal("Shopigo", product.Promotion.Source);
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

        public IReadOnlyDictionary<string, double> LastPricesByStockCode { get; private set; } =
            new Dictionary<string, double>();

        public Task<IReadOnlyDictionary<string, LabelPromotionDto>> GetActiveProductPromotionsAsync(
            int warehouseNo,
            IReadOnlyDictionary<string, double> pricesByStockCode,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastPricesByStockCode = pricesByStockCode;

            return Task.FromResult<IReadOnlyDictionary<string, LabelPromotionDto>>(
                pricesByStockCode.ToDictionary(
                    item => item.Key,
                    item => CreatePromotion(item.Key, item.Value),
                    StringComparer.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyCollection<string>> GetActiveProductCodesAsync(
            int warehouseNo,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>(["STK001"]);

        private static LabelPromotionDto CreatePromotion(string stockCode, double price) =>
            new()
            {
                Source = "Shopigo",
                IsActive = true,
                PromotionCode = $"P-{stockCode}",
                PromotionType = "PUF1",
                NormalPrice = price,
                PromotionPrice = price * 0.8,
                EffectiveUnitPrice = price * 0.8,
                RequiredProductCode = stockCode,
                RequiredQuantity = 1,
                DiscountedProductCode = stockCode,
                DiscountedQuantity = 1,
                DiscountType = "PERCENTAGE",
                DiscountValue = 20,
                DiscountRate = 20
            };
    }
}
