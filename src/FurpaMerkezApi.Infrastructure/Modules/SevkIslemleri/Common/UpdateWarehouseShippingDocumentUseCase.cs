using System.Data;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

public sealed class UpdateWarehouseShippingDocumentUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions)
    : IUpdateWarehouseShippingDocumentUseCase
{
    private const byte MovementType = 2;
    private const byte MovementGenre = 6;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte DeliveredToTargetWarehouseState = 1;
    private const byte WaitingShippingState = 0;
    private const short MovementFileId = 16;
    private const short FallbackMikroUserNo = 39;
    private const double QuantityTolerance = 0.000001d;
    private const string LineActionUpdate = "update";
    private const string LineActionAdd = "add";
    private const string LineActionDelete = "delete";
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<UpdateWarehouseShippingDocumentResponse> ExecuteAsync(
        UpdateWarehouseShippingDocumentRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.WarehouseShippingUpdate switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => throw CreateUnsupportedMikroApiModeException(),
            MikroWriteMode.DualShadow => throw CreateUnsupportedMikroApiModeException(),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:WarehouseShippingUpdate mode '{mode}'.")
        };
    }

    private async Task<UpdateWarehouseShippingDocumentResponse> ExecuteDatabaseAsync(
        UpdateWarehouseShippingDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var documentSerie = request.DocumentSerie.Trim();
                var normalReturn = request.IsReturn ? ReturnMovement : NormalMovement;
                var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
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
                    throw new KeyNotFoundException(
                        request.IsReturn
                            ? "Warehouse return document was not found in Mikro write database."
                            : "Inter warehouse shipment document was not found in Mikro write database.");
                }

                EnsureSingleDocument(rows, request.IsReturn);
                EnsureDocumentCanBeUpdated(rows, request);
                ValidateEffectiveDocumentState(rows[0], request);
                await EnsureRequestedStocksExistAsync(request, cancellationToken);

                var movementGuids = rows.Select(row => row.sth_Guid).ToArray();
                var movementExtras = await mikroWriteDbContext.STOK_HAREKETLERI_EKs
                    .Where(extra =>
                        extra.sthek_iptal != true &&
                        extra.sthek_related_uid.HasValue &&
                        movementGuids.Contains(extra.sthek_related_uid.Value) &&
                        extra.sth_subesip_uid.HasValue &&
                        extra.sth_subesip_uid.Value != Guid.Empty)
                    .ToArrayAsync(cancellationToken);
                var linkedOrderGuids = movementExtras
                    .Select(extra => extra.sth_subesip_uid!.Value)
                    .Distinct()
                    .ToArray();
                var linkedOrders = linkedOrderGuids.Length == 0
                    ? new Dictionary<Guid, DEPOLAR_ARASI_SIPARISLER>()
                    : await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
                        .Where(order => linkedOrderGuids.Contains(order.ssip_Guid))
                        .ToDictionaryAsync(order => order.ssip_Guid, cancellationToken);

                EnsureLinkedOrdersExist(linkedOrderGuids, linkedOrders);

                var targetWarehouseNo = request.TargetWarehouseNo ?? rows[0].sth_nakliyedeposu ?? 0;
                var transitWarehouseNo = request.TransitWarehouseNo ?? rows[0].sth_giris_depo_no ?? 0;
                ValidateEffectiveWarehouses(request.SourceWarehouseNo, targetWarehouseNo, transitWarehouseNo);
                EnsureLinkedOrdersMatchDocument(request.IsReturn, request.SourceWarehouseNo, targetWarehouseNo, linkedOrders.Values);

                var orderGuidsByMovementGuid = movementExtras
                    .GroupBy(extra => extra.sthek_related_uid!.Value)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(extra => extra.sth_subesip_uid!.Value)
                            .Distinct()
                            .ToArray());
                var rowsByGuid = rows.ToDictionary(row => row.sth_Guid);
                var updateUser = ResolveMikroUserNo(request.SourceWarehouseNo);
                var updatedAt = DateTime.Now;
                var touchedRows = new HashSet<Guid>();
                var addedRows = new List<STOK_HAREKETLERI>();
                var deletedRows = new HashSet<Guid>();
                var quantityChanges = new Dictionary<Guid, LineQuantityChange>();
                var usedRowNos = rows
                    .Where(row => row.sth_satirno.HasValue)
                    .Select(row => row.sth_satirno!.Value)
                    .ToHashSet();
                var nextRowNo = usedRowNos.Count == 0 ? 0 : usedRowNos.Max() + 1;

                if (HasHeaderPatch(request))
                {
                    foreach (var row in rows)
                    {
                        ApplyHeaderPatch(row, request);
                        touchedRows.Add(row.sth_Guid);
                    }
                }

                foreach (var line in request.Lines)
                {
                    var action = NormalizeLineAction(line.Action);
                    if (action == LineActionAdd)
                    {
                        var addedRowNo = ResolveAddedRowNo(line, usedRowNos, ref nextRowNo);
                        var addedRow = CreateAddedMovement(rows[0], request, line, addedRowNo, updatedAt, updateUser);
                        if (HasHeaderPatch(request))
                        {
                            ApplyHeaderPatch(addedRow, request);
                        }

                        mikroWriteDbContext.STOK_HAREKETLERIs.Add(addedRow);
                        addedRows.Add(addedRow);
                        rowsByGuid.Add(addedRow.sth_Guid, addedRow);
                        touchedRows.Add(addedRow.sth_Guid);
                        continue;
                    }

                    var movementGuid = line.MovementGuid!.Value;
                    if (!rowsByGuid.TryGetValue(movementGuid, out var row))
                    {
                        throw new KeyNotFoundException($"Warehouse shipping line was not found: {movementGuid}");
                    }

                    if (action == LineActionDelete)
                    {
                        var deletedOldQuantity = row.sth_miktar ?? 0d;
                        mikroWriteDbContext.STOK_HAREKETLERIs.Remove(row);
                        mikroWriteDbContext.STOK_HAREKETLERI_EKs.RemoveRange(
                            movementExtras.Where(extra => extra.sthek_related_uid == row.sth_Guid));
                        if (request.IsReturn &&
                            orderGuidsByMovementGuid.TryGetValue(row.sth_Guid, out var deletedReturnOrderGuids))
                        {
                            foreach (var orderGuid in deletedReturnOrderGuids)
                            {
                                mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.Remove(linkedOrders[orderGuid]);
                            }
                        }

                        touchedRows.Add(row.sth_Guid);
                        deletedRows.Add(row.sth_Guid);
                        quantityChanges[row.sth_Guid] = new LineQuantityChange(row.sth_Guid, deletedOldQuantity, 0d);
                        continue;
                    }

                    if (!HasLinePatch(line))
                    {
                        continue;
                    }

                    var oldQuantity = row.sth_miktar ?? 0d;
                    if (ApplyLinePatch(row, line))
                    {
                        touchedRows.Add(row.sth_Guid);
                        quantityChanges[row.sth_Guid] = new LineQuantityChange(
                            row.sth_Guid,
                            oldQuantity,
                            row.sth_miktar ?? 0d);
                    }
                }

                if (touchedRows.Count == 0)
                {
                    throw new ArgumentException("At least one warehouse shipping field must be provided.", nameof(request));
                }

                ApplyLinkedOrderUpdates(
                    request.IsReturn,
                    request.SourceWarehouseNo,
                    targetWarehouseNo,
                    orderGuidsByMovementGuid,
                    linkedOrders,
                    rowsByGuid,
                    touchedRows,
                    deletedRows,
                    quantityChanges,
                    updatedAt,
                    updateUser);

                foreach (var row in rows.Where(row => touchedRows.Contains(row.sth_Guid)))
                {
                    row.sth_lastup_user = updateUser;
                    row.sth_lastup_date = updatedAt;
                    row.sth_degisti = true;
                }

                var activeRows = rows
                    .Concat(addedRows)
                    .Where(row => row.sth_iptal != true && !deletedRows.Contains(row.sth_Guid))
                    .ToArray();

                if (activeRows.Length == 0)
                {
                    throw new ArgumentException("Warehouse shipping document must have at least one active line.", nameof(request.Lines));
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new UpdateWarehouseShippingDocumentResponse(
                    rows[0].sth_evrakno_seri ?? documentSerie,
                    rows[0].sth_evrakno_sira ?? request.DocumentOrderNo,
                    request.SourceWarehouseNo,
                    rows[0].sth_nakliyedeposu ?? targetWarehouseNo,
                    rows[0].sth_giris_depo_no ?? transitWarehouseNo,
                    request.IsReturn,
                    touchedRows.Count,
                    addedRows.Count,
                    deletedRows.Count,
                    activeRows.Length,
                    activeRows.Sum(row => row.sth_miktar ?? 0d),
                    activeRows.Sum(row => row.sth_tutar ?? 0d),
                    updatedAt,
                    updateUser,
                    mikroWriteOptions.Value.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    private static NotSupportedException CreateUnsupportedMikroApiModeException() =>
        new(
            "MikroWriteRouting:WarehouseShippingUpdate MikroApi/DualShadow is not wired yet. " +
            "This flow must use DahiliStokHareketDuzeltV2/DahiliStokHareketGuidSilV2 " +
            "and DepolarArasiSiparisDuzeltV2 with live payload tests before DB writes can be disabled.");

    private async Task EnsureRequestedStocksExistAsync(
        UpdateWarehouseShippingDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var requestedStockCodes = request.Lines
            .Where(line => line.StockCode is not null)
            .Select(line => NormalizeRequiredText(line.StockCode!, 25, nameof(line.StockCode)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedStockCodes.Length == 0)
        {
            return;
        }

        var existingStockCodes = await mikroWriteDbContext.STOKLARs
            .Where(stock => requestedStockCodes.Contains(stock.sto_kod))
            .Select(stock => stock.sto_kod!)
            .ToArrayAsync(cancellationToken);
        var existingSet = existingStockCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingStockCode = requestedStockCodes.FirstOrDefault(stockCode => !existingSet.Contains(stockCode));

        if (!string.IsNullOrWhiteSpace(missingStockCode))
        {
            throw new KeyNotFoundException($"Stock was not found: {missingStockCode}");
        }
    }

    private static void ApplyHeaderPatch(
        STOK_HAREKETLERI row,
        UpdateWarehouseShippingDocumentRequest request)
    {
        if (request.MovementDate.HasValue)
        {
            row.sth_tarih = request.MovementDate.Value.Date;
            row.sth_malkbl_sevk_tarihi = request.MovementDate.Value.Date;
            row.sth_teslim_tarihi = request.MovementDate.Value.Date;
        }

        if (request.DocumentDate.HasValue)
        {
            row.sth_belge_tarih = request.DocumentDate.Value.Date;
        }

        if (request.DocumentNo is not null)
        {
            row.sth_belge_no = NormalizeText(request.DocumentNo, 50, nameof(request.DocumentNo));
        }

        if (request.TargetWarehouseNo.HasValue)
        {
            row.sth_nakliyedeposu = request.TargetWarehouseNo.Value;
        }

        if (request.TransitWarehouseNo.HasValue)
        {
            row.sth_giris_depo_no = request.TransitWarehouseNo.Value;
        }

        if (request.Description is not null)
        {
            row.sth_aciklama = NormalizeText(request.Description, 50, nameof(request.Description));
        }
    }

    private static bool ApplyLinePatch(
        STOK_HAREKETLERI row,
        UpdateWarehouseShippingDocumentLineRequest line)
    {
        var changed = false;

        SetIfPresent(line.RowNo, value => row.sth_satirno = ValidateNonNegative(value, nameof(line.RowNo)), ref changed);
        SetIfPresent(line.StockCode, value => row.sth_stok_kod = NormalizeRequiredText(value, 25, nameof(line.StockCode)), ref changed);
        SetIfPresent(line.UnitPointer, value => row.sth_birim_pntr = ValidateUnitPointer(value, nameof(line.UnitPointer)), ref changed);

        var currentQuantity = row.sth_miktar ?? 0d;
        var currentAmount = row.sth_tutar ?? 0d;
        var currentUnitPrice = currentQuantity == 0d ? 0d : currentAmount / currentQuantity;
        var effectiveQuantity = line.Quantity ?? currentQuantity;
        var effectiveUnitPrice = line.UnitPrice ?? currentUnitPrice;

        if (line.Quantity.HasValue)
        {
            row.sth_miktar = ValidatePositive(line.Quantity.Value, nameof(line.Quantity));
            changed = true;
        }

        if (line.Amount.HasValue)
        {
            row.sth_tutar = ValidateNonNegative(line.Amount.Value, nameof(line.Amount));
            changed = true;
        }
        else if (line.Quantity.HasValue || line.UnitPrice.HasValue)
        {
            row.sth_tutar = effectiveQuantity * ValidateNonNegative(effectiveUnitPrice, nameof(line.UnitPrice));
            changed = true;
        }

        SetIfPresent(line.Description, value => row.sth_aciklama = NormalizeText(value, 50, nameof(line.Description)), ref changed);
        SetIfPresent(line.PartyCode, value => row.sth_parti_kodu = NormalizeText(value, 25, nameof(line.PartyCode)), ref changed);
        SetIfPresent(line.LotNo, value => row.sth_lot_no = ValidateNonNegative(value, nameof(line.LotNo)), ref changed);
        SetIfPresent(line.ProjectCode, value => row.sth_proje_kodu = NormalizeText(value, 25, nameof(line.ProjectCode)), ref changed);
        SetIfPresent(line.CustomerResponsibilityCenter, value => row.sth_cari_srm_merkezi = NormalizeText(value, 25, nameof(line.CustomerResponsibilityCenter)), ref changed);
        SetIfPresent(line.ProductResponsibilityCenter, value => row.sth_stok_srm_merkezi = NormalizeText(value, 25, nameof(line.ProductResponsibilityCenter)), ref changed);

        return changed;
    }

    private static STOK_HAREKETLERI CreateAddedMovement(
        STOK_HAREKETLERI template,
        UpdateWarehouseShippingDocumentRequest request,
        UpdateWarehouseShippingDocumentLineRequest line,
        int rowNo,
        DateTime now,
        short updateUser)
    {
        var movementDate = request.MovementDate?.Date ?? template.sth_tarih?.Date ?? now.Date;
        var documentDate = request.DocumentDate?.Date ?? template.sth_belge_tarih?.Date ?? movementDate;
        var quantity = ValidatePositive(line.Quantity!.Value, nameof(line.Quantity));
        var unitPrice = line.UnitPrice ??
            (line.Amount.HasValue ? line.Amount.Value / quantity : 0d);
        var amount = line.Amount ?? quantity * ValidateNonNegative(unitPrice, nameof(line.UnitPrice));

        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = MovementFileId,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = true,
            sth_checksum = 0,
            sth_create_user = updateUser,
            sth_create_date = now,
            sth_lastup_user = updateUser,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = template.sth_firmano ?? 0,
            sth_subeno = template.sth_subeno ?? 0,
            sth_tarih = movementDate,
            sth_tip = MovementType,
            sth_cins = MovementGenre,
            sth_normal_iade = request.IsReturn ? ReturnMovement : NormalMovement,
            sth_evraktip = InterWarehouseShipmentDocumentType,
            sth_evrakno_seri = template.sth_evrakno_seri,
            sth_evrakno_sira = template.sth_evrakno_sira,
            sth_satirno = rowNo,
            sth_belge_no = NormalizeText(request.DocumentNo ?? template.sth_belge_no, 50, nameof(request.DocumentNo)),
            sth_belge_tarih = documentDate,
            sth_stok_kod = NormalizeRequiredText(line.StockCode!, 25, nameof(line.StockCode)),
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
            sth_cari_kodu = string.Empty,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = ValidateUnitPointer(line.UnitPointer ?? 1, nameof(line.UnitPointer)),
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
            sth_vergi_pntr = 0,
            sth_vergi = 0d,
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = NormalizeText(line.Description ?? request.Description ?? template.sth_aciklama, 50, nameof(line.Description)),
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = request.TransitWarehouseNo ?? template.sth_giris_depo_no,
            sth_cikis_depo_no = request.SourceWarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = NormalizeText(line.CustomerResponsibilityCenter, 25, nameof(line.CustomerResponsibilityCenter)),
            sth_stok_srm_merkezi = NormalizeText(line.ProductResponsibilityCenter, 25, nameof(line.ProductResponsibilityCenter)),
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = NormalizeText(line.PartyCode, 25, nameof(line.PartyCode)),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = NormalizeText(line.ProjectCode, 25, nameof(line.ProjectCode)),
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
            sth_fiyat_liste_no = -1,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = request.TargetWarehouseNo ?? template.sth_nakliyedeposu,
            sth_nakliyedurumu = template.sth_nakliyedurumu ?? WaitingShippingState,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = string.Empty,
            sth_HareketGrupKodu2 = string.Empty,
            sth_HareketGrupKodu3 = string.Empty,
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
            sth_eticaret_kanal_kodu = template.sth_eticaret_kanal_kodu ?? string.Empty,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static void ApplyLinkedOrderUpdates(
        bool isReturn,
        int sourceWarehouseNo,
        int targetWarehouseNo,
        IReadOnlyDictionary<Guid, Guid[]> orderGuidsByMovementGuid,
        IReadOnlyDictionary<Guid, DEPOLAR_ARASI_SIPARISLER> linkedOrders,
        IReadOnlyDictionary<Guid, STOK_HAREKETLERI> rowsByGuid,
        IReadOnlySet<Guid> touchedRows,
        IReadOnlySet<Guid> deletedRows,
        IReadOnlyDictionary<Guid, LineQuantityChange> quantityChanges,
        DateTime updatedAt,
        short updateUser)
    {
        foreach (var movementGuid in touchedRows)
        {
            if (!orderGuidsByMovementGuid.TryGetValue(movementGuid, out var orderGuids) ||
                !rowsByGuid.TryGetValue(movementGuid, out var movement))
            {
                continue;
            }

            foreach (var orderGuid in orderGuids)
            {
                var order = linkedOrders[orderGuid];

                if (deletedRows.Contains(movementGuid))
                {
                    if (!isReturn &&
                        quantityChanges.TryGetValue(movementGuid, out var deletedQuantityChange) &&
                        Math.Abs(deletedQuantityChange.Delta) > QuantityTolerance)
                    {
                        ApplyDeliveredQuantityDelta(order, deletedQuantityChange.Delta);
                        TouchWarehouseOrder(order, updatedAt, updateUser);
                    }

                    continue;
                }

                if (isReturn)
                {
                    MirrorReturnMovementToWarehouseOrder(order, movement, sourceWarehouseNo, targetWarehouseNo);
                    TouchWarehouseOrder(order, updatedAt, updateUser);
                    continue;
                }

                EnsureShipmentMatchesLinkedOrder(order, movement, sourceWarehouseNo, targetWarehouseNo);
                if (!quantityChanges.TryGetValue(movementGuid, out var quantityChange) ||
                    Math.Abs(quantityChange.Delta) <= QuantityTolerance)
                {
                    continue;
                }

                ApplyDeliveredQuantityDelta(order, quantityChange.Delta);
                TouchWarehouseOrder(order, updatedAt, updateUser);
            }
        }
    }

    private static void MirrorReturnMovementToWarehouseOrder(
        DEPOLAR_ARASI_SIPARISLER order,
        STOK_HAREKETLERI movement,
        int sourceWarehouseNo,
        int targetWarehouseNo)
    {
        var quantity = movement.sth_miktar ?? 0d;
        var amount = movement.sth_tutar ?? 0d;

        order.ssip_tarih = movement.sth_tarih;
        order.ssip_teslim_tarih = movement.sth_tarih;
        order.ssip_belge_tarih = movement.sth_belge_tarih;
        order.ssip_stok_kod = movement.sth_stok_kod;
        order.ssip_miktar = quantity;
        order.ssip_b_fiyat = quantity == 0d ? 0d : amount / quantity;
        order.ssip_tutar = amount;
        order.ssip_aciklama = movement.sth_aciklama;
        order.ssip_girdepo = targetWarehouseNo;
        order.ssip_cikdepo = sourceWarehouseNo;
        order.ssip_birim_pntr = movement.sth_birim_pntr;
        order.ssip_projekodu = movement.sth_proje_kodu;
        order.ssip_sormerkezi = movement.sth_stok_srm_merkezi;
    }

    private static void ApplyDeliveredQuantityDelta(DEPOLAR_ARASI_SIPARISLER order, double delta)
    {
        var currentDelivered = order.ssip_teslim_miktar ?? 0d;
        var totalQuantity = order.ssip_miktar ?? 0d;
        var newDelivered = currentDelivered + delta;

        if (newDelivered < -QuantityTolerance)
        {
            throw new InvalidOperationException("Linked warehouse order delivered quantity can not be negative.");
        }

        if (totalQuantity > 0d && newDelivered - totalQuantity > QuantityTolerance)
        {
            throw new InvalidOperationException(
                "Shipment quantity can not be greater than linked warehouse order remaining quantity.");
        }

        order.ssip_teslim_miktar = Math.Max(0d, totalQuantity > 0d ? Math.Min(newDelivered, totalQuantity) : newDelivered);
        order.ssip_kapat_fl = totalQuantity > 0d && order.ssip_teslim_miktar >= totalQuantity - QuantityTolerance;
    }

    private static void TouchWarehouseOrder(DEPOLAR_ARASI_SIPARISLER order, DateTime updatedAt, short updateUser)
    {
        order.ssip_lastup_user = updateUser;
        order.ssip_lastup_date = updatedAt;
        order.ssip_degisti = true;
    }

    private static void EnsureShipmentMatchesLinkedOrder(
        DEPOLAR_ARASI_SIPARISLER order,
        STOK_HAREKETLERI movement,
        int sourceWarehouseNo,
        int targetWarehouseNo)
    {
        if (order.ssip_cikdepo != sourceWarehouseNo || order.ssip_girdepo != targetWarehouseNo)
        {
            throw new InvalidOperationException(
                "Linked warehouse order line does not match the selected source and target warehouses.");
        }

        if (!string.Equals(
                order.ssip_stok_kod?.Trim(),
                movement.sth_stok_kod?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Shipment stock code must match the linked warehouse order line stock code.");
        }
    }

    private static void EnsureDocumentCanBeUpdated(
        IReadOnlyCollection<STOK_HAREKETLERI> rows,
        UpdateWarehouseShippingDocumentRequest request)
    {
        if (rows.Any(row => row.sth_nakliyedurumu == DeliveredToTargetWarehouseState))
        {
            throw new InvalidOperationException(
                request.IsReturn
                    ? "Warehouse return document is already accepted and can not be updated."
                    : "Inter warehouse shipment document is already accepted and can not be updated.");
        }

        var sentRow = rows.FirstOrDefault(EDespatchMovementState.HasSentEDespatch);
        if (sentRow is not null)
        {
            throw new InvalidOperationException(
                $"E-despatch has already been sent with document number {sentRow.sth_belge_no}.");
        }

        if (rows.Any(row => row.sth_kilitli == true))
        {
            throw new InvalidOperationException("Warehouse shipping document is locked and can not be updated.");
        }
    }

    private static void EnsureSingleDocument(IReadOnlyCollection<STOK_HAREKETLERI> rows, bool isReturn)
    {
        var headerCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no,
                row.sth_giris_depo_no,
                row.sth_nakliyedeposu,
                row.sth_nakliyedurumu,
                row.sth_normal_iade
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                isReturn
                    ? "More than one warehouse return matched the requested serie and order number for the selected warehouse."
                    : "More than one inter warehouse shipment matched the requested serie and order number for the selected warehouse.");
        }
    }

    private static void EnsureLinkedOrdersExist(
        IReadOnlyCollection<Guid> linkedOrderGuids,
        IReadOnlyDictionary<Guid, DEPOLAR_ARASI_SIPARISLER> linkedOrders)
    {
        var missingOrderGuid = linkedOrderGuids.FirstOrDefault(guid => !linkedOrders.ContainsKey(guid));
        if (missingOrderGuid != Guid.Empty)
        {
            throw new KeyNotFoundException($"Linked warehouse order line was not found: {missingOrderGuid}");
        }
    }

    private static void EnsureLinkedOrdersMatchDocument(
        bool isReturn,
        int sourceWarehouseNo,
        int targetWarehouseNo,
        IEnumerable<DEPOLAR_ARASI_SIPARISLER> linkedOrders)
    {
        if (isReturn)
        {
            return;
        }

        foreach (var order in linkedOrders)
        {
            if (order.ssip_cikdepo != sourceWarehouseNo || order.ssip_girdepo != targetWarehouseNo)
            {
                throw new InvalidOperationException(
                    "Linked warehouse order line does not match the selected source and target warehouses.");
            }
        }
    }

    private static void ValidateEffectiveDocumentState(
        STOK_HAREKETLERI firstRow,
        UpdateWarehouseShippingDocumentRequest request)
    {
        var movementDate = request.MovementDate?.Date ?? firstRow.sth_tarih?.Date;
        var documentDate = request.DocumentDate?.Date ?? firstRow.sth_belge_tarih?.Date;

        if (movementDate.HasValue && documentDate.HasValue && documentDate.Value < movementDate.Value)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }
    }

    private static void ValidateEffectiveWarehouses(
        int sourceWarehouseNo,
        int targetWarehouseNo,
        int transitWarehouseNo)
    {
        if (targetWarehouseNo <= 0)
        {
            throw new ArgumentException("Target warehouse no must be greater than zero.", nameof(targetWarehouseNo));
        }

        if (transitWarehouseNo <= 0)
        {
            throw new ArgumentException("Transit warehouse no must be greater than zero.", nameof(transitWarehouseNo));
        }

        if (sourceWarehouseNo == targetWarehouseNo)
        {
            throw new ArgumentException("Source warehouse and target warehouse can not be the same.");
        }
    }

    private static void Validate(UpdateWarehouseShippingDocumentRequest request)
    {
        if (request.SourceWarehouseNo <= 0)
        {
            throw new ArgumentException("Source warehouse no must be greater than zero.", nameof(request.SourceWarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        _ = NormalizeRequiredText(request.DocumentSerie, 20, nameof(request.DocumentSerie));

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.TargetWarehouseNo is <= 0)
        {
            throw new ArgumentException("Target warehouse no must be greater than zero.", nameof(request.TargetWarehouseNo));
        }

        if (request.TransitWarehouseNo is <= 0)
        {
            throw new ArgumentException("Transit warehouse no must be greater than zero.", nameof(request.TransitWarehouseNo));
        }

        if (request.TargetWarehouseNo == request.SourceWarehouseNo)
        {
            throw new ArgumentException("Source warehouse and target warehouse can not be the same.");
        }

        if (request.DocumentDate.HasValue &&
            request.MovementDate.HasValue &&
            request.DocumentDate.Value.Date < request.MovementDate.Value.Date)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }

        if (request.Lines is null)
        {
            throw new ArgumentException("Lines can not be null.", nameof(request.Lines));
        }

        if (!HasHeaderPatch(request) && request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one header field or line field must be provided.", nameof(request));
        }

        foreach (var line in request.Lines)
        {
            Validate(line);
        }
    }

    private static void Validate(UpdateWarehouseShippingDocumentLineRequest line)
    {
        var action = NormalizeLineAction(line.Action);

        if (action == LineActionAdd)
        {
            if (line.MovementGuid is { } movementGuid && movementGuid != Guid.Empty)
            {
                throw new ArgumentException("Movement guid must be empty for add line action.", nameof(line.MovementGuid));
            }

            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required for add line action.", nameof(line.StockCode));
            }

            if (!line.Quantity.HasValue)
            {
                throw new ArgumentException("Quantity is required for add line action.", nameof(line.Quantity));
            }
        }
        else if (!line.MovementGuid.HasValue || line.MovementGuid.Value == Guid.Empty)
        {
            throw new ArgumentException("Movement guid is required.", nameof(line.MovementGuid));
        }

        if (line.RowNo is < 0)
        {
            throw new ArgumentException("Line row no can not be negative.", nameof(line.RowNo));
        }

        if (line.StockCode is not null)
        {
            _ = NormalizeRequiredText(line.StockCode, 25, nameof(line.StockCode));
        }

        if (line.Quantity is <= 0d)
        {
            throw new ArgumentException("Line quantity must be greater than zero.", nameof(line.Quantity));
        }

        if (line.UnitPrice is < 0d)
        {
            throw new ArgumentException("Line unit price can not be negative.", nameof(line.UnitPrice));
        }

        if (line.Amount is < 0d)
        {
            throw new ArgumentException("Line amount can not be negative.", nameof(line.Amount));
        }

        if (line.UnitPointer is < 1 or > 4)
        {
            throw new ArgumentException("Line unit pointer must be between 1 and 4.", nameof(line.UnitPointer));
        }

        if (line.LotNo is < 0)
        {
            throw new ArgumentException("Line lot no can not be negative.", nameof(line.LotNo));
        }
    }

    private static int ResolveAddedRowNo(
        UpdateWarehouseShippingDocumentLineRequest line,
        ISet<int> usedRowNos,
        ref int nextRowNo)
    {
        if (line.RowNo.HasValue)
        {
            var requestedRowNo = ValidateNonNegative(line.RowNo.Value, nameof(line.RowNo));
            if (!usedRowNos.Add(requestedRowNo))
            {
                throw new ArgumentException("Line row no already exists in the document.", nameof(line.RowNo));
            }

            if (requestedRowNo >= nextRowNo)
            {
                nextRowNo = requestedRowNo + 1;
            }

            return requestedRowNo;
        }

        while (!usedRowNos.Add(nextRowNo))
        {
            nextRowNo++;
        }

        return nextRowNo++;
    }

    private static bool HasHeaderPatch(UpdateWarehouseShippingDocumentRequest request) =>
        request.MovementDate.HasValue ||
        request.DocumentDate.HasValue ||
        request.DocumentNo is not null ||
        request.TargetWarehouseNo.HasValue ||
        request.TransitWarehouseNo.HasValue ||
        request.Description is not null;

    private static bool HasLinePatch(UpdateWarehouseShippingDocumentLineRequest line) =>
        line.RowNo.HasValue ||
        line.StockCode is not null ||
        line.Quantity.HasValue ||
        line.UnitPrice.HasValue ||
        line.Amount.HasValue ||
        line.UnitPointer.HasValue ||
        line.Description is not null ||
        line.PartyCode is not null ||
        line.LotNo.HasValue ||
        line.ProjectCode is not null ||
        line.CustomerResponsibilityCenter is not null ||
        line.ProductResponsibilityCenter is not null;

    private static string NormalizeLineAction(string? action)
    {
        var normalized = NormalizeText(action, 20, nameof(UpdateWarehouseShippingDocumentLineRequest.Action))
            .ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            return LineActionUpdate;
        }

        return normalized switch
        {
            LineActionUpdate => LineActionUpdate,
            LineActionAdd or "create" => LineActionAdd,
            LineActionDelete or "remove" => LineActionDelete,
            _ => throw new ArgumentException("Line action must be update, add or delete.", nameof(action))
        };
    }

    private static string NormalizeRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return NormalizeText(value, maxLength, parameterName);
    }

    private static string NormalizeText(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not be longer than {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static int ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentException("Value can not be negative.", parameterName);
        }

        return value;
    }

    private static double ValidateNonNegative(double value, string parameterName)
    {
        if (value < 0d)
        {
            throw new ArgumentException("Value can not be negative.", parameterName);
        }

        return value;
    }

    private static double ValidatePositive(double value, string parameterName)
    {
        if (value <= 0d)
        {
            throw new ArgumentException("Value must be greater than zero.", parameterName);
        }

        return value;
    }

    private static byte ValidateUnitPointer(int value, string parameterName)
    {
        if (value is < 1 or > 4)
        {
            throw new ArgumentException("Unit pointer must be between 1 and 4.", parameterName);
        }

        return Convert.ToByte(value);
    }

    private static short ResolveMikroUserNo(int warehouseNo) =>
        warehouseNo is > 0 and <= short.MaxValue
            ? Convert.ToInt16(warehouseNo)
            : FallbackMikroUserNo;

    private static void SetIfPresent<T>(T? value, Action<T> setter, ref bool changed)
        where T : struct
    {
        if (!value.HasValue)
        {
            return;
        }

        setter(value.Value);
        changed = true;
    }

    private static void SetIfPresent(string? value, Action<string> setter, ref bool changed)
    {
        if (value is null)
        {
            return;
        }

        setter(value);
        changed = true;
    }

    private sealed record LineQuantityChange(Guid MovementGuid, double OldQuantity, double NewQuantity)
    {
        public double Delta => NewQuantity - OldQuantity;
    }
}
