using FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services.MikroApi;

public sealed class MikroApiNullPayloadMappingTests
{
    [Fact]
    public void ShippingUpdate_OmitsNullFieldsAndPreservesExplicitDefaults()
    {
        var row = new
        {
            sth_Guid = Guid.Parse("8c5ee6ca-967a-4555-a0fb-30d58b361155"),
            sth_miktar = (double?)null,
            sth_tutar = (double?)0d,
            sth_vergisiz_fl = (bool?)false
        };

        var result = UpdateWarehouseShippingDocumentUseCase.ToShippingApiRow(row);

        Assert.DoesNotContain("sth_miktar", result.Keys);
        Assert.Equal(0d, result["sth_tutar"]);
        Assert.Equal(false, result["sth_vergisiz_fl"]);
    }

    [Fact]
    public void GenericInsert_OmitsNullFieldsAndPreservesExplicitDefaults()
    {
        var row = new
        {
            sdp_depo_kod = "015550",
            sdp_min_stok = (double?)null,
            sdp_max_stok = (double?)0d,
            sdp_sipdursun = (byte?)0
        };

        var result = MikroDocumentEditingService.BuildInsertRecord(row, "10");

        Assert.Equal("10", result["TabloNo"]);
        Assert.Equal("0", result["KayitTipi"]);
        Assert.DoesNotContain("sdp_min_stok", result.Keys);
        Assert.Equal(0d, result["sdp_max_stok"]);
        Assert.Equal((byte)0, result["sdp_sipdursun"]);
    }

    [Fact]
    public void StockMovementUpdate_MapsOnlyGuidConcurrencyAndChangedFields()
    {
        var guid = Guid.Parse("3c104822-d47a-443c-9c95-becff2f1f2e4");
        var lastUpdate = new DateTime(2026, 9, 8, 10, 37, 4, 137);
        var row = new
        {
            sth_Guid = guid,
            sth_lastup_date = lastUpdate,
            sth_HareketGrupKodu1 = "yeni-kod",
            sth_miktar = (double?)null,
            sth_degisti = true
        };
        var original = new Dictionary<string, object?>
        {
            ["sth_Guid"] = guid,
            ["sth_lastup_date"] = lastUpdate,
            ["sth_HareketGrupKodu1"] = string.Empty,
            ["sth_miktar"] = 5d,
            ["sth_degisti"] = false
        };

        var result = MikroDocumentEditingService.BuildStockMovementUpdateApiRow(row, original);

        Assert.Equal(guid, result["sth_Guid"]);
        Assert.Equal("2026-09-08T10:37:04.137", result["sth_lastup_date"]);
        Assert.Equal("yeni-kod", result["sth_HareketGrupKodu1"]);
        Assert.DoesNotContain("sth_miktar", result.Keys);
        Assert.DoesNotContain("sth_degisti", result.Keys);
    }
}
