using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

public sealed class CompanyMovementWriteService(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    MobileOfflineSyncService mobileOfflineSyncService,
    ILogger<CompanyMovementWriteService> logger)
{
    private const short MovementFileId = 16;
    private const short MikroUserNo = 39;
    private const byte CompanyDispatchDocumentType = 1;
    private const byte OutgoingMovementType = 1;
    private const byte WholesaleMovementGenre = 0;
    private const byte RetailMovementGenre = 1;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const int FirstDocumentOrderNo = 0;
    private const string IrsaliyeKaydetPath = "/Api/apiMethods/IrsaliyeKaydetV2";
    private const string CompanyShipmentOfflineOperationCode = "sevk-islemleri.giden-firma-sevkleri.create";
    private const string CompanyReturnOfflineOperationCode = "iade-islemleri.firma-iadeleri.create";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<CreateCompanyMovementResponse> ExecuteAsync(
        CreateCompanyMovementRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return await OfflineCreateGuard.ExecuteAsync(
            mobileOfflineSyncService,
            ResolveOfflineOperationCode(kind),
            request.RequestedByUserId,
            request.WarehouseNo,
            request.ClientRequestId,
            request,
            (_, innerCancellationToken) => TryRecoverOfflineResponseAsync(request, kind, innerCancellationToken),
            innerCancellationToken => ExecuteRoutedAsync(request, kind, innerCancellationToken),
            cancellationToken);
    }

    private Task<CreateCompanyMovementResponse> ExecuteRoutedAsync(
        CreateCompanyMovementRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken) =>
        mikroWriteRoutingOptions.CurrentValue.CompanyMovement switch
        {
            MikroWriteMode.Database => ExecuteDatabaseAsync(request, kind, cancellationToken),
            MikroWriteMode.MikroApi => ExecuteMikroApiAsync(request, kind, cancellationToken),
            MikroWriteMode.DualShadow => ExecuteDualShadowAsync(request, kind, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:CompanyMovement mode '{mode}'.")
        };

    private async Task<CreateCompanyMovementResponse> ExecuteDatabaseAsync(
        CreateCompanyMovementRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var customerCode = request.CustomerCode.Trim();
        var documentSerie = $"F{request.WarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo);
        var lines = request.Lines.ToArray();
        var returnType = ResolveReturnType(kind);
        var movementGenre = ResolveMovementGenre(kind);
        var offlineTraceKey = ResolveOfflineTraceKey(request.ClientRequestId);
        var totalStopwatch = Stopwatch.StartNew();
        var phaseStartedAt = Stopwatch.GetTimestamp();

        void LogCreatePhase(string phase, int lineCount)
        {
            var elapsed = Stopwatch.GetElapsedTime(phaseStartedAt);
            if (elapsed.TotalMilliseconds >= 500d)
            {
                logger.LogInformation(
                    "Company movement create phase {Phase} completed in {ElapsedMs} ms. TotalMs={TotalMs}, Kind={Kind}, DocumentSerie={DocumentSerie}, WarehouseNo={WarehouseNo}, CustomerCode={CustomerCode}, ReturnType={ReturnType}, LineCount={LineCount}, Route=Database",
                    phase,
                    elapsed.TotalMilliseconds,
                    totalStopwatch.Elapsed.TotalMilliseconds,
                    kind,
                    documentSerie,
                    request.WarehouseNo,
                    customerCode,
                    returnType,
                    lineCount);
            }

            phaseStartedAt = Stopwatch.GetTimestamp();
        }

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            var lineTaxInfos = await LoadLineTaxInfosAsync(lines, kind, cancellationToken);
            LogCreatePhase("line-tax-info-lookup", lines.Length);
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            LogCreatePhase("begin-transaction", lines.Length);

            try
            {
                var customer = await GetCustomerAsync(customerCode, cancellationToken);
                LogCreatePhase("customer-lookup", lines.Length);
                var customerAddressNo = ResolveCustomerAddressNo(customer);
                var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, returnType, cancellationToken);
                LogCreatePhase("next-document-order-no", lines.Length);
                var movements = lines
                    .Select((line, rowNo) => CreateMovement(
                        request,
                        line,
                        customerCode,
                        customerAddressNo,
                        movementGenre,
                        returnType,
                        rowNo,
                        now,
                        movementDate,
                        documentDate,
                        documentNo,
                        documentSerie,
                        documentOrderNo,
                        offlineTraceKey,
                        lineTaxInfos))
                    .ToArray();
                LogCreatePhase("build-movement-rows", movements.Length);

                await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(movements, cancellationToken);
                LogCreatePhase("track-entities", movements.Length);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                LogCreatePhase("save-changes", movements.Length);
                await transaction.CommitAsync(cancellationToken);
                LogCreatePhase("commit", movements.Length);

                return new CreateCompanyMovementResponse(
                    documentSerie,
                    documentOrderNo,
                    movementDate,
                    documentDate,
                    documentNo,
                    request.WarehouseNo,
                    customerCode,
                    movements.Length,
                    movements.Sum(movement => movement.sth_miktar ?? 0d),
                    movements.Sum(movement => movement.sth_tutar ?? 0d),
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<CreateCompanyMovementResponse> ExecuteMikroApiAsync(
        CreateCompanyMovementRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var customerCode = request.CustomerCode.Trim();
        var documentSerie = $"F{request.WarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo);
        var description = NormalizeText(request.Description);
        var lines = request.Lines.ToArray();
        var returnType = ResolveReturnType(kind);
        var movementGenre = ResolveMovementGenre(kind);
        var offlineTraceKey = ResolveOfflineTraceKey(request.ClientRequestId);
        var customer = await GetCustomerAsync(customerCode, cancellationToken);
        var customerAddressNo = ResolveCustomerAddressNo(customer);
        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, returnType, cancellationToken);
        var lineTaxInfos = await LoadLineTaxInfosAsync(lines, kind, cancellationToken);
        var payload = CompanyMovementIrsaliyeMikroApiPayloadFactory.Create(
            request,
            lines,
            customerCode,
            customerAddressNo,
            movementGenre,
            returnType,
            movementDate,
            documentDate,
            documentNo,
            documentSerie,
            documentOrderNo,
            description,
            offlineTraceKey,
            lineTaxInfos);

        logger.LogInformation(
            "Company movement create is routed to Mikro API {Path}. Kind={Kind}, DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, CustomerCode={CustomerCode}, ReturnType={ReturnType}, LineCount={LineCount}",
            IrsaliyeKaydetPath,
            kind,
            documentSerie,
            documentOrderNo,
            request.WarehouseNo,
            customerCode,
            returnType,
            lines.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            IrsaliyeKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API company movement create failed.");
        }

        var recovered = await RecoverMikroApiCreateResponseAsync(
            documentSerie,
            documentOrderNo,
            request,
            lines,
            customerCode,
            returnType,
            movementDate,
            documentDate,
            documentNo,
            options.ConnectionStringName,
            movementGenre,
            result.RawResponse,
            cancellationToken);

        await mikroApiClient.MarkRecoveredAsync(
            result,
            recovered.DocumentNo,
            cancellationToken: cancellationToken);
        return recovered;
    }

    private async Task<CreateCompanyMovementResponse> ExecuteDualShadowAsync(
        CreateCompanyMovementRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:CompanyMovement is DualShadow. IrsaliyeKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, kind, cancellationToken);
    }

    private async Task<CreateCompanyMovementResponse> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateCompanyMovementRequest request,
        IReadOnlyList<CreateCompanyMovementLineRequest> lines,
        string customerCode,
        byte returnType,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string writeConnectionName,
        byte movementGenre,
        string rawResponse,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverCompanyMovementResponseAsync(
                documentSerie,
                documentOrderNo,
                request,
                customerCode,
                returnType,
                movementDate,
                documentDate,
                documentNo,
                writeConnectionName,
                movementGenre,
                cancellationToken);

            if (response is not null)
            {
                return response;
            }

            if (attempt < MikroApiRecoveryAttemptCount)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(MikroApiRecoveryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        if (TryRecoverCompanyMovementResponseFromMikroApiResult(
                documentSerie,
                documentOrderNo,
                request,
                lines,
                customerCode,
                movementDate,
                documentDate,
                documentNo,
                writeConnectionName,
                rawResponse,
                out var recoveredFromResponse))
        {
            return recoveredFromResponse;
        }

        throw new InvalidOperationException(
            "Mikro API company movement create succeeded, but created STOK_HAREKETLERI rows could not be read back.");
    }

    private static bool TryRecoverCompanyMovementResponseFromMikroApiResult(
        string documentSerie,
        int documentOrderNo,
        CreateCompanyMovementRequest request,
        IReadOnlyList<CreateCompanyMovementLineRequest> lines,
        string customerCode,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string writeConnectionName,
        string rawResponse,
        out CreateCompanyMovementResponse response)
    {
        response = default!;
        var responseRows = MikroApiCreatedDocumentResultReader.ReadRows(rawResponse);
        if (responseRows.Count < lines.Count)
        {
            return false;
        }

        var firstRow = responseRows[0];
        response = new CreateCompanyMovementResponse(
            firstRow.DocumentSerie ?? documentSerie,
            firstRow.DocumentOrderNo ?? documentOrderNo,
            movementDate,
            documentDate,
            documentNo,
            request.WarehouseNo,
            customerCode,
            lines.Count,
            lines.Sum(line => line.Quantity),
            lines.Sum(line => line.Quantity * line.UnitPrice),
            writeConnectionName);

        return true;
    }

    private async Task<CreateCompanyMovementResponse?> TryRecoverCompanyMovementResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateCompanyMovementRequest request,
        string customerCode,
        byte returnType,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string writeConnectionName,
        byte movementGenre,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_cins == movementGenre &&
                movement.sth_normal_iade == returnType &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo &&
                movement.sth_cikis_depo_no == request.WarehouseNo &&
                movement.sth_cari_kodu == customerCode)
            .Select(movement => new
            {
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                movement.sth_cari_kodu,
                movement.sth_miktar,
                movement.sth_tutar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no,
                row.sth_cari_kodu
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one company movement document matched the same serie and order number.");
        }

        var firstRow = rows[0];

        return new CreateCompanyMovementResponse(
            firstRow.sth_evrakno_seri ?? documentSerie,
            firstRow.sth_evrakno_sira ?? documentOrderNo,
            firstRow.sth_tarih?.Date ?? movementDate,
            firstRow.sth_belge_tarih?.Date ?? documentDate,
            firstRow.sth_belge_no ?? documentNo,
            firstRow.sth_cikis_depo_no ?? request.WarehouseNo,
            firstRow.sth_cari_kodu ?? customerCode,
            rows.Count,
            rows.Sum(row => row.sth_miktar ?? 0d),
            rows.Sum(row => row.sth_tutar ?? 0d),
            writeConnectionName);
    }

    private async Task<CreateCompanyMovementResponse?> TryRecoverOfflineResponseAsync(
        CreateCompanyMovementRequest request,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        if (!request.ClientRequestId.HasValue)
        {
            return null;
        }

        var traceKey = MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value);
        var returnType = ResolveReturnType(kind);
        var movementGenre = ResolveMovementGenre(kind);
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_cins == movementGenre &&
                movement.sth_normal_iade == returnType &&
                movement.sth_cikis_depo_no == request.WarehouseNo &&
                movement.sth_eticaret_kanal_kodu == traceKey)
            .Select(movement => new
            {
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                movement.sth_cari_kodu,
                movement.sth_miktar,
                movement.sth_tutar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no,
                row.sth_cari_kodu
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one company movement document matched the same clientRequestId trace.");
        }

        var firstRow = rows[0];
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;

        return new CreateCompanyMovementResponse(
            firstRow.sth_evrakno_seri ?? $"F{request.WarehouseNo}",
            firstRow.sth_evrakno_sira ?? FirstDocumentOrderNo,
            firstRow.sth_tarih?.Date ?? movementDate,
            firstRow.sth_belge_tarih?.Date ?? documentDate,
            firstRow.sth_belge_no ?? NormalizeText(request.DocumentNo),
            firstRow.sth_cikis_depo_no ?? request.WarehouseNo,
            firstRow.sth_cari_kodu ?? request.CustomerCode.Trim(),
            rows.Count,
            rows.Sum(row => row.sth_miktar ?? 0d),
            rows.Sum(row => row.sth_tutar ?? 0d),
            mikroWriteOptions.Value.ConnectionStringName);
    }

    private async Task<CARI_HESAPLAR> GetCustomerAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var customer = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.cari_kod == customerCode, cancellationToken);

        if (customer is null)
        {
            throw new KeyNotFoundException("Customer was not found in Mikro write database.");
        }

        return customer;
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        byte returnType,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == returnType &&
                movement.sth_evrakno_seri == documentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<IReadOnlyDictionary<string, CompanyMovementLineTaxInfo>> LoadLineTaxInfosAsync(
        IReadOnlyCollection<CreateCompanyMovementLineRequest> lines,
        CompanyMovementKind kind,
        CancellationToken cancellationToken)
    {
        var stockCodes = lines
            .Select(line => line.StockCode.Trim())
            .Where(stockCode => stockCode.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (stockCodes.Length == 0)
        {
            return new Dictionary<string, CompanyMovementLineTaxInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var stockTaxPointers = await mikroWriteDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stock.sto_kod != null && stockCodes.Contains(stock.sto_kod))
            .Select(stock => new
            {
                StockCode = stock.sto_kod ?? string.Empty,
                TaxPointer = ResolveStockTaxPointer(
                    kind,
                    stock.sto_perakende_vergi ?? 0,
                    stock.sto_toptan_vergi ?? 0)
            })
            .ToListAsync(cancellationToken);

        var taxRates = await LoadTaxRatesAsync(cancellationToken);
        return stockTaxPointers.ToDictionary(
            row => row.StockCode,
            row => new CompanyMovementLineTaxInfo(
                row.TaxPointer,
                taxRates.TryGetValue(row.TaxPointer, out var taxRatePercent) ? taxRatePercent : 0m),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<int, decimal>> LoadTaxRatesAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, decimal>();
        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DepartmentNo, Rate
                FROM dbo.fn_hs_vergi_oran_listesi();
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var departmentNo = Convert.ToInt32(reader["DepartmentNo"]);
                var rate = Convert.ToDecimal(reader["Rate"]);
                result[departmentNo] = rate;
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }

    private static STOK_HAREKETLERI CreateMovement(
        CreateCompanyMovementRequest request,
        CreateCompanyMovementLineRequest line,
        string customerCode,
        int customerAddressNo,
        byte movementGenre,
        byte returnType,
        int rowNo,
        DateTime now,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string documentSerie,
        int documentOrderNo,
        string offlineTraceKey,
        IReadOnlyDictionary<string, CompanyMovementLineTaxInfo> lineTaxInfos)
    {
        var unitPrice = line.UnitPrice;
        var amount = line.Quantity * unitPrice;
        var taxInfo = ResolveLineTaxInfo(line, lineTaxInfos);

        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = MovementFileId,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = false,
            sth_checksum = 0,
            sth_create_user = MikroUserNo,
            sth_create_date = now,
            sth_lastup_user = MikroUserNo,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = movementDate,
            sth_tip = OutgoingMovementType,
            sth_cins = movementGenre,
            sth_normal_iade = returnType,
            sth_evraktip = CompanyDispatchDocumentType,
            sth_evrakno_seri = documentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = documentNo,
            sth_belge_tarih = documentDate,
            sth_stok_kod = line.StockCode.Trim(),
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 1,
            sth_isk_mas3 = 1,
            sth_isk_mas4 = 1,
            sth_isk_mas5 = 1,
            sth_isk_mas6 = 1,
            sth_isk_mas7 = 1,
            sth_isk_mas8 = 1,
            sth_isk_mas9 = 1,
            sth_isk_mas10 = 1,
            sth_sat_iskmas1 = false,
            sth_sat_iskmas2 = false,
            sth_sat_iskmas3 = false,
            sth_sat_iskmas4 = false,
            sth_sat_iskmas5 = false,
            sth_sat_iskmas6 = false,
            sth_sat_iskmas7 = false,
            sth_sat_iskmas8 = false,
            sth_sat_iskmas9 = false,
            sth_sat_iskmas10 = false,
            sth_pos_satis = 0,
            sth_promosyon_fl = false,
            sth_cari_cinsi = 0,
            sth_cari_kodu = customerCode,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = line.Quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = Convert.ToByte(line.UnitPointer),
            sth_tutar = amount,
            sth_iskonto1 = 0d,
            sth_iskonto2 = 0d,
            sth_iskonto3 = 0d,
            sth_iskonto4 = 0d,
            sth_iskonto5 = 0d,
            sth_iskonto6 = 0d,
            sth_masraf1 = 0d,
            sth_masraf2 = 0d,
            sth_masraf3 = 0d,
            sth_masraf4 = 0d,
            sth_vergi_pntr = taxInfo.TaxPointer,
            sth_vergi = CalculateTaxAmount(amount, taxInfo.TaxRatePercent),
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = NormalizeText(line.Description ?? request.Description),
            sth_sip_uid = line.OrderLineGuid ?? Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = ResolveIncomingWarehouseNo(returnType),
            sth_cikis_depo_no = request.WarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = NormalizeText(line.CustomerResponsibilityCenter),
            sth_stok_srm_merkezi = NormalizeText(line.ProductResponsibilityCenter),
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = customerAddressNo,
            sth_parti_kodu = NormalizeText(line.PartyCode),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = NormalizeText(line.ProjectCode),
            sth_exim_kodu = string.Empty,
            sth_otv_pntr = 0,
            sth_otv_vergi = 0d,
            sth_brutagirlik = 0d,
            sth_disticaret_turu = 0,
            sth_otvtutari = 0d,
            sth_otvvergisiz_fl = false,
            sth_oiv_pntr = 0,
            sth_oiv_vergi = 0d,
            sth_oivvergisiz_fl = false,
            sth_fiyat_liste_no = 1,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = 0,
            sth_nakliyedurumu = 0,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = string.Empty,
            sth_HareketGrupKodu2 = NormalizeText(request.Deliverer),
            sth_HareketGrupKodu3 = NormalizeText(request.Receiver),
            sth_Olcu1 = 0d,
            sth_Olcu2 = 0d,
            sth_Olcu3 = 0d,
            sth_Olcu4 = 0d,
            sth_Olcu5 = 0d,
            sth_FormulMiktarNo = 0,
            sth_FormulMiktar = 0d,
            sth_eirs_senaryo = 0,
            sth_eirs_tipi = 0,
            sth_teslim_tarihi = movementDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = offlineTraceKey,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static CompanyMovementLineTaxInfo ResolveLineTaxInfo(
        CreateCompanyMovementLineRequest line,
        IReadOnlyDictionary<string, CompanyMovementLineTaxInfo> lineTaxInfos)
    {
        var stockCode = line.StockCode.Trim();
        return lineTaxInfos.TryGetValue(stockCode, out var taxInfo)
            ? taxInfo
            : CompanyMovementLineTaxInfo.Empty;
    }

    internal static double CalculateTaxAmount(double amount, decimal taxRatePercent)
    {
        if (amount <= 0d || taxRatePercent <= 0m)
        {
            return 0d;
        }

        return (double)Math.Round((decimal)amount * taxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    internal static byte ResolveStockTaxPointer(
        CompanyMovementKind kind,
        byte retailTaxPointer,
        byte wholesaleTaxPointer) =>
        kind switch
        {
            CompanyMovementKind.OutgoingShipment => retailTaxPointer,
            CompanyMovementKind.PurchaseReturn => retailTaxPointer,
            _ => wholesaleTaxPointer
        };

    private static int ResolveCustomerAddressNo(CARI_HESAPLAR customer)
    {
        var addressNo = customer.cari_sevk_adres_no ?? customer.cari_fatura_adres_no ?? 1;
        return addressNo > 0 ? addressNo : 1;
    }

    private static byte ResolveReturnType(CompanyMovementKind kind) =>
        kind switch
        {
            CompanyMovementKind.OutgoingShipment => NormalMovement,
            CompanyMovementKind.PurchaseReturn => ReturnMovement,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported company movement kind for create.")
        };

    private static byte ResolveMovementGenre(CompanyMovementKind kind) =>
        kind switch
        {
            CompanyMovementKind.OutgoingShipment => RetailMovementGenre,
            CompanyMovementKind.PurchaseReturn => WholesaleMovementGenre,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported company movement kind for create.")
        };

    private static int ResolveIncomingWarehouseNo(byte returnType) =>
        returnType == ReturnMovement ? 1 : 0;

    private static string ResolveOfflineOperationCode(CompanyMovementKind kind) =>
        kind switch
        {
            CompanyMovementKind.OutgoingShipment => CompanyShipmentOfflineOperationCode,
            CompanyMovementKind.PurchaseReturn => CompanyReturnOfflineOperationCode,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported company movement kind for create.")
        };

    private static string ResolveOfflineTraceKey(Guid? clientRequestId) =>
        clientRequestId.HasValue ? MobileOfflineSyncService.ToTraceKey(clientRequestId.Value) : string.Empty;

    private static void Validate(CreateCompanyMovementRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            throw new ArgumentException("Customer code is required.", nameof(request.CustomerCode));
        }

        if (request.DocumentDate.HasValue &&
            request.MovementDate.HasValue &&
            request.DocumentDate.Value.Date < request.MovementDate.Value.Date)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one movement line is required.", nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required.", nameof(request.Lines));
            }

            if (line.Quantity <= 0)
            {
                throw new ArgumentException("Line quantity must be greater than zero.", nameof(request.Lines));
            }

            if (line.UnitPrice < 0)
            {
                throw new ArgumentException("Line unit price can not be negative.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }

            if (line.LotNo < 0)
            {
                throw new ArgumentException("Line lot no can not be negative.", nameof(request.Lines));
            }
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

internal sealed record CompanyMovementLineTaxInfo(byte TaxPointer, decimal TaxRatePercent)
{
    public static readonly CompanyMovementLineTaxInfo Empty = new(0, 0m);
}
