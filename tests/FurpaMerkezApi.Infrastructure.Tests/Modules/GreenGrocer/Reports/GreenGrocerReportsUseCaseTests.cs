using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var useCase = new GreenGrocerReportsUseCase(
            mikroDb,
            authDb,
            NullLogger<GreenGrocerReportsUseCase>.Instance);

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
        var useCase = new GreenGrocerReportsUseCase(
            mikroDb,
            authDb,
            NullLogger<GreenGrocerReportsUseCase>.Instance);

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

    [Fact]
    public async Task GetGreensAsync_WhenProductCasesEnabled_ShowsStockCaseFactorForPieceProducts()
    {
        await using var mikroDb = CreateMikroDbContext();
        await using var authDb = CreateAuthDbContext();
        mikroDb.DEPOLARs.Add(CreateBranch(101, "A SUBESI"));
        mikroDb.STOKLARs.Add(CreateProduct(
            "YSL-1",
            "MAYDANOZ",
            unit1: "ADET",
            unit2: "KOLI",
            unit2Factor: 25d));
        mikroDb.DEPOLAR_ARASI_SIPARISLERs.Add(CreateOrder(
            101,
            "F101",
            1,
            0,
            "YSL-1",
            quantity: 30d));
        await mikroDb.SaveChangesAsync();
        var useCase = CreateUseCase(mikroDb, authDb, enabled: true);

        var result = await useCase.GetGreensAsync(
            new GreenGrocerReportDateRequest(new DateTime(2026, 10, 20)),
            CancellationToken.None);

        var caseInfo = Assert.Single(result).CaseInfo;
        Assert.NotNull(caseInfo);
        Assert.Equal(25d, caseInfo.UnitsPerCase);
        Assert.Equal(GreenGrocerProductCaseModes.AverageSourceStockUnitFactor, caseInfo.AverageSource);
    }

    [Fact]
    public async Task GetGreensAsync_WhenProductCasesEnabled_ShowsLiveLabelAverageForKgProducts()
    {
        await using var mikroDb = CreateMikroDbContext();
        await using var authDb = CreateAuthDbContext();
        mikroDb.DEPOLARs.Add(CreateBranch(101, "A SUBESI"));
        mikroDb.STOKLARs.Add(CreateProduct("YSL-KG", "ISPANAK", unit1: "KG"));
        mikroDb.DEPOLAR_ARASI_SIPARISLERs.Add(CreateOrder(
            101,
            "F101",
            1,
            0,
            "YSL-KG",
            quantity: 24d));
        await mikroDb.SaveChangesAsync();
        var observations = new[]
        {
            new GreenGrocerLabelObservation(new DateTime(2026, 10, 19), 12d, 2d),
            new GreenGrocerLabelObservation(new DateTime(2026, 10, 18), 18d, 3d)
        };
        var useCase = CreateUseCase(mikroDb, authDb, enabled: true, observations);

        var result = await useCase.GetGreensAsync(
            new GreenGrocerReportDateRequest(new DateTime(2026, 10, 20)),
            CancellationToken.None);

        var caseInfo = Assert.Single(result).CaseInfo;
        Assert.NotNull(caseInfo);
        Assert.Equal(6d, caseInfo.AverageKgPerCase);
        Assert.Equal(4d, caseInfo.InputQuantity);
        Assert.Equal(GreenGrocerProductCaseModes.AverageSourceLabelHistory, caseInfo.AverageSource);
    }

    [Fact]
    public async Task GetGreensAsync_WhenProductCasesDisabled_DoesNotAddLiveCaseAverage()
    {
        await using var mikroDb = CreateMikroDbContext();
        await using var authDb = CreateAuthDbContext();
        mikroDb.DEPOLARs.Add(CreateBranch(101, "A SUBESI"));
        mikroDb.STOKLARs.Add(CreateProduct(
            "YSL-1",
            "MAYDANOZ",
            unit1: "ADET",
            unit2: "KOLI",
            unit2Factor: 25d));
        mikroDb.DEPOLAR_ARASI_SIPARISLERs.Add(CreateOrder(101, "F101", 1, 0, "YSL-1"));
        await mikroDb.SaveChangesAsync();
        var useCase = CreateUseCase(mikroDb, authDb, enabled: false);

        var result = await useCase.GetGreensAsync(
            new GreenGrocerReportDateRequest(new DateTime(2026, 10, 20)),
            CancellationToken.None);

        Assert.Null(Assert.Single(result).CaseInfo);
    }

    [Fact]
    public async Task Reports_OrderProductsByMeyveSebzeYesillikThenName()
    {
        await using var mikroDb = CreateMikroDbContext();
        await using var authDb = CreateAuthDbContext();
        mikroDb.DEPOLARs.Add(CreateBranch(101, "A SUBESI"));
        mikroDb.STOKLARs.AddRange(
            CreateProduct("YSL-1", "MAYDANOZ", "12"),
            CreateProduct("SBZ-1", "DOMATES", "11"),
            CreateProduct("MYV-2", "ELMA", "10"),
            CreateProduct("MYV-1", "ARMUT", "10"));
        mikroDb.DEPOLAR_ARASI_SIPARISLERs.AddRange(
            CreateOrder(101, "F101", 1, 0, "YSL-1"),
            CreateOrder(101, "F101", 1, 1, "SBZ-1"),
            CreateOrder(101, "F101", 1, 2, "MYV-2"),
            CreateOrder(101, "F101", 1, 3, "MYV-1"));
        await mikroDb.SaveChangesAsync();
        var useCase = new GreenGrocerReportsUseCase(
            mikroDb,
            authDb,
            NullLogger<GreenGrocerReportsUseCase>.Instance);
        var request = new GreenGrocerReportDateRequest(new DateTime(2026, 10, 20));

        var branch = await useCase.GetByBranchAsync(request, CancellationToken.None);
        var products = await useCase.GetByProductAsync(request, CancellationToken.None);

        Assert.Equal(["10", "10", "11", "12"], branch.Items.Select(item => item.TypeCode));
        Assert.Equal(["ARMUT", "ELMA", "DOMATES", "MAYDANOZ"], branch.Items.Select(item => item.ProductName));
        Assert.Equal(["10", "10", "11", "12"], products.Select(item => item.TypeCode));
        Assert.Equal(["ARMUT", "ELMA", "DOMATES", "MAYDANOZ"], products.Select(item => item.ProductName));
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

    private static STOKLAR CreateProduct(
        string stockCode,
        string stockName,
        string modelCode = "12",
        string unit1 = "ADET",
        string? unit2 = null,
        double? unit2Factor = null) => new()
    {
        sto_Guid = Guid.NewGuid(),
        sto_kod = stockCode,
        sto_isim = stockName,
        sto_kisa_ismi = stockName,
        sto_model_kodu = modelCode,
        sto_birim1_ad = unit1,
        sto_birim2_ad = unit2,
        sto_birim2_katsayi = unit2Factor,
        sto_create_date = DateTime.UtcNow
    };

    private static DEPOLAR_ARASI_SIPARISLER CreateOrder(
        int branchNo,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        string stockCode,
        double quantity = 1d) => new()
    {
        ssip_Guid = Guid.NewGuid(),
        ssip_iptal = false,
        ssip_tarih = new DateTime(2026, 10, 20),
        ssip_evrakno_seri = documentSerie,
        ssip_evrakno_sira = documentOrderNo,
        ssip_satirno = rowNo,
        ssip_stok_kod = stockCode,
        ssip_miktar = quantity,
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

    private static GreenGrocerReportsUseCase CreateUseCase(
        MikroDbContext mikroDb,
        AuthDbContext authDb,
        bool enabled,
        IReadOnlyList<GreenGrocerLabelObservation>? observations = null) =>
        new(
            mikroDb,
            authDb,
            NullLogger<GreenGrocerReportsUseCase>.Instance,
            new StaticOptionsMonitor<GreenGrocerProductCaseOptions>(new GreenGrocerProductCaseOptions
            {
                Enabled = enabled,
                AuthDbTrackingEnabled = false
            }),
            new FakeLabelStatsReader(observations ?? []));

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
