using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Services;

internal static class CompanyEDespatchCreateGuard
{
    internal static async Task EnsureCompleteAsync(AuthDbContext db, SendEDespatchRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows, CancellationToken cancellationToken)
    {
        var traces = rows.Select(row => row.sth_eticaret_kanal_kodu)
            .Where(trace => trace?.StartsWith("FR", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal).ToArray();
        // External/legacy documents have no local create request. Their expectedLineCount
        // and snapshot are still validated by the send pipeline.
        if (traces.Length == 0) return;

        if (traces.Length != 1 || rows.Any(row => row.sth_eticaret_kanal_kodu != traces[0]) ||
            !EDespatchService.TryParseOfflineTraceKey(traces[0]!, out var requestId))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        var isReturn = request.DocumentType == EDespatchDocumentType.CompanyReturn;
        var operationCode = isReturn ? "iade-islemleri.firma-iadeleri.create" : "sevk-islemleri.giden-firma-sevkleri.create";
        var creates = await db.MobileOfflineSyncRequests.AsNoTracking()
            .Where(record => record.OperationCode == operationCode && record.WarehouseNo == request.WarehouseNo &&
                record.ClientRequestId == requestId.ToString("D"))
            .Take(2).ToListAsync(cancellationToken);
        if (creates.Count > 1) throw StockMovementRecoveryMatcher.ContentMismatch();
        var create = creates.SingleOrDefault();
        if (create?.Retryable == false) throw StockMovementRecoveryMatcher.ContentMismatch();
        if (create?.Status != MobileOfflineSyncRequestStatus.Completed)
            throw StockMovementRecoveryMatcher.OutcomeUnconfirmed();

        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var original = JsonSerializer.Deserialize<CreateCompanyMovementRequest>(create.RequestPayload ?? "null", options);
            var response = JsonSerializer.Deserialize<CreateCompanyMovementResponse>(create.ResponsePayload ?? "null", options);
            if (original?.Lines is null || original.ClientRequestId != requestId ||
                original.WarehouseNo != request.WarehouseNo || response is null ||
                response.DocumentSerie != request.DocumentSerie || response.DocumentOrderNo != request.DocumentOrderNo ||
                response.WarehouseNo != request.WarehouseNo || response.LineCount != original.Lines.Count ||
                rows.Any(row => row.sth_evrakno_seri != request.DocumentSerie || row.sth_evrakno_sira != request.DocumentOrderNo))
                throw StockMovementRecoveryMatcher.ContentMismatch();

            if (!StockMovementRecoveryMatcher.IsCompleteOrThrow(StockMovementRecoveryMatcher.Company(
                    original, isReturn ? (byte)0 : (byte)1, isReturn ? (byte)1 : (byte)0, rows)))
                throw StockMovementRecoveryMatcher.OutcomeUnconfirmed();
        }
        catch (JsonException)
        {
            throw StockMovementRecoveryMatcher.ContentMismatch();
        }
    }
}
