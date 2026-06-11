using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

public sealed class InventoryCountWriteService(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    MobileOfflineSyncService mobileOfflineSyncService,
    ILogger<InventoryCountWriteService> logger)
{
    private const short CensusFileId = 28;
    private const short MikroUserNo = 39;
    private const string OfflineOperationCode = "stok-islemleri.sayim-sonuclari.create";
    private const string SayimSonuclariKaydetPath = "/Api/apiMethods/SayimSonuclariKaydetV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;

    public async Task<CreateInventoryCountResponse> ExecuteAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        if (request.ClientRequestId.HasValue)
        {
            var acquireResult = await mobileOfflineSyncService.AcquireAsync<CreateInventoryCountRequest, CreateInventoryCountResponse>(
                OfflineOperationCode,
                request.RequestedByUserId,
                request.WarehouseNo,
                request.ClientRequestId.Value,
                request,
                (_, innerCancellationToken) => TryRecoverOfflineResponseAsync(
                    request.WarehouseNo,
                    request.ClientRequestId.Value,
                    innerCancellationToken),
                cancellationToken);

            if (acquireResult.State == MobileOfflineSyncAcquireState.Completed)
            {
                return acquireResult.Response!;
            }

            if (acquireResult.State == MobileOfflineSyncAcquireState.Processing)
            {
                throw new InvalidOperationException(
                    "An offline inventory count sync request with the same clientRequestId is already being processed.");
            }
        }

        try
        {
            var response = mikroWriteRoutingOptions.CurrentValue.InventoryCount switch
            {
                MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
                MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
                MikroWriteMode.DualShadow => await ExecuteDualShadowAsync(request, cancellationToken),
                var mode => throw new InvalidOperationException(
                    $"Unsupported MikroWriteRouting:InventoryCount mode '{mode}'.")
            };

            if (request.ClientRequestId.HasValue)
            {
                await mobileOfflineSyncService.CompleteAsync(
                    OfflineOperationCode,
                    request.RequestedByUserId,
                    request.ClientRequestId.Value,
                    response,
                    cancellationToken);
            }

            return response;
        }
        catch (Exception exception)
        {
            if (request.ClientRequestId.HasValue)
            {
                await TryMarkFailedAsync(
                    request.RequestedByUserId,
                    request.ClientRequestId.Value,
                    exception.Message,
                    cancellationToken);
            }

            throw;
        }
    }

    private async Task<CreateInventoryCountResponse> ExecuteDatabaseAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var documentDate = (request.DocumentDate ?? DateTime.Today).Date;
        var name = NormalizeText(request.Name, 25);
        var lines = request.Lines.ToArray();
        var barcodeLookup = await BuildBarcodeLookupAsync(lines, cancellationToken);
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();
        var offlineTraceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentNo = await GetNextDocumentNoAsync(request.WarehouseNo, cancellationToken);
                var results = lines
                    .Select((line, rowNo) => CreateResult(
                        request.WarehouseNo,
                        documentNo,
                        documentDate,
                        name,
                        line,
                        barcodeLookup,
                        rowNo,
                        now,
                        offlineTraceKey))
                    .ToArray();

                await mikroWriteDbContext.SAYIM_SONUCLARIs.AddRangeAsync(results, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateInventoryCountResponse(
                    documentNo,
                    documentDate,
                    request.WarehouseNo,
                    name,
                    results.Length,
                    results.Sum(item => item.sym_miktar1 ?? 0d),
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<CreateInventoryCountResponse> ExecuteMikroApiAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var documentDate = (request.DocumentDate ?? DateTime.Today).Date;
        var name = NormalizeText(request.Name, 25);
        var lines = request.Lines.ToArray();
        var barcodeLookup = await BuildBarcodeLookupAsync(lines, cancellationToken);
        var traceKey = CreateMikroApiTraceKey(request.ClientRequestId);

        var existingResponse = await TryRecoverInventoryCountResponseByTraceAsync(
            request.WarehouseNo,
            traceKey,
            options.ConnectionStringName,
            cancellationToken);

        if (existingResponse is not null)
        {
            return existingResponse;
        }

        var payload = InventoryCountMikroApiPayloadFactory.Create(
            request.WarehouseNo,
            documentDate,
            name,
            lines,
            barcodeLookup,
            traceKey);

        logger.LogInformation(
            "Inventory count create is routed to Mikro API {Path}. WarehouseNo={WarehouseNo}, LineCount={LineCount}, TraceKey={TraceKey}",
            SayimSonuclariKaydetPath,
            request.WarehouseNo,
            lines.Length,
            traceKey);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            SayimSonuclariKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API inventory count create failed.");
        }

        return await RecoverMikroApiCreateResponseAsync(
            request.WarehouseNo,
            traceKey,
            options.ConnectionStringName,
            cancellationToken);
    }

    private async Task<CreateInventoryCountResponse> ExecuteDualShadowAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:InventoryCount is DualShadow. SayimSonuclariKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    public Task<OfflineSyncStatusDto<CreateInventoryCountResponse>> GetOfflineSyncStatusAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        mobileOfflineSyncService.GetStatusAsync<CreateInventoryCountResponse>(
            OfflineOperationCode,
            requestedByUserId,
            clientRequestId,
            (_, innerCancellationToken) => TryRecoverOfflineResponseAsync(
                warehouseNo,
                clientRequestId,
                innerCancellationToken),
            cancellationToken);

    private async Task<int> GetNextDocumentNoAsync(int warehouseNo, CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.SAYIM_SONUCLARIs
            .Where(item => item.sym_depono == warehouseNo)
            .MaxAsync(item => item.sym_evrakno, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : 1;
    }

    private async Task<Dictionary<string, string>> BuildBarcodeLookupAsync(
        IReadOnlyCollection<CreateInventoryCountLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var stockCodes = lines
            .Where(line => string.IsNullOrWhiteSpace(line.Barcode) == true)
            .Select(line => line.StockCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (stockCodes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var barcodeRows = await mikroWriteDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(item =>
                item.bar_stokkodu != null &&
                stockCodes.Contains(item.bar_stokkodu) &&
                item.bar_kodu != null)
            .OrderByDescending(item => item.bar_master == true)
            .ThenBy(item => item.bar_create_date)
            .Select(item => new { item.bar_stokkodu, item.bar_kodu })
            .ToListAsync(cancellationToken);

        return barcodeRows
            .GroupBy(item => item.bar_stokkodu!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.bar_kodu!).FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static SAYIM_SONUCLARI CreateResult(
        int warehouseNo,
        int documentNo,
        DateTime documentDate,
        string name,
        CreateInventoryCountLineRequest line,
        IReadOnlyDictionary<string, string> barcodeLookup,
        int rowNo,
        DateTime now,
        string offlineTraceKey)
    {
        var stockCode = NormalizeText(line.StockCode, 25);
        var barcode = ResolveBarcode(line, stockCode, barcodeLookup);

        return new SAYIM_SONUCLARI
        {
            sym_Guid = Guid.NewGuid(),
            sym_DBCno = 0,
            sym_SpecRECno = 0,
            sym_iptal = false,
            sym_fileid = CensusFileId,
            sym_hidden = false,
            sym_kilitli = false,
            sym_degisti = false,
            sym_checksum = 0,
            sym_create_user = MikroUserNo,
            sym_create_date = now,
            sym_lastup_user = MikroUserNo,
            sym_lastup_date = now,
            sym_special1 = string.Empty,
            sym_special2 = string.Empty,
            sym_special3 = string.Empty,
            sym_tarihi = documentDate,
            sym_depono = warehouseNo,
            sym_evrakno = documentNo,
            sym_satirno = rowNo,
            sym_Stokkodu = stockCode,
            sym_reyonkodu = string.Empty,
            sym_koridorkodu = string.Empty,
            sym_rafkodu = string.Empty,
            sym_miktar1 = line.Quantity,
            sym_miktar2 = 0d,
            sym_miktar3 = 0d,
            sym_miktar4 = 0d,
            sym_miktar5 = 0d,
            sym_birim_pntr = Convert.ToByte(line.UnitPointer),
            sym_barkod = barcode,
            sym_renkno = 0,
            sym_bedenno = 0,
            sym_parti_kodu = name,
            sym_lot_no = 0,
            sym_serino = offlineTraceKey
        };
    }

    private async Task<CreateInventoryCountResponse?> TryRecoverOfflineResponseAsync(
        int warehouseNo,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var traceKey = MobileOfflineSyncService.ToTraceKey(clientRequestId);
        return await TryRecoverInventoryCountResponseByTraceAsync(
            warehouseNo,
            traceKey,
            mikroWriteOptions.Value.ConnectionStringName,
            cancellationToken);
    }

    private async Task<CreateInventoryCountResponse> RecoverMikroApiCreateResponseAsync(
        int warehouseNo,
        string traceKey,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverInventoryCountResponseByTraceAsync(
                warehouseNo,
                traceKey,
                writeConnectionName,
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

        throw new InvalidOperationException(
            "Mikro API inventory count create succeeded, but created SAYIM_SONUCLARI rows could not be read back.");
    }

    private async Task<CreateInventoryCountResponse?> TryRecoverInventoryCountResponseByTraceAsync(
        int warehouseNo,
        string traceKey,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(traceKey))
        {
            return null;
        }

        var rows = await mikroWriteDbContext.SAYIM_SONUCLARIs
            .AsNoTracking()
            .Where(item =>
                item.sym_depono == warehouseNo &&
                item.sym_serino == traceKey)
            .Select(item => new
            {
                item.sym_tarihi,
                item.sym_evrakno,
                item.sym_depono,
                item.sym_parti_kodu,
                item.sym_miktar1
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var headerCount = rows
            .Select(item => new
            {
                item.sym_tarihi,
                item.sym_evrakno,
                item.sym_depono,
                item.sym_parti_kodu
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one inventory count document matched the same clientRequestId trace.");
        }

        var firstRow = rows[0];
        var resolvedDocumentDate = firstRow.sym_tarihi?.Date ?? DateTime.Today;

        return new CreateInventoryCountResponse(
            firstRow.sym_evrakno ?? 0,
            resolvedDocumentDate,
            firstRow.sym_depono ?? warehouseNo,
            firstRow.sym_parti_kodu ?? string.Empty,
            rows.Count,
            rows.Sum(item => item.sym_miktar1 ?? 0d),
            writeConnectionName);
    }

    private static string CreateMikroApiTraceKey(Guid? clientRequestId) =>
        clientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(clientRequestId.Value)
            : $"api-{Guid.NewGuid():N}";

    private static string ResolveBarcode(
        CreateInventoryCountLineRequest line,
        string stockCode,
        IReadOnlyDictionary<string, string> barcodeLookup)
    {
        var barcode = NormalizeText(line.Barcode, 50);
        if (!string.IsNullOrEmpty(barcode))
        {
            return barcode;
        }

        return barcodeLookup.TryGetValue(stockCode, out var resolvedBarcode)
            ? NormalizeText(resolvedBarcode, 50)
            : string.Empty;
    }

    private static void Validate(CreateInventoryCountRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.RequestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one census line is required.", nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required.", nameof(request.Lines));
            }

            if (line.Quantity < 0)
            {
                throw new ArgumentException("Line quantity can not be negative.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }
        }
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private async Task TryMarkFailedAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await mobileOfflineSyncService.MarkFailedAsync(
                OfflineOperationCode,
                requestedByUserId,
                clientRequestId,
                errorMessage,
                cancellationToken);
        }
        catch
        {
            // Best effort only; preserve the original business exception.
        }
    }
}
