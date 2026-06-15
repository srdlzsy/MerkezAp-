using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed class ListKunyeLabelTagsUseCase(MikroDbContext mikroDbContext)
    : IListKunyeLabelTagsUseCase
{
    public async Task<IReadOnlyCollection<KunyeLabelTagDto>> ExecuteAsync(
        KunyeLabelTagListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var date = request.DateToGet?.Date;
        var nextDate = date?.AddDays(1);
        var tags = new List<KunyeLabelTagDto>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                ;WITH SonKunye AS
                (
                    SELECT
                        fi.StokId,
                        fi.Aciklama AS FaturaAciklama,
                        fi.BirimAdi AS FaturaBirimAdi,
                        vw.BranchNo,
                        vw.BranchName,
                        vw.ProductionCity,
                        vw.ProductionDistrict,
                        vw.ProductName,
                        vw.GoodsType,
                        vw.GoodsGenus,
                        vw.Quantity,
                        vw.TakenTag,
                        vw.Buyer,
                        vw.ProductionDate,
                        vw.BuyingPrice,
                        vw.ShippingDate,
                        vw.Manufacturer,
                        vw.ProductUnit,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY fi.StokId
                            ORDER BY vw.ShippingDate DESC
                        ) AS RN
                    FROM [KUNYENET].[dbo].[FaturaIslem] fi WITH (NOLOCK)
                    INNER JOIN [Furpa].[dbo].[VwKunyeNet] vw WITH (NOLOCK)
                        ON vw.TakenTag = fi.Kunye
                    WHERE
                        vw.BranchNo = @warehouseNo
                        AND (
                            (
                                @date IS NOT NULL
                                AND vw.[ShippingDate] >= @date
                                AND vw.[ShippingDate] < @nextDate
                            )
                            OR (
                                @date IS NULL
                                AND vw.[ShippingDate] >= DATEADD(MONTH, -1, CAST(SYSDATETIME() AS date))
                                AND vw.[ShippingDate] < DATEADD(DAY, 1, CAST(SYSDATETIME() AS date))
                            )
                        )
                )
                SELECT
                    sk.BranchNo,
                    COALESCE(sk.BranchName, '') AS BranchName,
                    COALESCE(sk.ProductionCity, '') AS ProductionCity,
                    s.sto_kod AS StockCode,
                    COALESCE(s.sto_isim, '') AS StockName,
                    dbo.[fn_StokSatisFiyati](s.sto_kod, '1', sk.BranchNo, '1') AS SalesPrice,
                    COALESCE(sk.ProductionDistrict, '') AS ProductionDistrict,
                    COALESCE(sk.ProductName, '') AS ProductName,
                    COALESCE(sk.GoodsType, '') AS GoodsType,
                    COALESCE(sk.GoodsGenus, '') AS GoodsGenus,
                    sk.Quantity,
                    COALESCE(sk.TakenTag, '') AS TakenTag,
                    COALESCE(sk.Buyer, '') AS Buyer,
                    sk.ProductionDate,
                    sk.BuyingPrice,
                    sk.ShippingDate,
                    COALESCE(sk.Manufacturer, '') AS Manufacturer,
                    COALESCE(sk.ProductUnit, '') AS ProductUnit
                FROM [dbo].[STOKLAR] s WITH (NOLOCK)
                INNER JOIN [KUNYENET].[dbo].[MuhStok] ms WITH (NOLOCK)
                    ON ms.StokKodu = s.sto_kod
                INNER JOIN SonKunye sk
                    ON sk.StokId = ms.Stokid
                    AND sk.RN = 1
                WHERE s.sto_model_kodu IN ('10', '11', '12')
                    AND ISNULL(s.sto_satis_dursun, 0) = 0
                    AND ISNULL(s.sto_siparis_dursun, 0) = 0
                    AND ISNULL(s.sto_malkabul_dursun, 0) = 0
                    AND ISNULL(s.sto_pasif_fl, 0) = 0
                    AND LTRIM(RTRIM(ISNULL(s.sto_isim, ''))) = LTRIM(RTRIM(ISNULL(sk.FaturaAciklama, '')))
                    AND UPPER(LTRIM(RTRIM(ISNULL(s.sto_birim1_ad, '')))) = UPPER(LTRIM(RTRIM(ISNULL(sk.FaturaBirimAdi, ''))))
                ORDER BY s.sto_isim;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            AddParameter(command, "@date", date, DbType.DateTime2);
            AddParameter(command, "@nextDate", nextDate, DbType.DateTime2);
            AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                tags.Add(new KunyeLabelTagDto(
                    ReadInt(reader, "BranchNo"),
                    ReadString(reader, "BranchName"),
                    ReadString(reader, "ProductionCity"),
                    ReadString(reader, "StockCode"),
                    ReadString(reader, "StockName"),
                    ReadDouble(reader, "SalesPrice"),
                    ReadString(reader, "ProductionDistrict"),
                    ReadString(reader, "ProductName"),
                    ReadString(reader, "GoodsType"),
                    ReadString(reader, "GoodsGenus"),
                    ReadDouble(reader, "Quantity"),
                    ReadString(reader, "TakenTag"),
                    ReadString(reader, "Buyer"),
                    ReadDateTime(reader, "ProductionDate"),
                    ReadDouble(reader, "BuyingPrice"),
                    ReadDateTime(reader, "ShippingDate"),
                    ReadString(reader, "Manufacturer"),
                    ReadString(reader, "ProductUnit")));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return tags;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType? dbType = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        if (dbType.HasValue)
        {
            parameter.DbType = dbType.Value;
        }

        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
}
