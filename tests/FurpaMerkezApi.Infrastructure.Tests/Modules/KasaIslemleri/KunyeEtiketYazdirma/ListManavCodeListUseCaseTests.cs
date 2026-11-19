using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed class ListManavCodeListUseCaseTests
{
    [Fact]
    public void QuerySql_ContainsDynamicMnvAndMnvsfMatchingAndGroupingRules()
    {
        var sql = ListManavCodeListUseCase.QuerySql;

        Assert.Contains("S.sto_isim LIKE 'MNV %'", sql);
        Assert.Contains("S.sto_isim LIKE '.MNVSF %'", sql);
        Assert.Contains("ISNULL(S.sto_iptal, 0) = 0", sql);
        Assert.Contains("ISNULL(S.sto_pasif_fl, 0) = 0", sql);
        Assert.Contains("ISNULL(B.bar_iptal, 0) = 0", sql);
        Assert.Contains("B.bar_master = 1", sql);
        Assert.Contains("B.bar_birimpntr = 1", sql);
        Assert.Contains("LEN(B.bar_kodu) = 7", sql);
        Assert.Contains("LEFT(B.bar_kodu, 2) IN ('27', '29')", sql);
        Assert.Contains("TRY_CONVERT(INT, RIGHT(B.bar_kodu, 5))", sql);
        Assert.Contains("COLLATE Turkish_CI_AS AS EslesmeAdi", sql);
        Assert.Contains("'03.02.01.01'", sql);
        Assert.Contains("'03.02.01.04'", sql);
        Assert.Contains("'03.02.01.02'", sql);
        Assert.Contains("'03.02.01.03'", sql);
        Assert.Contains("WHERE M.UrunTipi = 'MNV'", sql);
    }

    [Fact]
    public void QuerySql_DoesNotDuplicateRowsForMultipleBarcodesOrMnvsfCards()
    {
        var sql = ListManavCodeListUseCase.QuerySql;

        // Her stok karti icin tek barkod.
        Assert.Contains("PARTITION BY S.sto_kod", sql);
        Assert.Contains("WHERE BarkodSirasi = 1", sql);

        // MNVSF eslesmesi satir cogaltan LEFT JOIN yerine TOP (1) + eslesme sayisi ile yapilir.
        Assert.DoesNotContain("LEFT JOIN Urunler SF", sql);
        Assert.Contains("OUTER APPLY", sql);
        Assert.Contains("SELECT TOP (1)", sql);
        Assert.Contains("COUNT(1) OVER () AS EslesmeSayisi", sql);
        Assert.Contains("AS MnvsfMatchCount", sql);
    }

    [Fact]
    public void QuerySql_IsReadOnly()
    {
        var sql = ListManavCodeListUseCase.QuerySql.ToUpperInvariant();

        Assert.DoesNotContain("INSERT ", sql);
        Assert.DoesNotContain("UPDATE ", sql);
        Assert.DoesNotContain("DELETE ", sql);
        Assert.DoesNotContain("MERGE ", sql);
    }
}
