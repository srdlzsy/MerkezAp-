using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed class ListManavCodeListUseCase(MikroDbContext mikroDbContext)
    : IListManavCodeListUseCase
{
    internal const string QuerySql = """
        ;WITH UrunAdaylari AS
        (
            SELECT
                S.sto_kod AS StokKodu,
                S.sto_isim AS UrunIsmi,
                S.sto_kategori_kodu AS KategoriKodu,
                TRY_CONVERT(INT, RIGHT(B.bar_kodu, 5)) AS Kod,

                CASE
                    WHEN S.sto_isim LIKE '.MNVSF %' THEN 'MNVSF'
                    ELSE 'MNV'
                END AS UrunTipi,

                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(S.sto_isim, '.MNVSF ', ''),
                                'MNV ', ''
                            ), ' ', ''
                        ), '(', ''
                    ), ')', ''
                ) COLLATE Turkish_CI_AS AS EslesmeAdi,

                -- Ayni stok kartinda birden fazla uygun barkod varsa satir cogalmasin.
                ROW_NUMBER() OVER
                (
                    PARTITION BY S.sto_kod
                    ORDER BY B.bar_kodu
                ) AS BarkodSirasi

            FROM dbo.STOKLAR S

            INNER JOIN dbo.BARKOD_TANIMLARI B
                ON B.bar_stokkodu = S.sto_kod

            WHERE (
                    S.sto_isim LIKE 'MNV %'
                    OR S.sto_isim LIKE '.MNVSF %'
                  )
              AND ISNULL(S.sto_iptal, 0) = 0
              AND ISNULL(S.sto_pasif_fl, 0) = 0
              AND ISNULL(B.bar_iptal, 0) = 0
              AND B.bar_master = 1
              AND B.bar_birimpntr = 1
              AND LEN(B.bar_kodu) = 7
              AND LEFT(B.bar_kodu, 2) IN ('27', '29')
              AND TRY_CONVERT(INT, RIGHT(B.bar_kodu, 5)) IS NOT NULL
        ),
        Urunler AS
        (
            SELECT StokKodu, UrunIsmi, KategoriKodu, Kod, UrunTipi, EslesmeAdi
            FROM UrunAdaylari
            WHERE BarkodSirasi = 1
        )
        SELECT
            CASE
                WHEN M.KategoriKodu IN (
                    '03.02.01.01',
                    '03.02.01.04'
                ) THEN N'MEYVE ÇEŞİTLERİ'

                WHEN M.KategoriKodu IN (
                    '03.02.01.02',
                    '03.02.01.03'
                ) THEN N'SEBZE ÇEŞİTLERİ'

                ELSE N'DİĞER'
            END AS ProductGroup,

            M.UrunIsmi AS ProductName,
            M.Kod AS Code,
            SF.Kod AS MnvsfCode,
            M.StokKodu AS StockCode,
            SF.StokKodu AS MnvsfStockCode,
            COALESCE(M.KategoriKodu, '') AS CategoryCode,
            COALESCE(SF.EslesmeSayisi, 0) AS MnvsfMatchCount

        FROM Urunler M

        -- Ayni isimde birden fazla MNVSF karti varsa MNV satiri cogaltilmaz;
        -- en kucuk kod gosterilir ve eslesme sayisi kontrol icin dondurulur.
        OUTER APPLY
        (
            SELECT TOP (1)
                X.Kod,
                X.StokKodu,
                COUNT(1) OVER () AS EslesmeSayisi
            FROM Urunler X
            WHERE X.UrunTipi = 'MNVSF'
              AND X.EslesmeAdi = M.EslesmeAdi
            ORDER BY X.Kod, X.StokKodu
        ) SF

        WHERE M.UrunTipi = 'MNV'

        ORDER BY
            CASE
                WHEN M.KategoriKodu IN ('03.02.01.01', '03.02.01.04') THEN 1
                WHEN M.KategoriKodu IN ('03.02.01.02', '03.02.01.03') THEN 2
                ELSE 3
            END,
            M.UrunIsmi;
        """;

    public async Task<IReadOnlyCollection<ManavCodeListItemDto>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        var items = new List<ManavCodeListItemDto>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = QuerySql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 120;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new ManavCodeListItemDto(
                    ReadString(reader, "ProductGroup"),
                    ReadString(reader, "ProductName"),
                    ReadInt(reader, "Code"),
                    ReadNullableInt(reader, "MnvsfCode"),
                    ReadString(reader, "StockCode"),
                    ReadNullableString(reader, "MnvsfStockCode"),
                    ReadString(reader, "CategoryCode"),
                    ReadInt(reader, "MnvsfMatchCount")));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return items;
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name]);

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static string? ReadNullableString(DbDataReader reader, string name)
    {
        if (reader[name] is DBNull)
        {
            return null;
        }

        var value = Convert.ToString(reader[name]);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
