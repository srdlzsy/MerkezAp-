using System.Text.Json;
using System.Globalization;
using System.Reflection;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

public sealed partial class UpdateWarehouseShippingDocumentUseCase
{
    private const string StockMovementUpdatePath = "/Api/apiMethods/DahiliStokHareketDuzeltV2";
    private const string StockMovementGuidDeletePath = "/Api/apiMethods/DahiliStokHareketGuidSilV2";
    private const string WarehouseOrderUpdatePath = "/Api/apiMethods/DepolarArasiSiparisDuzeltV2";
    private const string WarehouseOrderGuidDeletePath = "/Api/apiMethods/DepolarArasiSiparisGuidSilV2";

    private async Task<UpdateWarehouseShippingDocumentResponse> ExecuteMikroApiAsync(
        UpdateWarehouseShippingDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var normalReturn = request.IsReturn ? ReturnMovement : NormalMovement;
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_iptal != true &&
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_tip == MovementType &&
                movement.sth_cins == MovementGenre &&
                movement.sth_normal_iade == normalReturn &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == request.DocumentOrderNo &&
                movement.sth_cikis_depo_no == request.SourceWarehouseNo)
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToArrayAsync(cancellationToken);
        if (rows.Length == 0)
        {
            throw new KeyNotFoundException(request.IsReturn
                ? "Warehouse return document was not found in Mikro write database."
                : "Inter warehouse shipment document was not found in Mikro write database.");
        }

        EnsureSingleDocument(rows, request.IsReturn);
        EnsureDocumentCanBeUpdated(rows, request);
        ValidateEffectiveDocumentState(rows[0], request);
        await EnsureRequestedStocksExistAsync(request, cancellationToken);

        var movementGuids = rows.Select(row => row.sth_Guid).ToArray();
        var movementExtras = await mikroWriteDbContext.STOK_HAREKETLERI_EKs
            .AsNoTracking()
            .Where(extra =>
                extra.sthek_iptal != true &&
                extra.sthek_related_uid.HasValue &&
                movementGuids.Contains(extra.sthek_related_uid.Value) &&
                extra.sth_subesip_uid.HasValue &&
                extra.sth_subesip_uid.Value != Guid.Empty)
            .ToArrayAsync(cancellationToken);
        var linkedOrderGuids = movementExtras.Select(extra => extra.sth_subesip_uid!.Value).Distinct().ToArray();
        var linkedOrders = linkedOrderGuids.Length == 0
            ? new Dictionary<Guid, DEPOLAR_ARASI_SIPARISLER>()
            : await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
                .AsNoTracking()
                .Where(order => linkedOrderGuids.Contains(order.ssip_Guid))
                .ToDictionaryAsync(order => order.ssip_Guid, cancellationToken);
        EnsureLinkedOrdersExist(linkedOrderGuids, linkedOrders);

        var targetWarehouseNo = request.TargetWarehouseNo ?? rows[0].sth_nakliyedeposu ?? 0;
        var transitWarehouseNo = request.TransitWarehouseNo ?? rows[0].sth_giris_depo_no ?? 0;
        ValidateEffectiveWarehouses(request.SourceWarehouseNo, targetWarehouseNo, transitWarehouseNo);
        EnsureLinkedOrdersMatchDocument(request.IsReturn, request.SourceWarehouseNo, targetWarehouseNo, linkedOrders.Values);

        var orderGuidsByMovementGuid = movementExtras
            .GroupBy(extra => extra.sthek_related_uid!.Value)
            .ToDictionary(group => group.Key, group => group.Select(extra => extra.sth_subesip_uid!.Value).Distinct().ToArray());
        var rowsByGuid = rows.ToDictionary(row => row.sth_Guid);
        var movementLastUpdateDates = rows.ToDictionary(row => row.sth_Guid, row => row.sth_lastup_date);
        var orderLastUpdateDates = linkedOrders.ToDictionary(pair => pair.Key, pair => pair.Value.ssip_lastup_date);
        var updateUser = ResolveMikroUserNo(request.SourceWarehouseNo);
        var updatedAt = DateTime.Now;
        var touchedRows = new HashSet<Guid>();
        var addedRows = new List<STOK_HAREKETLERI>();
        var deletedRows = new HashSet<Guid>();
        var quantityChanges = new Dictionary<Guid, LineQuantityChange>();
        var usedRowNos = rows.Where(row => row.sth_satirno.HasValue).Select(row => row.sth_satirno!.Value).ToHashSet();
        var nextRowNo = usedRowNos.Count == 0 ? 0 : usedRowNos.Max() + 1;

        if (HasHeaderPatch(request))
        {
            foreach (var row in rows) { ApplyHeaderPatch(row, request); touchedRows.Add(row.sth_Guid); }
        }

