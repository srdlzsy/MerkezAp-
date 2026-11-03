using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Services;

internal static class CompanyEDespatchCreateGuard
{
    private const string CompanyShipmentCreateOperation = "sevk-islemleri.giden-firma-sevkleri.create";
    private const string CompanyReturnCreateOperation = "iade-islemleri.firma-iadeleri.create";
    private const string CompanyReceivingCreateOperation = "mal-kabul-islemleri.firma-mal-kabulleri.create";
    private const string AutoReturnCreatedStatus = "IadeOlusturuldu";
    private const double QuantityTolerance = 0.0001d;

    internal static Task EnsureCompleteAsync(AuthDbContext db, SendEDespatchRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows, CancellationToken cancellationToken) =>
        EnsureCompleteAsync(db, request, rows, null, cancellationToken);

    internal static async Task EnsureCompleteAsync(AuthDbContext db, SendEDespatchRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows, ILogger? logger, CancellationToken cancellationToken)
    {
        var traces = rows.Select(row => row.sth_eticaret_kanal_kodu)
            .Where(trace => trace?.StartsWith("FR", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal).ToArray();
        // External/legacy documents have no local create request. Their expectedLineCount
        // and snapshot are still validated by the send pipeline.
        if (traces.Length == 0) return;

        // If multiple distinct FR trace keys exist, rows from different requests were mixed.
        // Traced rows must share the same trace key. Rows added directly in Mikro Desktop (null/empty trace) are allowed.
        if (traces.Length != 1 ||
            rows.Where(row => row.sth_eticaret_kanal_kodu?.StartsWith("FR", StringComparison.Ordinal) == true)
                .Any(row => row.sth_eticaret_kanal_kodu != traces[0]) ||
            !EDespatchService.TryParseOfflineTraceKey(traces[0]!, out var requestId))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        var isReturn = request.DocumentType == EDespatchDocumentType.CompanyReturn;
        var operationCodes = isReturn
            ? new[] { CompanyReturnCreateOperation, CompanyReceivingCreateOperation }
            : new[] { CompanyShipmentCreateOperation };
        var creates = await db.MobileOfflineSyncRequests.AsNoTracking()
            .Where(record => operationCodes.Contains(record.OperationCode) && record.WarehouseNo == request.WarehouseNo &&
                record.ClientRequestId == requestId.ToString("D"))
            .Take(3).ToListAsync(cancellationToken);
        if (creates.Count > 1) throw StockMovementRecoveryMatcher.ContentMismatch();
        var create = creates.SingleOrDefault();
        if (create?.Retryable == false) throw StockMovementRecoveryMatcher.ContentMismatch();
        if (create?.Status != MobileOfflineSyncRequestStatus.Completed)
            throw StockMovementRecoveryMatcher.OutcomeUnconfirmed();

        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            if (create.OperationCode == CompanyReceivingCreateOperation)
            {
                EnsureAutomaticReceivingReturn(create, requestId, request, rows, logger, options);
                return;
            }

            EnsureDirectCompanyMovement(create, requestId, request, rows, isReturn, logger, options);
        }
        catch (JsonException)
        {
            throw StockMovementRecoveryMatcher.ContentMismatch();
        }
    }

    private static void EnsureDirectCompanyMovement(
        MobileOfflineSyncRequest create,
        Guid requestId,
        SendEDespatchRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows,
        bool isReturn,
        ILogger? logger,
        JsonSerializerOptions options)
    {
        var original = JsonSerializer.Deserialize<CreateCompanyMovementRequest>(create.RequestPayload ?? "null", options);
        var response = JsonSerializer.Deserialize<CreateCompanyMovementResponse>(create.ResponsePayload ?? "null", options);
        if (original?.Lines is null || original.ClientRequestId != requestId ||
            original.WarehouseNo != request.WarehouseNo || response is null ||
            response.DocumentSerie != request.DocumentSerie || response.DocumentOrderNo != request.DocumentOrderNo ||
            response.WarehouseNo != request.WarehouseNo || response.LineCount != original.Lines.Count ||
            rows.Count == 0 ||
            rows.Any(row => row.sth_evrakno_seri != request.DocumentSerie || row.sth_evrakno_sira != request.DocumentOrderNo ||
                            row.sth_iptal == true || row.sth_evraktip != 1 || row.sth_tip != 1 ||
                            row.sth_cins != (isReturn ? 0 : 1) || row.sth_normal_iade != (isReturn ? 1 : 0) ||
                            row.sth_cikis_depo_no != request.WarehouseNo ||
                            !TextEquals(row.sth_cari_kodu, original.CustomerCode)))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        // Model 1 (ERP Priority):
        // The initial creation succeeded completely (create.Status == Completed && response.LineCount == original.Lines.Count).
        // Any subsequent line additions, deletions, or quantity modifications directly in Mikro Desktop
        // are intentional ERP revisions. Current Mikro document rows are accepted as the source of truth for the e-despatch.
        var match = StockMovementRecoveryMatcher.Company(original, isReturn ? (byte)0 : (byte)1, isReturn ? (byte)1 : (byte)0, rows);
        if (match != StockMovementMatch.Complete)
        {
            logger?.LogInformation(
                "E-despatch document {DocumentSerie}/{DocumentOrderNo} was revised in Mikro (Original lines: {OriginalCount}, Mikro lines: {MikroCount}). Proceeding with current Mikro document.",
                request.DocumentSerie, request.DocumentOrderNo, original.Lines.Count, rows.Count);
        }
    }

