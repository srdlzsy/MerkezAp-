using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.GreenGrocer.Reports;

public sealed class GreenGrocerReportsUseCaseTests
{
    [Fact]
    public async Task GetByBranchAsync_TakeLimitsDocumentsWithoutTruncatingTheirLines()
    {
        await using var mikroDb = CreateMikroDbContext();
        await using var authDb = CreateAuthDbContext();
        SeedReportData(mikroDb);
        await mikroDb.SaveChangesAsync();
        var useCase = new GreenGrocerReportsUseCase(mikroDb, authDb);

        var result = await useCase.GetByBranchAsync(
            new GreenGrocerReportDateRequest(new DateTime(2026, 10, 20), Take: 1),
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(101, item.BranchNo);
            Assert.Equal(1, item.DocumentOrderNo);
        });
    }

    [Fact]
    public async Task GetGreensAsync_TakeLimitsDocumentsWithoutTruncatingTheirLines()
    {
        await using var mikroDb = CreateMikroDbContext();
        await using var authDb = CreateAuthDbContext();
        SeedReportData(mikroDb);
        await mikroDb.SaveChangesAsync();
        var useCase = new GreenGrocerReportsUseCase(mikroDb, authDb);

        var result = await useCase.GetGreensAsync(
            new GreenGrocerReportDateRequest(new DateTime(2026, 10, 20), Take: 1),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, item =>
        {
            Assert.Equal(101, item.BranchNo);
            Assert.Equal(1, item.DocumentOrderNo);
        });
    }

    private static void SeedReportData(MikroDbContext dbContext)
    {
        dbContext.DEPOLARs.AddRange(
            CreateBranch(101, "A SUBESI"),
            CreateBranch(102, "B SUBESI"));
        dbContext.STOKLARs.AddRange(
            CreateProduct("MNV-1", "YESILLIK 1"),
            CreateProduct("MNV-2", "YESILLIK 2"));
        dbContext.DEPOLAR_ARASI_SIPARISLERs.AddRange(
            CreateOrder(101, "F101", 1, 0, "MNV-1"),
            CreateOrder(101, "F101", 1, 1, "MNV-2"),
            CreateOrder(102, "F102", 2, 0, "MNV-1"));
    }

    private static DEPOLAR CreateBranch(int branchNo, string branchName) => new()
    {
        dep_Guid = Guid.NewGuid(),
        dep_no = branchNo,
        dep_adi = branchName,
        dep_tipi = 1,
        dep_create_date = DateTime.UtcNow
    };

    private static STOKLAR CreateProduct(string stockCode, string stockName) => new()
    {
        sto_Guid = Guid.NewGuid(),
        sto_kod = stockCode,
        sto_isim = stockName,
        sto_kisa_ismi = stockName,
        sto_model_kodu = "12",
        sto_birim1_ad = "ADET",
        sto_create_date = DateTime.UtcNow
    };

    private static DEPOLAR_ARASI_SIPARISLER CreateOrder(
        int branchNo,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        string stockCode) => new()
    {
        ssip_Guid = Guid.NewGuid(),
        ssip_iptal = false,
        ssip_tarih = new DateTime(2026, 10, 20),
        ssip_evrakno_seri = documentSerie,
        ssip_evrakno_sira = documentOrderNo,
        ssip_satirno = rowNo,
        ssip_stok_kod = stockCode,
        ssip_miktar = 1,
        ssip_girdepo = branchNo,
        ssip_create_date = DateTime.UtcNow
    };

    private static MikroDbContext CreateMikroDbContext() => new(
        new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"green-grocer-report-mikro-{Guid.NewGuid():N}")
            .Options);

    private static AuthDbContext CreateAuthDbContext() => new(
        new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"green-grocer-report-auth-{Guid.NewGuid():N}")
            .Options);
}
