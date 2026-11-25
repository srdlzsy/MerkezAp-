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

    private static GreenGrocerProductCaseService CreateService(
        AuthDbContext authDb,
        MikroDbContext mikroDb,
        FurpaDbContext furpaDb) =>
        new(
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
}
