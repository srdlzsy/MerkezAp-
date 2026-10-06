using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.FirmaEvrakTakibi;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.FirmaEvrakTakibi;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.FirmaEvrakTakibi;

public sealed class CompanyDocumentTrackingServiceTests
{
    [Fact]
    public async Task GetAsync_CombinesReceivingsByCreateDateAndReturnsByDocumentDate()
    {
        await using var dbContext = CreateDbContext();
        var selectedDate = new DateOnly(2026, 10, 6);
        var selectedDay = selectedDate.ToDateTime(TimeOnly.MinValue);
        var generatedAtUtc = new DateTime(2026, 10, 6, 9, 30, 0, DateTimeKind.Utc);

        dbContext.CARI_HESAPLARs.AddRange(
            CreateCustomer("320001", "TEDARIKCI", "ANONIM"),
            CreateCustomer("320002", "IADE CARI", "LIMITED"));
        dbContext.DEPOLARs.Add(CreateWarehouse(149, "DEPO 149"));
        dbContext.STOK_HAREKETLERIs.AddRange(
            CreateReceivingLine(0, selectedDay.AddHours(8), selectedDay.AddDays(-2), 4d),
            CreateReceivingLine(1, selectedDay.AddHours(8).AddSeconds(1), selectedDay.AddDays(-2), 6d),
            CreateReturnLine(selectedDay.AddDays(-1), selectedDay, 3d),
            CreateReceivingLine(0, selectedDay.AddDays(-1), selectedDay, 100d, documentOrderNo: 99));
        await dbContext.SaveChangesAsync();

        var service = new CompanyDocumentTrackingService(dbContext, new FixedClock(generatedAtUtc));

        var result = await service.GetAsync(
            new CompanyDocumentTrackingRequest(selectedDate, 149),
            CancellationToken.None);

        Assert.Equal(generatedAtUtc, result.GeneratedAtUtc);
        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(1, result.CompanyReceivingCount);
        Assert.Equal(1, result.CompanyReturnCount);

        var receiving = Assert.Single(result.Items, item => item.DocumentKind == "CompanyReceiving");
        Assert.Equal("F149/10", receiving.DocumentNo);
        Assert.Equal(2, receiving.LineCount);
        Assert.Equal(10d, receiving.TotalQuantity);
        Assert.Equal("TESLIM EDEN", receiving.Deliverer);
        Assert.Equal("TESLIM ALAN", receiving.Receiver);
        Assert.Equal("TEDARIKCI ANONIM", receiving.CustomerDisplayName);

        var companyReturn = Assert.Single(result.Items, item => item.DocumentKind == "CompanyReturn");
        Assert.Equal("IR149", companyReturn.DocumentSerie);
        Assert.Equal(selectedDay, companyReturn.DocumentDate);
        Assert.Equal(149, companyReturn.WarehouseNo);
    }

    [Fact]
    public async Task GetAsync_DoesNotDependOnLegacyCreatorOrCustomerAddressFilters()
    {
        await using var dbContext = CreateDbContext();
        var selectedDate = new DateOnly(2026, 10, 6);
        var selectedDay = selectedDate.ToDateTime(TimeOnly.MinValue);

        dbContext.CARI_HESAPLARs.Add(CreateCustomer("320001", "ADRESSIZ", "CARI"));
        dbContext.DEPOLARs.Add(CreateWarehouse(149, "DEPO 149"));
        var movement = CreateReceivingLine(0, selectedDay.AddHours(10), selectedDay, 1d);
        movement.sth_create_user = 77;
        movement.sth_adres_no = 9;
        dbContext.STOK_HAREKETLERIs.Add(movement);
        await dbContext.SaveChangesAsync();

        var service = new CompanyDocumentTrackingService(
            dbContext,
            new FixedClock(DateTime.UtcNow));

        var result = await service.GetAsync(
            new CompanyDocumentTrackingRequest(selectedDate, 149),
            CancellationToken.None);

        Assert.Single(result.Items);
    }

    private static STOK_HAREKETLERI CreateReceivingLine(
        int lineNo,
        DateTime createDate,
        DateTime documentDate,
        double quantity,
        int documentOrderNo = 10) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_user = 77,
            sth_create_date = createDate,
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_evraktip = 13,
            sth_tip = 0,
            sth_normal_iade = 0,
            sth_evrakno_seri = "F149",
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = lineNo,
            sth_stok_kod = $"STK{lineNo}",
            sth_miktar = quantity,
            sth_cari_kodu = "320001",
            sth_giris_depo_no = 149,
            sth_cikis_depo_no = 1,
            sth_HareketGrupKodu2 = "TESLIM EDEN",
            sth_HareketGrupKodu3 = "TESLIM ALAN"
        };

    private static STOK_HAREKETLERI CreateReturnLine(
        DateTime createDate,
        DateTime documentDate,
        double quantity) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_create_user = 88,
            sth_create_date = createDate,
            sth_tarih = documentDate,
            sth_belge_tarih = documentDate,
            sth_evraktip = 1,
            sth_tip = 1,
            sth_normal_iade = 1,
            sth_evrakno_seri = "IR149",
            sth_evrakno_sira = 20,
            sth_satirno = 0,
            sth_belge_no = "IADE-20",
            sth_stok_kod = "STK9",
            sth_miktar = quantity,
            sth_cari_kodu = "320002",
            sth_giris_depo_no = 1,
            sth_cikis_depo_no = 149,
            sth_HareketGrupKodu2 = "IADE EDEN",
            sth_HareketGrupKodu3 = "IADE ALAN"
        };

    private static CARI_HESAPLAR CreateCustomer(string code, string name, string title) =>
        new()
        {
            cari_Guid = Guid.NewGuid(),
            cari_create_date = new DateTime(2020, 1, 1),
            cari_kod = code,
            cari_unvan1 = name,
            cari_unvan2 = title
        };

    private static DEPOLAR CreateWarehouse(int warehouseNo, string warehouseName) =>
        new()
        {
            dep_Guid = Guid.NewGuid(),
            dep_create_date = new DateTime(2020, 1, 1),
            dep_no = warehouseNo,
            dep_adi = warehouseName
        };

    private static MikroDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"company-document-tracking-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
