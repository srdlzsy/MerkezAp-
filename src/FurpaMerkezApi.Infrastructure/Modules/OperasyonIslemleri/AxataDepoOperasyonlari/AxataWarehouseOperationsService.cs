using System.Data;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;

public sealed class AxataWarehouseOperationsService(
    IConfiguration configuration,
    IMemoryCache memoryCache)
    : IAxataWarehouseOperationsService
{
    private const int CommandTimeoutSeconds = 30;
    private const int MaximumReportDayCount = 31;

    private string CompanyCode => configuration["AxataWarehouseOperations:CompanyCode"] ?? "01";
    private string AxataWarehouseCode => configuration["AxataWarehouseOperations:WarehouseCode"] ?? "01";
    private int MikroWarehouseNo => configuration.GetValue("AxataWarehouseOperations:MikroWarehouseNo", 50);

    public async Task<AxataWarehouseOverviewDto> GetOverviewAsync(
        int? warehouseNo,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        ValidateWarehouse(warehouseNo);
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var dateValue = ToAxataDate(targetDate);
        var cacheKey = $"axata-warehouse-overview:{MikroWarehouseNo}:{dateValue}";

        if (memoryCache.TryGetValue(cacheKey, out AxataWarehouseOverviewDto? cached) && cached is not null)
        {
            return cached;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string countSql = """
            SELECT
                (SELECT COUNT(1) FROM dbo.SIPMASTER
                 WHERE SMSIRK=@company AND SMDAGK=@warehouse AND SMSPST=0 AND SM_ISCANCEL=0),
                (SELECT COUNT(DISTINCT CONCAT(CONVERT(int,TMSYIL),':',CONVERT(int,TMSEVK))) FROM dbo.TOPMASTER
                 WHERE TMSIRK=@company AND TMDEPO=@warehouse AND TMBAST=@date AND TMBITT=0),
                (SELECT COUNT(1) FROM dbo.SEVK_MASTER
                 WHERE SMSKOD=@company AND SMAKOD=@warehouse AND SMSTAT=6 AND SMYSTR=@date),
                (SELECT COALESCE(SUM(d.SDGMIK),0) FROM dbo.SEVK_MASTER m
                 INNER JOIN dbo.SEVK_DETAIL d ON d.SDSKOD=m.SMSKOD AND d.SDSYIL=m.SMSYIL AND d.SDSNO=m.SMSNO
                 WHERE m.SMSKOD=@company AND m.SMAKOD=@warehouse AND m.SMSTAT=6 AND m.SMYSTR=@date),
                (SELECT COUNT(1) FROM dbo.GSIPMST
                 WHERE CMSIRK=@company AND CMDEPO=@warehouse AND CMSTAT=0
                   AND NOT EXISTS (SELECT 1 FROM dbo.GSIPMASTER g
                     WHERE g.CSSKOD=CMSIRK AND g.CSHARK=CMHKOD AND g.CSBNUM=CMSIPN AND g.CSSTAT<>0)),
                (SELECT COUNT(1) FROM dbo.GSIPMST m
                 WHERE m.CMSIRK=@company AND m.CMDEPO=@warehouse AND m.CMSTAT=0
                   AND EXISTS (SELECT 1 FROM dbo.GSIPMASTER g
                     WHERE g.CSSKOD=m.CMSIRK AND g.CSHARK=m.CMHKOD AND g.CSBNUM=m.CMSIPN AND g.CSSTAT<>0)),
                (SELECT COUNT(1) FROM dbo.GSIPMST
                 WHERE CMSIRK=@company AND CMDEPO=@warehouse AND CMSTAT=4
                   AND (CONVERT(date,CMBITT)=@calendarDate OR CMTEST=@date)),
                (SELECT COUNT(1) FROM dbo.ENT006
                 WHERE S06SKOD=@company AND S06DEPO=@warehouse AND S06STAT=0 AND S06ITAR=@date),
                (SELECT COUNT(1) FROM dbo.ENT016_MST
                 WHERE S16SKOD=@company AND S16AKOD=@warehouse AND S16STAT=0 AND S16ITAR=@date)
            """;

        int pendingOrders;
        int pickingOrders;
        int shippedToday;
        decimal shippedQuantity;
        int pendingReceiving;
        int processingReceiving;
        int completedReceiving;
        int outboundQueue;
        int inboundQueue;
        await using (var command = CreateCommand(connection, countSql))
        {
            AddCommonParameters(command);
            command.Parameters.Add(new SqlParameter("@date", SqlDbType.Decimal) { Value = dateValue });
            command.Parameters.Add(new SqlParameter("@calendarDate", SqlDbType.Date) { Value = targetDate.ToDateTime(TimeOnly.MinValue) });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            pendingOrders = reader.GetInt32(0);
            pickingOrders = reader.GetInt32(1);
            shippedToday = reader.GetInt32(2);
            shippedQuantity = reader.GetDecimal(3);
            pendingReceiving = reader.GetInt32(4);
            processingReceiving = reader.GetInt32(5);
            completedReceiving = reader.GetInt32(6);
            outboundQueue = reader.GetInt32(7);
            inboundQueue = reader.GetInt32(8);
        }

        const string activeSql = """
            SELECT TOP (20) CONVERT(int,tm.TMSYIL),CONVERT(int,tm.TMSEVK),sm.SMAPLK,sm.SMBKOD,f.CMMUNV,
                   tm.TMRAMP,CONVERT(varchar(20),CONVERT(int,tm.TMPERS)),
                   CONCAT(CONVERT(varchar(8),CONVERT(bigint,tm.TMBAST)),' ',RIGHT('000000'+CONVERT(varchar(6),CONVERT(bigint,tm.TMBASS)),6)),
                   COUNT(DISTINCT d.SDSRNO)
            FROM dbo.TOPMASTER tm
            INNER JOIN dbo.SEVK_MASTER sm ON sm.SMSKOD=tm.TMSIRK AND sm.SMSYIL=tm.TMSYIL AND sm.SMSNO=tm.TMSEVK
            LEFT JOIN dbo.FIRMASTER f ON f.CMSKOD=sm.SMSKOD AND f.CMMKOD=sm.SMBKOD
            LEFT JOIN dbo.SEVK_DETAIL d ON d.SDSKOD=sm.SMSKOD AND d.SDSYIL=sm.SMSYIL AND d.SDSNO=sm.SMSNO
            WHERE tm.TMSIRK=@company AND tm.TMDEPO=@warehouse AND tm.TMBAST=@date AND tm.TMBITT=0
            GROUP BY tm.TMSYIL,tm.TMSEVK,sm.SMAPLK,sm.SMBKOD,f.CMMUNV,tm.TMRAMP,tm.TMPERS,tm.TMBAST,tm.TMBASS
            ORDER BY CONVERT(int,tm.TMSEVK) DESC
            """;
        var activeLoadings = new List<AxataActiveLoadingItem>();
        await using (var command = CreateCommand(connection, activeSql))
        {
            AddCommonParameters(command);
            command.Parameters.Add(new SqlParameter("@date", SqlDbType.Decimal) { Value = dateValue });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                activeLoadings.Add(new AxataActiveLoadingItem(
                    reader.GetInt32(0), reader.GetInt32(1), Text(reader, 2), Text(reader, 3), Text(reader, 4),
                    Text(reader, 5), Text(reader, 6), Text(reader, 7), reader.GetInt32(8)));
            }
        }

        const string pendingSql = """
            SELECT TOP (30) sm.SMSIPN,sm.SMHARK,sm.SMBAYK,f.CMMUNV,CONVERT(int,sm.SMCRDT),
                   COUNT(sd.SDKALN),COALESCE(SUM(sd.SDMIKI),0)
            FROM dbo.SIPMASTER sm
            LEFT JOIN dbo.FIRMASTER f ON f.CMSKOD=sm.SMSIRK AND f.CMMKOD=sm.SMBAYK
            LEFT JOIN dbo.SIPDETAIL sd ON sd.SDSIRK=sm.SMSIRK AND sd.SDSIPN=sm.SMSIPN
            WHERE sm.SMSIRK=@company AND sm.SMDAGK=@warehouse AND sm.SMSPST=0 AND sm.SM_ISCANCEL=0
            GROUP BY sm.SMSIPN,sm.SMHARK,sm.SMBAYK,f.CMMUNV,sm.SMCRDT
            ORDER BY CONVERT(int,sm.SMCRDT),sm.SMSIPN
            """;
        var pendingItems = new List<AxataPendingOrderItem>();
        await using (var command = CreateCommand(connection, pendingSql))
        {
            AddCommonParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pendingItems.Add(new AxataPendingOrderItem(
                    reader.GetString(0).Trim(), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                    ParseAxataDate(reader.GetInt32(4)) ?? targetDate, reader.GetInt32(5), reader.GetDecimal(6)));
            }
        }

        var result = new AxataWarehouseOverviewDto(
            Metadata("AXATA canlı operasyon verisi", targetDate),
            new AxataOutboundOverview(pendingOrders, pickingOrders, shippedToday, shippedQuantity),
            new AxataInboundOverview(pendingReceiving, processingReceiving, completedReceiving),
            new AxataQueueOverview(outboundQueue, inboundQueue), activeLoadings, pendingItems);
        memoryCache.Set(cacheKey, result, TimeSpan.FromSeconds(20));
        return result;
    }

    public async Task<AxataDocumentTraceDto> GetTraceAsync(string searchTerm, CancellationToken cancellationToken)
    {
        var term = searchTerm?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length > 100)
        {
            throw new ArgumentException("Sipariş, irsaliye veya sevk numarası 1-100 karakter olmalıdır.", nameof(searchTerm));
        }
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string orderSql = """
            SELECT TOP (1) sm.SMSIPN,sm.SMHARK,sm.SMBAYK,f.CMMUNV,CONVERT(int,sm.SMCRDT),CONVERT(int,sm.SMSPST)
            FROM dbo.SIPMASTER sm
            LEFT JOIN dbo.FIRMASTER f ON f.CMSKOD=sm.SMSIRK AND f.CMMKOD=sm.SMBAYK
            WHERE sm.SMSIRK=@company AND sm.SMDAGK=@warehouse AND (
                sm.SMSIPN=@term OR CONVERT(varchar(30),sm.SMEXIRSNO)=@term OR EXISTS (
                    SELECT 1 FROM dbo.SEVK_LINK l WHERE l.SDLSKOD=sm.SMSIRK AND l.SDLSIPN=sm.SMSIPN
                    AND (CONVERT(varchar(20),CONVERT(int,l.SDLSNO))=@term
                         OR CONCAT(CONVERT(int,l.SDLSYIL),'/',CONVERT(int,l.SDLSNO))=@term)))
            ORDER BY sm.id DESC
            """;
        string? orderNo = null, movement = null, targetCode = null, targetName = null;
        DateOnly? orderDate = null;
        int? orderStatus = null;
        await using (var command = CreateCommand(connection, orderSql))
        {
            AddCommonParameters(command);
            command.Parameters.Add(new SqlParameter("@term", SqlDbType.NVarChar, 100) { Value = term });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                orderNo = reader.GetString(0).Trim(); movement = Text(reader, 1); targetCode = Text(reader, 2); targetName = Text(reader, 3);
                orderDate = ParseAxataDate(reader.GetInt32(4)); orderStatus = reader.GetInt32(5);
            }
        }
        var shipments = orderNo is null ? [] : await ReadShipmentsAsync(connection, orderNo, cancellationToken);
        var logs = orderNo is null ? [] : await ReadPickingLogsAsync(connection, orderNo, cancellationToken);
        var queues = orderNo is null ? [] : await ReadQueuesAsync(connection, orderNo, cancellationToken);
        var inbounds = orderNo is null ? await ReadInboundsAsync(connection, term, cancellationToken) : [];
        return new AxataDocumentTraceDto(Metadata("AXATA anlık evrak izi"), term,
            orderNo is not null ? "OutboundOrder" : inbounds.Count > 0 ? "InboundOrder" : "NotFound",
            orderNo, movement, targetCode, targetName, orderDate, orderStatus,
            orderNo is null ? null : MapOrderStatus(orderStatus), shipments, logs, inbounds, queues);
    }

    public async Task<AxataPickingPerformanceDto> GetPickingPerformanceAsync(
        DateOnly? startDate, DateOnly? endDate, int? warehouseNo, int take, CancellationToken cancellationToken)
    {
        ValidateWarehouse(warehouseNo);
        var (start, end) = ResolveDateRange(startDate, endDate);
        var limit = Math.Clamp(take, 1, 200);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string pickerSql = """
            SELECT TOP (@take) CONVERT(varchar(20),p.UserID),u.PERADI,
                COUNT(DISTINCT CONCAT(p.ShipmentYear,':',p.ShipmentNo)),COUNT(1),
                COALESCE(SUM(p.TotalLines),0),COALESCE(SUM(p.TotalQty),0),COALESCE(SUM(p.UnPickedLines),0),
                CONVERT(decimal(18,2),COALESCE(AVG(CASE WHEN p.CompleteDate>=p.StartDate THEN DATEDIFF(second,p.StartDate,p.CompleteDate)/60.0 END),0))
            FROM dbo.tbl_PickingPerformance p
            LEFT JOIN dbo.PERSONEL u ON u.PERSRK=p.Company AND u.PERSNO=p.UserID
            WHERE p.Company=@company AND p.Warehouse=@warehouse
              AND p.StartDate>=@start AND p.StartDate<DATEADD(day,1,@end)
            GROUP BY p.UserID,u.PERADI
            ORDER BY COALESCE(SUM(p.TotalQty),0) DESC,p.UserID
            """;
        var pickers = new List<AxataPickerPerformanceItem>();
        await using (var command = CreateCommand(connection, pickerSql))
        {
            AddCommonParameters(command); AddDateRange(command, start, end);
            command.Parameters.Add(new SqlParameter("@take", SqlDbType.Int) { Value = limit });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var lines = reader.GetInt32(4); var missing = reader.GetInt32(6);
                pickers.Add(new AxataPickerPerformanceItem(reader.GetString(0), Text(reader, 1), reader.GetInt32(2), reader.GetInt32(3),
                    lines, reader.GetDecimal(5), missing, lines == 0 ? 100 : Percentage(Math.Max(0, lines - missing), lines), Convert.ToDouble(reader.GetDecimal(7))));
            }
        }
        const string zoneSql = """
            SELECT TOP (30) CONVERT(varchar(20),p.ZoneCode),
                COUNT(DISTINCT CONCAT(p.ShipmentYear,':',p.ShipmentNo)),COALESCE(SUM(p.TotalLines),0),
                COALESCE(SUM(p.TotalQty),0),COALESCE(SUM(p.UnPickedLines),0)
            FROM dbo.tbl_PickingPerformance p
            WHERE p.Company=@company AND p.Warehouse=@warehouse
              AND p.StartDate>=@start AND p.StartDate<DATEADD(day,1,@end)
            GROUP BY p.ZoneCode ORDER BY COALESCE(SUM(p.TotalQty),0) DESC,p.ZoneCode
            """;
        var zones = new List<AxataZonePerformanceItem>();
        await using (var command = CreateCommand(connection, zoneSql))
        {
            AddCommonParameters(command); AddDateRange(command, start, end);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                zones.Add(new AxataZonePerformanceItem(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetInt32(4)));
        }
        return new AxataPickingPerformanceDto(Metadata($"{start:dd.MM.yyyy} - {end:dd.MM.yyyy}"), pickers, zones);
    }

    public async Task<AxataFillRateDto> GetFillRateAsync(
        DateOnly? startDate, DateOnly? endDate, string? targetStoreCode, int? warehouseNo, int take, CancellationToken cancellationToken)
    {
        ValidateWarehouse(warehouseNo);
        var (start, end) = ResolveDateRange(startDate, endDate);
        var store = string.IsNullOrWhiteSpace(targetStoreCode) ? null : targetStoreCode.Trim();
        var limit = Math.Clamp(take, 1, 100);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string cte = """
            ;WITH OrderLines AS (
              SELECT sd.SDSIRK,sd.SDSIPN,sd.SDKALN,sm.SMBAYK,sd.SDSTKD,sd.SDMIKI,
                CASE WHEN COALESCE(SUM(d.SDGMIK),0)>sd.SDMIKI THEN sd.SDMIKI ELSE COALESCE(SUM(d.SDGMIK),0) END FulfilledQty
              FROM dbo.SIPMASTER sm
              INNER JOIN dbo.SIPDETAIL sd ON sd.SDSIRK=sm.SMSIRK AND sd.SDSIPN=sm.SMSIPN
              LEFT JOIN dbo.SEVK_LINK l ON l.SDLSKOD=sd.SDSIRK AND l.SDLSIPN=sd.SDSIPN AND l.SDLKALN=sd.SDKALN
              LEFT JOIN dbo.SEVK_DETAIL d ON d.SDSKOD=l.SDLSKOD AND d.SDSYIL=l.SDLSYIL AND d.SDSNO=l.SDLSNO AND d.SDSRNO=l.SDLSIRA
              WHERE sm.SMSIRK=@company AND sm.SMDAGK=@warehouse AND sm.SM_ISCANCEL=0 AND sm.SMSPST<>9
                AND sm.SMCRDT BETWEEN @startDate AND @endDate AND (@store IS NULL OR sm.SMBAYK=@store)
              GROUP BY sd.SDSIRK,sd.SDSIPN,sd.SDKALN,sm.SMBAYK,sd.SDSTKD,sd.SDMIKI)
            """;
        var summary = await ReadFillSummary(connection, cte, start, end, store, cancellationToken);
        var stores = await ReadStores(connection, cte, start, end, store, limit, cancellationToken);
        var skus = await ReadMissingSkus(connection, cte, start, end, store, limit, cancellationToken);
        return new AxataFillRateDto(Metadata($"{start:dd.MM.yyyy} - {end:dd.MM.yyyy}"), summary.Rate,
            summary.Lines, summary.Full, summary.Partial, summary.None, stores, skus);
    }

    public async Task<AxataCapacityDto> GetCapacityAsync(int? warehouseNo, CancellationToken cancellationToken)
    {
        ValidateWarehouse(warehouseNo);
        var key = $"axata-capacity:{MikroWarehouseNo}";
        if (memoryCache.TryGetValue(key, out AxataCapacityDto? cached) && cached is not null) return cached;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string summarySql = """
            SELECT COUNT(1),COALESCE(SUM(CASE WHEN YDURUM=2 THEN 1 ELSE 0 END),0),
                   COALESCE(SUM(CASE WHEN YDURUM=0 THEN 1 ELSE 0 END),0),
                   COALESCE(SUM(CASE WHEN YDURUM=1 THEN 1 ELSE 0 END),0)
            FROM dbo.CELL WHERE YSIRKD=@company AND YDEPO=@warehouse
            """;
        int total, occupied, empty, blocked;
        await using (var command = CreateCommand(connection, summarySql))
        {
            AddCommonParameters(command); await using var reader = await command.ExecuteReaderAsync(cancellationToken); await reader.ReadAsync(cancellationToken);
            total = reader.GetInt32(0); occupied = reader.GetInt32(1); empty = reader.GetInt32(2); blocked = reader.GetInt32(3);
        }
        const string zoneSql = """
            SELECT TOP (50) CONVERT(varchar(20),YZONE),COUNT(1),
              COALESCE(SUM(CASE WHEN YDURUM=2 THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN YDURUM=0 THEN 1 ELSE 0 END),0)
            FROM dbo.CELL WHERE YSIRKD=@company AND YDEPO=@warehouse
            GROUP BY YZONE ORDER BY COUNT(1) DESC,YZONE
            """;
        var zones = new List<AxataZoneCapacityItem>();
        await using (var command = CreateCommand(connection, zoneSql))
        {
            AddCommonParameters(command); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) { var t = reader.GetInt32(1); var o = reader.GetInt32(2); zones.Add(new(reader.GetString(0), t, o, reader.GetInt32(3), Percentage(o, t))); }
        }
        const string typeSql = """
            SELECT sy.CSYER,sy.CSTAN,COUNT(c.YKORID) FROM dbo.STOKYER sy
            LEFT JOIN dbo.CELL c ON c.YSIRKD=@company AND c.YDEPO=@warehouse
              AND LTRIM(RTRIM(CONVERT(varchar(20),c.YTIP)))=LTRIM(RTRIM(CONVERT(varchar(20),sy.CSYER)))
            GROUP BY sy.CSYER,sy.CSTAN ORDER BY COUNT(c.YKORID) DESC,sy.CSYER
            """;
        var types = new List<AxataStockTypeCapacityItem>();
        await using (var command = CreateCommand(connection, typeSql))
        {
            AddCommonParameters(command); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) types.Add(new(reader.GetString(0).Trim(), Text(reader, 1), reader.GetInt32(2)));
        }
        var result = new AxataCapacityDto(Metadata("AXATA anlık raf haritası"), total, occupied, empty, blocked, Percentage(occupied, total), zones, types);
        memoryCache.Set(key, result, TimeSpan.FromMinutes(2)); return result;
    }

    private async Task<IReadOnlyList<AxataTraceShipmentItem>> ReadShipmentsAsync(SqlConnection connection, string orderNo, CancellationToken ct)
    {
        const string sql = """
            SELECT CONVERT(int,m.SMSYIL),CONVERT(int,m.SMSNO),m.SMHKOD,CONVERT(int,m.SMSTAT),m.SMAPLK,r.TMRAMP,
              CONVERT(varchar(8),CONVERT(bigint,m.SMYSTR)),CONVERT(int,d.SDKALN),l.SDLSIRA,d.SDSTKD,sku.MAMADA,d.SDMIKI,
              CASE WHEN COALESCE(sd.SDGMIK,0)>d.SDMIKI THEN d.SDMIKI ELSE COALESCE(sd.SDGMIK,0) END
            FROM dbo.SIPDETAIL d
            INNER JOIN dbo.SEVK_LINK l ON l.SDLSKOD=d.SDSIRK AND l.SDLSIPN=d.SDSIPN AND l.SDLKALN=d.SDKALN
            INNER JOIN dbo.SEVK_MASTER m ON m.SMSKOD=l.SDLSKOD AND m.SMSYIL=l.SDLSYIL AND m.SMSNO=l.SDLSNO
            LEFT JOIN dbo.SEVK_DETAIL sd ON sd.SDSKOD=l.SDLSKOD AND sd.SDSYIL=l.SDLSYIL AND sd.SDSNO=l.SDLSNO AND sd.SDSRNO=l.SDLSIRA
            OUTER APPLY(SELECT TOP (1) tm.TMRAMP FROM dbo.TOPMASTER tm WHERE tm.TMSIRK=m.SMSKOD AND tm.TMSYIL=m.SMSYIL AND tm.TMSEVK=m.SMSNO ORDER BY tm.TMID DESC)r
            LEFT JOIN dbo.SKUMASTER sku ON sku.MSIRKD=d.SDSIRK AND sku.MAMAUA=d.SDSTKD
            WHERE d.SDSIRK=@company AND d.SDSIPN=@orderNo ORDER BY m.SMSYIL,m.SMSNO,l.SDLSIRA
            """;
        var groups = new Dictionary<(int, int), (AxataTraceShipmentItem Header, List<AxataTraceShipmentDetailItem> Lines)>();
        await using var command = CreateCommand(connection, sql); command.Parameters.Add(new("@company", SqlDbType.NVarChar, 4) { Value = CompanyCode }); command.Parameters.Add(new("@orderNo", SqlDbType.NVarChar, 100) { Value = orderNo });
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var key = (reader.GetInt32(0), reader.GetInt32(1));
            if (!groups.TryGetValue(key, out var group)) { var status = reader.GetInt32(3); group = (new(key.Item1, key.Item2, Text(reader, 2), status, MapShipmentStatus(status), Text(reader, 4), Text(reader, 5), Text(reader, 6), []), []); groups.Add(key, group); }
            var ordered = reader.GetDecimal(11); var shipped = reader.GetDecimal(12);
            group.Lines.Add(new(reader.IsDBNull(7) ? reader.GetInt32(8) : reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9).Trim(), Text(reader, 10), ordered, shipped, ordered - shipped));
        }
        return groups.Values.Select(x => x.Header with { Details = x.Lines }).ToArray();
    }

    private async Task<IReadOnlyList<AxataTracePickingLogItem>> ReadPickingLogsAsync(SqlConnection connection, string orderNo, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (100) tl.TLSIRA,tl.TLDEPO,tl.TLKORID,tl.TLSKU,sku.MAMADA,tl.TLMIKTAR,
              CONVERT(varchar(20),CONVERT(int,tl.TLPERS)),CONVERT(varchar(8),CONVERT(bigint,tl.TLDATE)),
              RIGHT('000000'+CONVERT(varchar(6),CONVERT(bigint,tl.TLTIME)),6),CONVERT(varchar(30),tl.TLPALNO)
            FROM dbo.TOPLOG tl LEFT JOIN dbo.SKUMASTER sku ON sku.MSIRKD=tl.TLSKOD AND sku.MAMAUA=tl.TLSKU
            WHERE tl.TLSKOD=@company AND tl.TLDEPO=@warehouse AND tl.TLSIPNO=@orderNo
            ORDER BY tl.TLDATE DESC,tl.TLTIME DESC,tl.TLSIRA DESC
            """;
        var items = new List<AxataTracePickingLogItem>(); await using var command = CreateCommand(connection, sql); AddCommonParameters(command); command.Parameters.Add(new("@orderNo", SqlDbType.NVarChar, 100) { Value = orderNo });
        await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) items.Add(new(reader.GetInt32(0), Text(reader, 1), null, Text(reader, 2), Text(reader, 3), Text(reader, 4), reader.GetDecimal(5), Text(reader, 6), FormatDate(Text(reader, 7)), FormatTime(Text(reader, 8)), Text(reader, 9))); return items;
    }

    private async Task<IReadOnlyList<AxataTraceQueueItem>> ReadQueuesAsync(SqlConnection connection, string orderNo, CancellationToken ct)
    {
        const string sql = "SELECT S06TESL,CONVERT(int,S06STAT),S06FIRM,CONVERT(varchar(8),CONVERT(bigint,S06ITAR)) FROM dbo.ENT006 WHERE S06SKOD=@company AND S06TESL=@orderNo ORDER BY S06SIRA DESC";
        var items = new List<AxataTraceQueueItem>(); await using var command = CreateCommand(connection, sql); command.Parameters.Add(new("@company", SqlDbType.NVarChar, 4) { Value = CompanyCode }); command.Parameters.Add(new("@orderNo", SqlDbType.NVarChar, 100) { Value = orderNo });
        await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) { var status = reader.GetInt32(1); items.Add(new("ENT006 (Çıkış kuyruğu)", reader.GetString(0).Trim(), status, status == 1 ? "Mikro'ya aktarıldı" : "Kuyrukta bekliyor", Text(reader, 2), FormatDate(Text(reader, 3)))); }
        return items;
    }

    private async Task<IReadOnlyList<AxataTraceInboundItem>> ReadInboundsAsync(SqlConnection connection, string term, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (10) m.id,m.CMSIPN,m.CMHKOD,m.CMFIRM,f.CMMUNV,m.CMIRSN,CONVERT(int,m.CMIRST),CONVERT(int,m.CMSTAT)
            FROM dbo.GSIPMST m LEFT JOIN dbo.FIRMASTER f ON f.CMSKOD=m.CMSIRK AND f.CMMKOD=m.CMFIRM
            WHERE m.CMSIRK=@company AND m.CMDEPO=@warehouse AND (m.CMSIPN=@term OR m.CMIRSN=@term OR m.CMREFN=@term) ORDER BY m.id DESC
            """;
        var headers = new List<(long Id, string Doc, string? Type, string? Supplier, string? Name, string? Receipt, DateOnly? Date, int Status)>();
        await using (var command = CreateCommand(connection, sql)) { AddCommonParameters(command); command.Parameters.Add(new("@term", SqlDbType.NVarChar, 100) { Value = term }); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) headers.Add((reader.GetInt64(0), reader.GetString(1).Trim(), Text(reader, 2), Text(reader, 3), Text(reader, 4), Text(reader, 5), ParseAxataDate(reader.GetInt32(6)), reader.GetInt32(7))); }
        var result = new List<AxataTraceInboundItem>();
        foreach (var h in headers)
        {
            const string lineSql = """
                SELECT ROW_NUMBER() OVER(ORDER BY g.CSKALN,g.CSID),g.CSSKUK,sku.MAMADA,g.CSIPMIK,g.CSAMIK
                FROM dbo.GSIPMASTER g LEFT JOIN dbo.SKUMASTER sku ON sku.MSIRKD=g.CSSKOD AND sku.MAMAUA=g.CSSKUK
                WHERE g.CSSKOD=@company AND g.CSHARK=@type AND g.CSBNUM=@doc ORDER BY g.CSKALN,g.CSID
                """;
            var lines = new List<AxataTraceInboundDetailItem>(); await using var command = CreateCommand(connection, lineSql); command.Parameters.Add(new("@company", SqlDbType.NVarChar, 4) { Value = CompanyCode }); command.Parameters.Add(new("@type", SqlDbType.NVarChar, 6) { Value = (object?)h.Type ?? DBNull.Value }); command.Parameters.Add(new("@doc", SqlDbType.NVarChar, 100) { Value = h.Doc }); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) lines.Add(new(Convert.ToInt32(reader.GetInt64(0)), reader.GetString(1).Trim(), Text(reader, 2), reader.GetDecimal(3), reader.GetDecimal(4)));
            result.Add(new(h.Id, h.Doc, h.Type, h.Supplier, h.Name, h.Receipt, h.Date, h.Status, MapInboundStatus(h.Status), lines));
        }
        return result;
    }

    private async Task<(decimal Rate, int Lines, int Full, int Partial, int None)> ReadFillSummary(SqlConnection c, string cte, DateOnly start, DateOnly end, string? store, CancellationToken ct)
    {
        var sql = cte + "SELECT COUNT(1),COALESCE(SUM(CASE WHEN FulfilledQty>=SDMIKI THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN FulfilledQty>0 AND FulfilledQty<SDMIKI THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN FulfilledQty=0 THEN 1 ELSE 0 END),0),COALESCE(SUM(SDMIKI),0),COALESCE(SUM(FulfilledQty),0) FROM OrderLines";
        await using var command = CreateCommand(c, sql, 60); AddFillParameters(command, start, end, store); await using var r = await command.ExecuteReaderAsync(ct); await r.ReadAsync(ct); var ordered = r.GetDecimal(4); var fulfilled = r.GetDecimal(5); return (ordered == 0 ? 100 : Math.Round(fulfilled / ordered * 100, 2), r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3));
    }
    private async Task<IReadOnlyList<AxataStoreFillRateItem>> ReadStores(SqlConnection c, string cte, DateOnly start, DateOnly end, string? store, int take, CancellationToken ct)
    {
        var sql = cte + "SELECT TOP (@take) o.SMBAYK,f.CMMUNV,COUNT(DISTINCT o.SDSIPN),COUNT(1),SUM(o.SDMIKI),SUM(o.FulfilledQty),SUM(o.SDMIKI-o.FulfilledQty) FROM OrderLines o LEFT JOIN dbo.FIRMASTER f ON f.CMSKOD=o.SDSIRK AND f.CMMKOD=o.SMBAYK GROUP BY o.SMBAYK,f.CMMUNV ORDER BY SUM(o.SDMIKI-o.FulfilledQty) DESC,o.SMBAYK";
        var items = new List<AxataStoreFillRateItem>(); await using var command = CreateCommand(c, sql, 60); AddFillParameters(command, start, end, store); command.Parameters.Add(new("@take", SqlDbType.Int) { Value = take }); await using var r = await command.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) { var ordered = r.GetDecimal(4); var fulfilled = r.GetDecimal(5); items.Add(new(r.GetString(0).Trim(), Text(r, 1), r.GetInt32(2), r.GetInt32(3), ordered, fulfilled, r.GetDecimal(6), Percentage(fulfilled, ordered))); }
        return items;
    }
    private async Task<IReadOnlyList<AxataSkuFillRateItem>> ReadMissingSkus(SqlConnection c, string cte, DateOnly start, DateOnly end, string? store, int take, CancellationToken ct)
    {
        var sql = cte + "SELECT TOP (@take) o.SDSTKD,sku.MAMADA,COUNT(1),SUM(o.SDMIKI),SUM(o.FulfilledQty),SUM(o.SDMIKI-o.FulfilledQty) FROM OrderLines o LEFT JOIN dbo.SKUMASTER sku ON sku.MSIRKD=o.SDSIRK AND sku.MAMAUA=o.SDSTKD GROUP BY o.SDSTKD,sku.MAMADA HAVING SUM(o.SDMIKI-o.FulfilledQty)>0 ORDER BY SUM(o.SDMIKI-o.FulfilledQty) DESC,o.SDSTKD";
        var items = new List<AxataSkuFillRateItem>(); await using var command = CreateCommand(c, sql, 60); AddFillParameters(command, start, end, store); command.Parameters.Add(new("@take", SqlDbType.Int) { Value = take }); await using var r = await command.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) { var ordered = r.GetDecimal(3); var fulfilled = r.GetDecimal(4); items.Add(new(r.GetString(0).Trim(), Text(r, 1), r.GetInt32(2), ordered, fulfilled, r.GetDecimal(5), Percentage(fulfilled, ordered))); }
        return items;
    }

    private void AddCommonParameters(SqlCommand c) { c.Parameters.Add(new("@company", SqlDbType.NVarChar, 4) { Value = CompanyCode }); c.Parameters.Add(new("@warehouse", SqlDbType.NVarChar, 4) { Value = AxataWarehouseCode }); }
    private void AddFillParameters(SqlCommand c, DateOnly start, DateOnly end, string? store) { AddCommonParameters(c); c.Parameters.Add(new("@startDate", SqlDbType.Decimal) { Value = ToAxataDate(start) }); c.Parameters.Add(new("@endDate", SqlDbType.Decimal) { Value = ToAxataDate(end) }); c.Parameters.Add(new("@store", SqlDbType.NVarChar, 100) { Value = (object?)store ?? DBNull.Value }); }
    private static void AddDateRange(SqlCommand c, DateOnly start, DateOnly end) { c.Parameters.Add(new("@start", SqlDbType.DateTime) { Value = start.ToDateTime(TimeOnly.MinValue) }); c.Parameters.Add(new("@end", SqlDbType.DateTime) { Value = end.ToDateTime(TimeOnly.MinValue) }); }
    private static SqlCommand CreateCommand(SqlConnection c, string sql, int timeout = CommandTimeoutSeconds) => new(sql, c) { CommandTimeout = timeout };
    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct) { var cs = configuration.GetConnectionString("AxataOperationsReadConnection") ?? configuration.GetConnectionString("AxataConnection"); if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("AXATA salt okunur bağlantısı tanımlanmamış."); var c = new SqlConnection(cs); await c.OpenAsync(ct); return c; }
    private static string? Text(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim();
    private (DateOnly Start, DateOnly End) ResolveDateRange(DateOnly? startDate, DateOnly? endDate) { var end = endDate ?? DateOnly.FromDateTime(DateTime.Today); var start = startDate ?? end.AddDays(-6); if (end < start) throw new ArgumentException("Bitiş tarihi başlangıç tarihinden önce olamaz."); if (end.DayNumber - start.DayNumber + 1 > MaximumReportDayCount) throw new ArgumentException($"Rapor aralığı en fazla {MaximumReportDayCount} gün olabilir."); return (start, end); }
    private void ValidateWarehouse(int? n) { if (n.HasValue && n.Value != MikroWarehouseNo) throw new ArgumentException($"AXATA depo kokpiti yalnız merkez depo {MikroWarehouseNo} verisini gösterir.", nameof(n)); }
    private AxataOperationsMetadata Metadata(string freshness, DateOnly? date = null) => new(DateTimeOffset.UtcNow, "AxataWM_CANLI (salt okunur)", freshness, MikroWarehouseNo, date);
    private static int ToAxataDate(DateOnly d) => int.Parse(d.ToString("yyyyMMdd"), System.Globalization.CultureInfo.InvariantCulture);
    private static DateOnly? ParseAxataDate(int v) => DateOnly.TryParseExact(v.ToString(System.Globalization.CultureInfo.InvariantCulture), "yyyyMMdd", out var d) ? d : null;
    private static string? FormatDate(string? v) => v is not null && DateOnly.TryParseExact(v, "yyyyMMdd", out var d) ? d.ToString("dd.MM.yyyy") : v;
    private static string? FormatTime(string? v) => v is { Length: 6 } ? $"{v[..2]}:{v.Substring(2, 2)}:{v[4..]}" : v;
    private static double Percentage(decimal n, decimal d) => d == 0 ? 0 : Math.Round((double)(n / d * 100), 2);
    private static double Percentage(int n, int d) => d == 0 ? 0 : Math.Round((double)n / d * 100, 2);
    private static string MapOrderStatus(int? s) => s switch { 0 => "Açık / toplama bekliyor", 4 => "Sevk edildi / tamamlandı", 9 => "İptal edildi", _ => $"Durum {s}" };
    private static string MapShipmentStatus(int? s) => s switch { 4 => "Rampada / yükleniyor", 6 => "Yüklendi / sevk edildi", _ => $"Durum {s}" };
    private static string MapInboundStatus(int? s) => s switch { 0 => "Kabul bekliyor", 4 => "Kabul tamamlandı", _ => $"Durum {s}" };
}
