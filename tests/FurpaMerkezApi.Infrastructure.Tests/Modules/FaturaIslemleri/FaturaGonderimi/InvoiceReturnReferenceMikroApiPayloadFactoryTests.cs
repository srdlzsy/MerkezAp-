using System.Text.Json;
using FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class InvoiceReturnReferenceMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_ExistingRow_BuildsPartialUpdate()
    {
        var rowGuid = Guid.NewGuid();
        var invoiceGuid = Guid.NewGuid();

        var payload = InvoiceReturnReferenceMikroApiPayloadFactory.Create(
            rowGuid,
            invoiceGuid,
            "FRP20260000123",
            new DateTime(2026, 9, 10));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var row = json.RootElement.GetProperty("Kayit")[0];

        Assert.Equal("597", row.GetProperty("TabloNo").GetString());
        Assert.Equal("1", row.GetProperty("KayitTipi").GetString());
        Assert.Equal(rowGuid, row.GetProperty("ebh_Guid").GetGuid());
        Assert.Equal(invoiceGuid, row.GetProperty("ebh_related_uid").GetGuid());
        Assert.Equal("FRP20260000123", row.GetProperty("ebh_iade_fat_no1").GetString());
        Assert.Equal("20260910", row.GetProperty("ebh_iade_fat_tarihi1").GetString());
        Assert.False(row.TryGetProperty("ebh_odeme_sekli", out _));
    }

    [Fact]
    public void Create_MissingRow_BuildsInsertWithRequiredDefaults()
    {
        var invoiceGuid = Guid.NewGuid();

        var payload = InvoiceReturnReferenceMikroApiPayloadFactory.Create(
            null,
            invoiceGuid,
            " FRP20260000456 ",
            null);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var row = json.RootElement.GetProperty("Kayit")[0];

        Assert.Equal("0", row.GetProperty("KayitTipi").GetString());
        Assert.NotEqual(Guid.Empty, row.GetProperty("ebh_Guid").GetGuid());
        Assert.Equal(1, row.GetProperty("ebh_hareket_tipi").GetInt32());
        Assert.Equal("FRP20260000456", row.GetProperty("ebh_iade_fat_no1").GetString());
        Assert.Equal(string.Empty, row.GetProperty("ebh_iade_fat_tarihi1").GetString());
        Assert.Equal(0, row.GetProperty("ebh_odeme_sekli").GetInt32());
        Assert.Equal("18991230", row.GetProperty("ebh_mukellefiyetdonembasi").GetString());
    }
}
