using FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
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
            sth_lastup_date = new DateTime(2026, 9, 8, 10, 37, 4, 137),
            sth_degisti = true,
            sth_miktar = (double?)null,
            sth_tutar = (double?)0d,
            sth_vergisiz_fl = (bool?)false
        };

        var result = UpdateWarehouseShippingDocumentUseCase.ToShippingApiRow(row);

        Assert.DoesNotContain("sth_miktar", result.Keys);
        Assert.DoesNotContain("sth_lastup_date", result.Keys);
        Assert.DoesNotContain("sth_degisti", result.Keys);
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
    public void StockMovementUpdate_MapsOnlyGuidAndChangedFields()
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
        Assert.Equal("yeni-kod", result["sth_HareketGrupKodu1"]);
        Assert.DoesNotContain("sth_lastup_date", result.Keys);
        Assert.DoesNotContain("sth_miktar", result.Keys);
        Assert.DoesNotContain("sth_degisti", result.Keys);
    }

    [Fact]
    public void PartialUpdate_MapsOnlyGuidAndChangedNonTechnicalFields()
    {
        var guid = Guid.Parse("c1217961-c164-42a6-9ec6-d3be96b8cf89");
        var originalRow = new
        {
            ssip_Guid = guid,
            ssip_miktar = 5d,
            ssip_tutar = 50d,
            ssip_lastup_date = new DateTime(2026, 9, 8, 11, 0, 0),
            ssip_degisti = false
        };
        var changedRow = new
        {
            ssip_Guid = guid,
            ssip_miktar = 7d,
            ssip_tutar = 50d,
            ssip_lastup_date = new DateTime(2026, 9, 8, 11, 1, 0),
            ssip_degisti = true
        };

        var result = MikroApiPartialUpdateRowMapper.Build(
            changedRow,
            MikroApiPartialUpdateRowMapper.Snapshot(originalRow),
            "ssip_Guid");

        Assert.Equal(guid, result["ssip_Guid"]);
        Assert.Equal(7d, result["ssip_miktar"]);
        Assert.DoesNotContain("ssip_tutar", result.Keys);
        Assert.DoesNotContain("ssip_lastup_date", result.Keys);
        Assert.DoesNotContain("ssip_degisti", result.Keys);
    }

    [Fact]
    public void PartialUpdate_ReturnsNullWhenOnlyTechnicalFieldsChanged()
    {
        var guid = Guid.Parse("6da92b05-e82e-45da-adc5-fbe62bdd2ebf");
        var originalRow = new { sip_Guid = guid, sip_miktar = 5d, sip_degisti = false };
        var changedRow = new { sip_Guid = guid, sip_miktar = 5d, sip_degisti = true };

        var result = MikroApiPartialUpdateRowMapper.TryBuild(
            changedRow,
            MikroApiPartialUpdateRowMapper.Snapshot(originalRow),
            "sip_Guid");

        Assert.Null(result);
    }
}