        foreach (var line in request.Lines)
        {
            var action = NormalizeLineAction(line.Action);
            if (action == LineActionAdd)
            {
                var rowNo = ResolveAddedRowNo(line, usedRowNos, ref nextRowNo);
                var added = CreateAddedMovement(rows[0], request, line, rowNo, updatedAt, updateUser);
                if (HasHeaderPatch(request)) ApplyHeaderPatch(added, request);
                addedRows.Add(added); rowsByGuid.Add(added.sth_Guid, added); touchedRows.Add(added.sth_Guid);
                continue;
            }

            var movementGuid = line.MovementGuid!.Value;
            if (!rowsByGuid.TryGetValue(movementGuid, out var row)) throw new KeyNotFoundException($"Warehouse shipping line was not found: {movementGuid}");
            if (action == LineActionDelete)
            {
                var oldQuantity = row.sth_miktar ?? 0d;
                touchedRows.Add(row.sth_Guid); deletedRows.Add(row.sth_Guid);
                quantityChanges[row.sth_Guid] = new(row.sth_Guid, oldQuantity, 0d);
                continue;
            }
            if (!HasLinePatch(line)) continue;
            var previousQuantity = row.sth_miktar ?? 0d;
            if (ApplyLinePatch(row, line))
            {
                touchedRows.Add(row.sth_Guid);
                quantityChanges[row.sth_Guid] = new(row.sth_Guid, previousQuantity, row.sth_miktar ?? 0d);
            }
        }
        if (touchedRows.Count == 0) throw new ArgumentException("At least one warehouse shipping field must be provided.", nameof(request));

        ApplyLinkedOrderUpdates(request.IsReturn, request.SourceWarehouseNo, targetWarehouseNo, orderGuidsByMovementGuid,
            linkedOrders, rowsByGuid, touchedRows, deletedRows, quantityChanges, updatedAt, updateUser);
        foreach (var row in rows.Where(row => touchedRows.Contains(row.sth_Guid) && !deletedRows.Contains(row.sth_Guid)))
        {
            row.sth_lastup_user = updateUser;
            row.sth_lastup_date = movementLastUpdateDates[row.sth_Guid];
            row.sth_degisti = true;
        }
        foreach (var order in linkedOrders.Values)
        {
            order.ssip_lastup_date = orderLastUpdateDates[order.ssip_Guid];
        }

        var activeBeforeWrite = rows.Concat(addedRows).Where(row => !deletedRows.Contains(row.sth_Guid)).ToArray();
        if (activeBeforeWrite.Length == 0) throw new ArgumentException("Warehouse shipping document must have at least one active line.", nameof(request.Lines));

        var upserts = rows.Where(row => touchedRows.Contains(row.sth_Guid) && !deletedRows.Contains(row.sth_Guid)).Concat(addedRows).ToArray();
        if (upserts.Length > 0) await PostShippingRowsAsync(StockMovementUpdatePath, upserts, cancellationToken);

        var returnOrderDeletes = request.IsReturn
            ? deletedRows.SelectMany(guid => orderGuidsByMovementGuid.GetValueOrDefault(guid) ?? []).Distinct().ToHashSet()
            : [];
        var orderUpdates = linkedOrders.Values.Where(order => !returnOrderDeletes.Contains(order.ssip_Guid)).ToArray();
        if (orderUpdates.Length > 0) await PostShippingRowsAsync(WarehouseOrderUpdatePath, orderUpdates, cancellationToken);
        if (deletedRows.Count > 0) await PostShippingRowsAsync(StockMovementGuidDeletePath, deletedRows.Select(guid => new { sth_Guid = guid }), cancellationToken);
        if (returnOrderDeletes.Count > 0) await PostShippingRowsAsync(WarehouseOrderGuidDeletePath, returnOrderDeletes.Select(guid => new { ssip_Guid = guid }), cancellationToken);

        STOK_HAREKETLERI[] verified = [];
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            verified = await mikroWriteDbContext.STOK_HAREKETLERIs.AsNoTracking()
                .Where(movement => movement.sth_iptal != true && movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                    movement.sth_tip == MovementType && movement.sth_cins == MovementGenre && movement.sth_normal_iade == normalReturn &&
                    movement.sth_evrakno_seri == documentSerie && movement.sth_evrakno_sira == request.DocumentOrderNo &&
                    movement.sth_cikis_depo_no == request.SourceWarehouseNo)
                .ToArrayAsync(cancellationToken);
            if (verified.Length == activeBeforeWrite.Length) break;
            if (attempt < 3) await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
        }
        if (verified.Length != activeBeforeWrite.Length) throw new InvalidOperationException("Mikro API warehouse shipping update succeeded, but the final line set could not be verified.");

        return new(documentSerie, request.DocumentOrderNo, request.SourceWarehouseNo,
            verified[0].sth_nakliyedeposu ?? targetWarehouseNo, verified[0].sth_giris_depo_no ?? transitWarehouseNo,
            request.IsReturn, touchedRows.Count, addedRows.Count, deletedRows.Count, verified.Length,
            verified.Sum(row => row.sth_miktar ?? 0d), verified.Sum(row => row.sth_tutar ?? 0d), updatedAt, updateUser,
            mikroWriteOptions.Value.ConnectionStringName);
    }

    private async Task PostShippingRowsAsync<T>(string path, IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        var payload = new { evraklar = new[] { new { satirlar = rows.Select(ToShippingApiRow).ToArray() } } };
        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(path, payload, cancellationToken);
        if (result.IsError) throw new InvalidOperationException(result.ErrorMessage ?? $"Mikro API warehouse shipping request failed: {path}");
    }

    internal static Dictionary<string, object?> ToShippingApiRow<T>(T row)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in row!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var name = property.Name;
            if (name.EndsWith("_DBCno", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_SpecRECno", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_fileid", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_checksum", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_create_user", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_create_date", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_lastup_user", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_lastup_date", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_degisti", StringComparison.OrdinalIgnoreCase)) continue;
            var value = property.GetValue(row);
            if (value is null) continue;
            result[name] = value is DateTime date
                ? date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                : value;
        }
        return result;
    }
}
