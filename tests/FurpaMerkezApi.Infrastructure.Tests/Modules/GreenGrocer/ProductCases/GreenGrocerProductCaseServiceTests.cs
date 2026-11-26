using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.GreenGrocer.ProductCases;

public sealed class GreenGrocerProductCaseServiceTests
{
    [Fact]
    public async Task PreviewResolutionAsync_AdetProductWithSecondaryUnit_DefaultsToDirectPieceQuantity()
    {
        await using var authDb = CreateAuthDbContext();
        await using var mikroDb = CreateMikroDbContext();
        await using var furpaDb = CreateFurpaDbContext();
        mikroDb.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = "016167",
            sto_isim = "MNV MAYDANOZ ADET",
            sto_kisa_ismi = "MNV MAYDANOZ ADET",
            sto_model_kodu = "12",
            sto_birim1_ad = "ADET",
            sto_birim2_ad = "KOLI",
            sto_birim2_katsayi = 25,
            sto_pasif_fl = false,
            sto_create_date = DateTime.UtcNow
        });
        await mikroDb.SaveChangesAsync();

        var service = new GreenGrocerProductCaseService(
            authDb,
            mikroDb,
            furpaDb,
            new SystemClock(),
            new StaticOptionsMonitor<GreenGrocerProductCaseOptions>(new GreenGrocerProductCaseOptions
            {
                Enabled = true,
                OrderLinkingEnabled = true,
                SourceWarehouseNo = 56
            }));

        var result = await service.PreviewResolutionAsync(
            new GreenGrocerProductCaseResolutionRequest("016167", 30, 56),
            CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(GreenGrocerProductCaseModes.InputPiece, result.InputMode);
        Assert.Equal(GreenGrocerProductCaseModes.ConversionDirectQuantity, result.ConversionMode);
        Assert.Equal(30, result.EstimatedQuantity);
        Assert.Null(result.UnitsPerCase);
        Assert.Contains(result.Warnings, warning => warning.Contains("otomatik uygulanmadi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreviewResolutionAsync_AdetProductWithExplicitPackProfile_UsesConfiguredCaseFactor()
    {
        await using var authDb = CreateAuthDbContext();
        await using var mikroDb = CreateMikroDbContext();
        await using var furpaDb = CreateFurpaDbContext();
        mikroDb.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = "016167",
            sto_isim = "MNV MAYDANOZ ADET",
            sto_kisa_ismi = "MNV MAYDANOZ ADET",
            sto_model_kodu = "12",
            sto_birim1_ad = "ADET",
            sto_birim2_ad = "KOLI",
            sto_birim2_katsayi = 25,
            sto_pasif_fl = false,
            sto_create_date = DateTime.UtcNow
        });
        authDb.GreenGrocerProductCaseProfiles.Add(new GreenGrocerProductCaseProfile(
            Guid.NewGuid(),
            "016167",
            GreenGrocerProductCaseModes.InputPack,
            GreenGrocerProductCaseModes.ConversionFixedUnitsPerCase,
            Guid.NewGuid(),
            DateTime.UtcNow));
        await mikroDb.SaveChangesAsync();
        await authDb.SaveChangesAsync();

        var service = CreateService(authDb, mikroDb, furpaDb);

        var result = await service.PreviewResolutionAsync(
            new GreenGrocerProductCaseResolutionRequest("016167", 5, 56),
            CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(GreenGrocerProductCaseModes.InputPack, result.InputMode);
        Assert.Equal(GreenGrocerProductCaseModes.ConversionFixedUnitsPerCase, result.ConversionMode);
        Assert.Equal(125, result.EstimatedQuantity);
        Assert.Equal(25, result.UnitsPerCase);
    }

    [Fact]
    public async Task PreviewResolutionAsync_KgProduct_UsesWeighted7DayAverageAndReturnsTierMetadata()
    {
        await using var authDb = CreateAuthDbContext();
        await using var mikroDb = CreateMikroDbContext();
        await using var furpaDb = CreateFurpaDbContext();
        mikroDb.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = "016181",
            sto_isim = "MNV ELMA GOLDEN LUX KG",
            sto_kisa_ismi = "MNV ELMA GOLDEN LUX KG",
            sto_model_kodu = "10",
            sto_birim1_ad = "KG",
            sto_pasif_fl = false,
            sto_create_date = DateTime.UtcNow
        });
        await mikroDb.SaveChangesAsync();

        var orderDate = new DateTime(2026, 11, 25);
        var reader = new FakeLabelStatsReader([
            new GreenGrocerLabelObservation(orderDate.AddDays(-1), 200 * 3.85, 200),
            new GreenGrocerLabelObservation(orderDate.AddDays(-2), 200 * 3.92, 200),
            new GreenGrocerLabelObservation(orderDate.AddDays(-4), 128 * 3.90, 128),
            new GreenGrocerLabelObservation(orderDate.AddDays(-12), 100 * 7.43, 100)
        ]);

        var service = CreateService(authDb, mikroDb, furpaDb, reader);

        var result = await service.PreviewResolutionAsync(
            new GreenGrocerProductCaseResolutionRequest("016181", 3, 56, 101, orderDate),
            CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.False(result.RequiresManualApproval);
        Assert.Equal(GreenGrocerProductCaseModes.Tier1, result.CalculationTier);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceHigh, result.Confidence);
        Assert.Equal(7, result.AverageWindowDaysUsed);
        Assert.Equal(1, result.LabelAgeDays);
        Assert.True(result.DriftDetected);
        Assert.InRange(result.AverageKgPerCase!.Value, 3.88, 3.90);
        Assert.InRange(result.EstimatedQuantity, 11.65, 11.70);
        Assert.Equal(7.43, result.PriorAverageKgPerCase!.Value, 2);
    }

    [Fact]
    public async Task PreviewResolutionAsync_KgProductWithoutLabelHistory_UsesDirectQuantityWithoutBlockingUser()
    {
        await using var authDb = CreateAuthDbContext();
        await using var mikroDb = CreateMikroDbContext();
        await using var furpaDb = CreateFurpaDbContext();
        mikroDb.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = "016181",
            sto_isim = "MNV ELMA GOLDEN LUX KG",
            sto_kisa_ismi = "MNV ELMA GOLDEN LUX KG",
            sto_model_kodu = "10",
            sto_birim1_ad = "KG",
            sto_pasif_fl = false,
            sto_create_date = DateTime.UtcNow
        });
        await mikroDb.SaveChangesAsync();

        var service = CreateService(
            authDb,
            mikroDb,
            furpaDb,
            new FakeLabelStatsReader([]),
            profileStorageEnabled: false);

        var result = await service.PreviewResolutionAsync(
            new GreenGrocerProductCaseResolutionRequest("016181", 5, 56),
            CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.False(result.RequiresManualApproval);
        Assert.Equal(GreenGrocerProductCaseModes.ConversionDirectQuantity, result.ConversionMode);
        Assert.Equal(GreenGrocerProductCaseModes.ConfidenceLow, result.Confidence);
        Assert.Equal(5, result.EstimatedQuantity);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, warning => warning.Contains("dogrudan kullanildi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListProfilesAsync_ProfileStorageDisabled_ReturnsEmptyWithoutAuthTableDependency()
    {
        await using var authDb = CreateAuthDbContext();
        await using var mikroDb = CreateMikroDbContext();
        await using var furpaDb = CreateFurpaDbContext();
        var service = CreateService(
            authDb,
            mikroDb,
            furpaDb,
            profileStorageEnabled: false);

        var result = await service.ListProfilesAsync(
            new GreenGrocerProductCaseProfileListRequest(),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PreviewResolutionAsync_LowConfidenceData_DoesNotAskOrderUserForApprovalByDefault()
    {
        await using var authDb = CreateAuthDbContext();
        await using var mikroDb = CreateMikroDbContext();
        await using var furpaDb = CreateFurpaDbContext();
        mikroDb.STOKLARs.Add(new STOKLAR
        {
            sto_Guid = Guid.NewGuid(),
            sto_kod = "016181",
            sto_isim = "MNV ELMA GOLDEN LUX KG",
            sto_kisa_ismi = "MNV ELMA GOLDEN LUX KG",
            sto_model_kodu = "10",
            sto_birim1_ad = "KG",
            sto_pasif_fl = false,
            sto_create_date = DateTime.UtcNow
        });
        await mikroDb.SaveChangesAsync();

        var orderDate = new DateTime(2026, 11, 25);
        var service = CreateService(
            authDb,
            mikroDb,
            furpaDb,
            new FakeLabelStatsReader([
                new GreenGrocerLabelObservation(orderDate.AddDays(-1), 20, 2)
            ]),
            requireOrderUserManualApproval: false);

        var result = await service.PreviewResolutionAsync(
            new GreenGrocerProductCaseResolutionRequest("016181", 3, 56, 101, orderDate),
            CancellationToken.None);

        Assert.True(result.IsUsable);
        Assert.Equal(GreenGrocerProductCaseModes.Tier3, result.CalculationTier);
        Assert.False(result.RequiresManualApproval);
        Assert.Equal(30, result.EstimatedQuantity);
    }

    private static GreenGrocerProductCaseService CreateService(
        AuthDbContext authDb,
        MikroDbContext mikroDb,
        FurpaDbContext furpaDb,
        IGreenGrocerLabelStatsReader? labelStatsReader = null,
        bool profileStorageEnabled = true,
        bool requireOrderUserManualApproval = false) =>
        new(
            authDb,
            mikroDb,
            furpaDb,
            new SystemClock(),
            new StaticOptionsMonitor<GreenGrocerProductCaseOptions>(new GreenGrocerProductCaseOptions
            {
                Enabled = true,
                OrderLinkingEnabled = true,
                RequireOrderUserManualApproval = requireOrderUserManualApproval,
                AuthDbProfileStorageEnabled = profileStorageEnabled,
                AuthDbTrackingEnabled = true,
                SourceWarehouseNo = 56
            }),
            labelStatsReader);

    private static AuthDbContext CreateAuthDbContext() => new(
        new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"green-grocer-case-auth-{Guid.NewGuid():N}")
            .Options);

    private static MikroDbContext CreateMikroDbContext() => new(
        new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"green-grocer-case-mikro-{Guid.NewGuid():N}")
            .Options);

    private static FurpaDbContext CreateFurpaDbContext() => new(
        new DbContextOptionsBuilder<FurpaDbContext>()
            .UseInMemoryDatabase($"green-grocer-case-furpa-{Guid.NewGuid():N}")
            .Options);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeLabelStatsReader(IReadOnlyList<GreenGrocerLabelObservation> observations)
        : IGreenGrocerLabelStatsReader
    {
        public Task<IReadOnlyList<GreenGrocerLabelObservation>> ReadObservationsAsync(
            string stockCode,
            DateTime orderDate,
            int windowDays,
            CancellationToken cancellationToken) =>
            Task.FromResult(observations);
    }
}