    private static void EnsureAutomaticReceivingReturn(
        MobileOfflineSyncRequest create,
        Guid requestId,
        SendEDespatchRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows,
        ILogger? logger,
        JsonSerializerOptions options)
    {
        if (request.DocumentType != EDespatchDocumentType.CompanyReturn)
            throw StockMovementRecoveryMatcher.ContentMismatch();

        var original = JsonSerializer.Deserialize<CreateCompanyReceivingRequest>(create.RequestPayload ?? "null", options);
        var response = JsonSerializer.Deserialize<CreateCompanyReceivingResponse>(create.ResponsePayload ?? "null", options);
        if (original?.Lines is null || response?.Lines is null ||
            original.ClientRequestId != requestId || !original.AutoCreateReturnForPartialAcceptance ||
            original.WarehouseNo != request.WarehouseNo || response.WarehouseNo != request.WarehouseNo ||
            !TextEquals(original.CustomerCode, response.CustomerCode) ||
            response.DocumentSerie != original.DocumentSerie || response.DocumentOrderNo != original.DocumentOrderNo ||
            response.AutoCreatedReturnDocumentSerie != request.DocumentSerie ||
            response.AutoCreatedReturnDocumentOrderNo != request.DocumentOrderNo ||
            rows.Count == 0 ||
            rows.Any(row => row.sth_evrakno_seri != request.DocumentSerie || row.sth_evrakno_sira != request.DocumentOrderNo ||
                            row.sth_iptal == true || row.sth_evraktip != 1 || row.sth_tip != 1 ||
                            row.sth_cins != 0 || row.sth_normal_iade != 1 ||
                            row.sth_cikis_depo_no != request.WarehouseNo ||
                            !TextEquals(row.sth_cari_kodu, original.CustomerCode)))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        var returnResults = response.Lines
            .Where(line => line.ReturnMovementGuid.HasValue)
            .ToArray();
        if (returnResults.Length == 0 ||
            response.AutoCreatedReturnLineCount != returnResults.Length ||
            returnResults.Select(line => line.ReturnMovementGuid).Distinct().Count() != returnResults.Length ||
            returnResults.Select(line => line.SourceLineNo).Distinct().Count() != returnResults.Length ||
            !NearlyEquals(response.TotalReturnedQuantity, returnResults.Sum(line => line.ReturnQuantity)) ||
            returnResults.Any(result => result.SourceLineNo < 0 || result.SourceLineNo >= original.Lines.Count ||
                result.ReturnQuantity <= QuantityTolerance || result.ReturnStatus != AutoReturnCreatedStatus ||
                result.ReturnDocumentSerie != request.DocumentSerie || result.ReturnDocumentOrderNo != request.DocumentOrderNo))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        // Model 1 (ERP Priority):
        // The auto-return was generated successfully during receiving.
        // Subsequent additions, deletions or quantity modifications in Mikro Desktop are intentional ERP revisions.
        if (rows.Count != returnResults.Length)
        {
            logger?.LogInformation(
                "Auto-created return document {DocumentSerie}/{DocumentOrderNo} was revised in Mikro (Original lines: {OriginalCount}, Mikro lines: {MikroCount}). Proceeding with current Mikro document.",
                request.DocumentSerie, request.DocumentOrderNo, returnResults.Length, rows.Count);
        }
    }

    private static bool NearlyEquals(double actual, double expected) =>
        double.IsFinite(actual) && double.IsFinite(expected) && Math.Abs(actual - expected) <= QuantityTolerance;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(actual?.Trim() ?? string.Empty, expected?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
}
