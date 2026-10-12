using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Services;

internal static class CompanyEDespatchCreateGuard
{
    private const string CompanyShipmentCreateOperation = "sevk-islemleri.giden-firma-sevkleri.create";
    private const string CompanyReturnCreateOperation = "iade-islemleri.firma-iadeleri.create";
    private const string CompanyReceivingCreateOperation = "mal-kabul-islemleri.firma-mal-kabulleri.create";
    private const string AutoReturnCreatedStatus = "IadeOlusturuldu";
    private const double QuantityTolerance = 0.0001d;

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
                EnsureAutomaticReceivingReturn(create, requestId, request, rows, options);
                return;
            }

            EnsureDirectCompanyMovement(create, requestId, request, rows, isReturn, options);
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
        JsonSerializerOptions options)
    {
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

    private static void EnsureAutomaticReceivingReturn(
        MobileOfflineSyncRequest create,
        Guid requestId,
        SendEDespatchRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows,
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
            rows.Any(row => row.sth_evrakno_seri != request.DocumentSerie || row.sth_evrakno_sira != request.DocumentOrderNo))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        var returnResults = response.Lines
            .Where(line => line.ReturnMovementGuid.HasValue)
            .ToArray();
        if (returnResults.Length == 0 || returnResults.Length != rows.Count ||
            response.AutoCreatedReturnLineCount != returnResults.Length ||
            returnResults.Select(line => line.ReturnMovementGuid).Distinct().Count() != returnResults.Length ||
            returnResults.Select(line => line.SourceLineNo).Distinct().Count() != returnResults.Length ||
            !NearlyEquals(response.TotalReturnedQuantity, returnResults.Sum(line => line.ReturnQuantity)))
            throw StockMovementRecoveryMatcher.ContentMismatch();

        var expectedLines = new List<CreateCompanyMovementLineRequest>(rows.Count);
        foreach (var row in rows.OrderBy(row => row.sth_satirno))
        {
            var result = returnResults.SingleOrDefault(line => line.ReturnMovementGuid == row.sth_Guid);
            if (result is null || result.SourceLineNo < 0 || result.SourceLineNo >= original.Lines.Count ||
                result.ReturnQuantity <= QuantityTolerance || result.ReturnStatus != AutoReturnCreatedStatus ||
                result.ReturnDocumentSerie != request.DocumentSerie || result.ReturnDocumentOrderNo != request.DocumentOrderNo)
                throw StockMovementRecoveryMatcher.ContentMismatch();

            var source = original.Lines.ElementAt(result.SourceLineNo);
            if (!TextEquals(result.StockCode, source.StockCode))
                throw StockMovementRecoveryMatcher.ContentMismatch();

            expectedLines.Add(new CreateCompanyMovementLineRequest(
                source.StockCode,
                result.ReturnQuantity,
                source.UnitPrice,
                source.UnitPointer,
                source.Description,
                Truncate(source.PartyCode, 25),
                source.LotNo,
                Truncate(source.ProjectCode, 25),
                Truncate(source.CustomerResponsibilityCenter, 25),
                Truncate(source.ProductResponsibilityCenter, 25)));
        }

        var expected = new CreateCompanyMovementRequest(
            original.WarehouseNo,
            original.CustomerCode,
            original.MovementDate,
            original.DocumentDate,
            null,
            original.Description,
            expectedLines,
            ClientRequestId: original.ClientRequestId,
            RequestedByUserId: original.RequestedByUserId);
        if (!StockMovementRecoveryMatcher.IsCompleteOrThrow(
                StockMovementRecoveryMatcher.Company(expected, 0, 1, rows)))
            throw StockMovementRecoveryMatcher.OutcomeUnconfirmed();
    }

    private static bool NearlyEquals(double actual, double expected) =>
        double.IsFinite(actual) && double.IsFinite(expected) && Math.Abs(actual - expected) <= QuantityTolerance;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(actual?.Trim() ?? string.Empty, expected?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
