using System.Reflection;
using System.Globalization;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed partial class MikroDocumentEditingService
{
    private const string StockMovementUpdatePath = "/Api/apiMethods/DahiliStokHareketDuzeltV2";
    private const string StockMovementGuidDeletePath = "/Api/apiMethods/DahiliStokHareketGuidSilV2";
    private const string CompanyOrderUpdatePath = "/Api/apiMethods/SiparisDuzeltV2";
    private const string CompanyOrderGuidDeletePath = "/Api/apiMethods/SiparisGuidSilV2";
    private const string WarehouseOrderUpdatePath = "/Api/apiMethods/DepolarArasiSiparisDuzeltV2";
    private const string WarehouseOrderGuidDeletePath = "/Api/apiMethods/DepolarArasiSiparisGuidSilV2";
    private const string InventoryCountUpdatePath = "/Api/apiMethods/SayimSonuclariDuzeltV2";
    private const string BulkRecordPath = "/Api/apiMethods/KayitKaydetTopluV2";
    private const string CustomerCardUpdatePath = "/API/APIMethods/CariGuncelleV2";
    private const string StockTableNo = "13";
    private const string WarehouseTableNo = "111";
    private const string StockWarehouseSettingsTableNo = "10";
    private const string StockSalesPriceTableNo = "228";
    private const string CustomerMovementTableNo = "51";

    private bool UseMikroApiForDocumentEditing() =>
        mikroWriteRoutingOptions.CurrentValue.MicroDocumentEditing == MikroWriteMode.MikroApi;

    private async Task<StockCardUpdateResponse> UpdateStockCardMikroApiAsync(
        UpdateStockCardRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        var patch = request.Patch ?? throw new ArgumentException("Patch is required.", nameof(request.Patch));
        var stock = await mikroWriteDbContext.STOKLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
            ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");
        var original = SnapshotValues(stock);
        if (!ApplyStockCardPatch(stock, patch))
        {
            throw new ArgumentException("At least one stock card field must be provided.", nameof(request.Patch));
        }

        await PostBulkRecordAsync(
            BuildPartialUpdateRecord(stock, original, StockTableNo, nameof(stock.sto_Guid), nameof(stock.sto_lastup_date)),
            cancellationToken);

        var updatedAt = DateTime.Now;
        var detail = await ReadWithRetryAsync(() => GetStockCardAsync(stockCode, cancellationToken), cancellationToken);
        return new(new("stok-kartlari", 1, updatedAt, ResolveMikroUserNo(request.CurrentUserWarehouseNo)), detail);
    }

    private async Task<StockCardWarehouseUpdateResponse> UpdateStockCardWarehouseSettingsMikroApiAsync(
        UpdateStockCardWarehouseSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        var warehouseNo = request.WarehouseNo > 0
            ? request.WarehouseNo
            : throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        var patch = request.Patch ?? throw new ArgumentException("Patch is required.", nameof(request.Patch));
        if (!HasStockCardWarehousePatch(patch))
        {
            throw new ArgumentException("At least one warehouse stock field or resetToGlobal must be provided.", nameof(request.Patch));
        }

        _ = await mikroWriteDbContext.STOKLARs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
            ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");
        _ = await mikroWriteDbContext.DEPOLARs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.dep_no == warehouseNo && item.dep_iptal != true && item.dep_hidden != true, cancellationToken)
            ?? throw new KeyNotFoundException($"Warehouse was not found: {warehouseNo}");

        var detail = await mikroWriteDbContext.STOK_DEPO_DETAYLARIs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.sdp_depo_kod == stockCode && item.sdp_depo_no == warehouseNo, cancellationToken);
        var updatedAt = DateTime.Now;
        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);

        if (detail is null && patch.ResetToGlobal && !HasStockCardWarehouseValuePatch(patch))
        {
            return new(
                new($"stok-kartlari/{stockCode}/depolar/{warehouseNo}", 0, updatedAt, updateUser),
                await ReadStockWarehouseSettingAsync(stockCode, warehouseNo, cancellationToken));
        }

        if (detail is null)
        {
            detail = CreateStockCardWarehouseDetail(stockCode, warehouseNo, updateUser, updatedAt);
            ApplyStockCardWarehousePatch(detail, patch);
            await PostBulkRecordAsync(BuildInsertRecord(detail, StockWarehouseSettingsTableNo), cancellationToken);
        }
        else if (patch.ResetToGlobal && !HasStockCardWarehouseValuePatch(patch))
        {
            await PostBulkRecordAsync(
                BuildDeleteRecord(StockWarehouseSettingsTableNo, nameof(detail.sdp_Guid), detail.sdp_Guid, nameof(detail.sdp_lastup_date), detail.sdp_lastup_date),
                cancellationToken);
        }
        else
        {
            var original = SnapshotValues(detail);
            ApplyStockCardWarehousePatch(detail, patch);
            await PostBulkRecordAsync(
                BuildPartialUpdateRecord(detail, original, StockWarehouseSettingsTableNo, nameof(detail.sdp_Guid), nameof(detail.sdp_lastup_date)),
                cancellationToken);
        }

        return new(
            new($"stok-kartlari/{stockCode}/depolar/{warehouseNo}", 1, updatedAt, updateUser),
            await ReadWithRetryAsync(() => ReadStockWarehouseSettingAsync(stockCode, warehouseNo, cancellationToken), cancellationToken));
    }

    private async Task<MikroDocumentDeleteResponse> DeleteStockCardWarehouseSettingsMikroApiAsync(
        DeleteStockCardWarehouseSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        var warehouseNo = request.WarehouseNo > 0
            ? request.WarehouseNo
            : throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        var detail = await mikroWriteDbContext.STOK_DEPO_DETAYLARIs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.sdp_depo_kod == stockCode && item.sdp_depo_no == warehouseNo, cancellationToken);
        if (detail is not null)
        {
            await PostBulkRecordAsync(
                BuildDeleteRecord(StockWarehouseSettingsTableNo, nameof(detail.sdp_Guid), detail.sdp_Guid, nameof(detail.sdp_lastup_date), detail.sdp_lastup_date),
                cancellationToken);
        }

        return new(
            $"stok-kartlari/{stockCode}/depolar/{warehouseNo}",
            detail is null ? 0 : 1,
            DateTime.Now,
            ResolveMikroUserNo(request.CurrentUserWarehouseNo),
            "api-delete-override");
    }

    private async Task<CustomerCardUpdateResponse> UpdateCustomerCardMikroApiAsync(
        UpdateCustomerCardRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var customerCode = NormalizeRequiredText(request.CustomerCode, 25, nameof(request.CustomerCode));
        var patch = request.Patch ?? throw new ArgumentException("Patch is required.", nameof(request.Patch));
        if (!HasCustomerCardPatch(patch))
        {
            throw new ArgumentException("At least one customer card field must be provided.", nameof(request.Patch));
        }

        await EnsureCustomerCardReferencesExistAsync(patch, cancellationToken);
        var customer = await mikroWriteDbContext.CARI_HESAPLARs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.cari_kod == customerCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer was not found in Mikro write database: {customerCode}");
        var original = SnapshotValues(customer);
        if (!ApplyCustomerCardPatch(customer, patch))
        {
            throw new ArgumentException("At least one customer card field must be provided.", nameof(request.Patch));
        }

        var record = BuildPartialUpdateRecord(customer, original, "1", nameof(customer.cari_Guid), nameof(customer.cari_lastup_date));
        record.Remove("TabloNo");
        record.Remove("KayitTipi");
        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            CustomerCardUpdatePath,
            new { cariler = new[] { record } },
            cancellationToken);
        if (result.IsError)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Mikro API customer card update failed.");
        }

        var updatedAt = DateTime.Now;
        var detail = await ReadWithRetryAsync(() => GetCustomerCardAsync(customerCode, cancellationToken), cancellationToken);
        return new(new($"cariler/{customerCode}", 1, updatedAt, ResolveMikroUserNo(request.CurrentUserWarehouseNo)), detail);
    }

    private async Task<WarehouseCardUpdateResponse> UpdateWarehouseCardMikroApiAsync(
        UpdateWarehouseCardRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var warehouseNo = request.WarehouseNo > 0
            ? request.WarehouseNo
            : throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        var patch = request.Patch ?? throw new ArgumentException("Patch is required.", nameof(request.Patch));
        if (!HasWarehouseCardPatch(patch))
        {
            throw new ArgumentException("At least one warehouse card field must be provided.", nameof(request.Patch));
        }

        var warehouse = await mikroWriteDbContext.DEPOLARs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.dep_no == warehouseNo, cancellationToken)
            ?? throw new KeyNotFoundException($"Warehouse was not found in Mikro write database: {warehouseNo}");
        var original = SnapshotValues(warehouse);
        if (!ApplyWarehouseCardPatch(warehouse, patch))
        {
            throw new ArgumentException("At least one warehouse card field must be provided.", nameof(request.Patch));
        }

        await PostBulkRecordAsync(
            BuildPartialUpdateRecord(warehouse, original, WarehouseTableNo, nameof(warehouse.dep_Guid), nameof(warehouse.dep_lastup_date)),
            cancellationToken);
        var updatedAt = DateTime.Now;
        var detail = await ReadWithRetryAsync(() => GetWarehouseCardAsync(warehouseNo, cancellationToken), cancellationToken);
        return new(new($"depolar/{warehouseNo}", 1, updatedAt, ResolveMikroUserNo(request.CurrentUserWarehouseNo)), detail);
    }

    private async Task<StockSalesPriceUpsertResponse> UpsertStockSalesPriceMikroApiAsync(
        UpsertStockSalesPriceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        ValidateStockSalesPriceRequest(request);
        var stock = await mikroWriteDbContext.STOKLARs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
            ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");
        _ = await mikroWriteDbContext.DEPOLARs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.dep_no == request.WarehouseNo && item.dep_iptal != true && item.dep_hidden != true, cancellationToken)
            ?? throw new KeyNotFoundException($"Warehouse was not found: {request.WarehouseNo}");
        _ = await mikroWriteDbContext.STOK_SATIS_FIYAT_LISTE_TANIMLARIs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.sfl_sirano == request.PriceListNo && item.sfl_iptal != true && item.sfl_hidden != true, cancellationToken)
            ?? throw new KeyNotFoundException($"Active sales price list was not found: {request.PriceListNo}");

        var row = await FindStockSalesPriceAsync(stockCode, request.WarehouseNo, request.PriceListNo, request.PaymentPlanNo, request.UnitPointer, cancellationToken);
        var created = row is null;
        var previousPrice = row?.sfiyat_fiyati;
        var updatedAt = DateTime.Now;
        if (row is null)
        {
            row = CreateStockSalesPrice(request, stockCode, ResolveMikroUserNo(request.CurrentUserWarehouseNo), updatedAt);
            await PostBulkRecordAsync(BuildInsertRecord(row, StockSalesPriceTableNo), cancellationToken);
        }
        else
        {
            var original = SnapshotValues(row);
            row.sfiyat_iptal = false;
            row.sfiyat_hidden = false;
            row.sfiyat_kilitli = false;
            row.sfiyat_fiyati = request.Price;
            row.sfiyat_doviz = request.CurrencyType;
            row.sfiyat_deg_nedeni = request.ChangeReason;
            await PostBulkRecordAsync(
                BuildPartialUpdateRecord(row, original, StockSalesPriceTableNo, nameof(row.sfiyat_Guid), nameof(row.sfiyat_lastup_date)),
                cancellationToken);
        }

        var saved = await ReadWithRetryAsync(
            async () => await FindStockSalesPriceAsync(stockCode, request.WarehouseNo, request.PriceListNo, request.PaymentPlanNo, request.UnitPointer, cancellationToken)
                        ?? throw new KeyNotFoundException("Stock sales price was not visible after Mikro API write."),
            cancellationToken);
        var dto = (await GetStockSalesPricesAsync(stockCode, request.WarehouseNo, cancellationToken))
            .Single(item => item.PriceGuid == saved.sfiyat_Guid);
        return new(new($"stok-kartlari/{stockCode}/satis-fiyatlari/{request.WarehouseNo}", 1, updatedAt, ResolveMikroUserNo(request.CurrentUserWarehouseNo)), created, previousPrice, dto);
    }

    private async Task<MikroDocumentDeleteResponse> DeleteStockSalesPriceMikroApiAsync(
        DeleteStockSalesPriceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        ValidateStockSalesPriceKey(request.WarehouseNo, request.PriceListNo, request.PaymentPlanNo, request.UnitPointer);
        var row = await FindStockSalesPriceAsync(stockCode, request.WarehouseNo, request.PriceListNo, request.PaymentPlanNo, request.UnitPointer, cancellationToken)
                  ?? throw new KeyNotFoundException("Active stock sales price was not found in Mikro write database.");
        await PostBulkRecordAsync(
            BuildDeleteRecord(StockSalesPriceTableNo, nameof(row.sfiyat_Guid), row.sfiyat_Guid, nameof(row.sfiyat_lastup_date), row.sfiyat_lastup_date),
            cancellationToken);
        return new(
            $"stok-kartlari/{stockCode}/satis-fiyatlari/{request.WarehouseNo}",
            1,
            DateTime.Now,
            ResolveMikroUserNo(request.CurrentUserWarehouseNo),
            "api-delete");
    }

    private async Task<StockMovementDocumentUpdateResponse> UpdateStockMovementDocumentMikroApiAsync(
        UpdateStockMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        ValidateStockMovementLookup(request.Lookup);
        ValidateStockMovementUpdate(request);
        await EnsureStockMovementReferencesExistAsync(request, cancellationToken);

        var rows = await CreateStockMovementQuery(
                mikroWriteDbContext.STOK_HAREKETLERIs.AsNoTracking(), request.Lookup)
            .OrderBy(row => row.sth_satirno)
            .ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Stock movement document was not found in Mikro write database.");
        EnsureSingleStockMovementDocument(rows);
        var originalRows = rows.ToDictionary(row => row.sth_Guid, SnapshotValues);

        var touched = new HashSet<Guid>();
        if (request.Header is not null && HasStockMovementHeaderPatch(request.Header))
        {
            foreach (var row in rows) { ApplyStockMovementHeaderPatch(row, request.Header); touched.Add(row.sth_Guid); }
        }
        var byGuid = rows.ToDictionary(row => row.sth_Guid);
        foreach (var line in request.Lines)
        {
            if (!byGuid.TryGetValue(line.MovementGuid, out var row)) throw new KeyNotFoundException($"Stock movement line was not found: {line.MovementGuid}");
            if (ApplyStockMovementLinePatch(row, line)) touched.Add(row.sth_Guid);
        }
        EnsureTouched(touched.Count, request);
        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        foreach (var row in rows.Where(row => touched.Contains(row.sth_Guid))) { row.sth_lastup_user = updateUser; row.sth_degisti = true; }

        var updateRows = rows
            .Where(row => touched.Contains(row.sth_Guid))
            .Select(row => BuildStockMovementUpdateApiRow(row, originalRows[row.sth_Guid]))
            .ToArray();
        await PostStockMovementUpdateRowsAsync(updateRows, cancellationToken);
        var document = await ReadWithRetryAsync(() => GetStockMovementDocumentAsync(request.Lookup, cancellationToken), cancellationToken);
        return new(new("stok-hareketleri", touched.Count, updatedAt, updateUser), document);
    }

    private async Task<MikroDocumentDeleteResponse> DeleteStockMovementDocumentMikroApiAsync(
        DeleteStockMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        ValidateStockMovementLookup(request.Lookup);
        var rows = await CreateStockMovementQuery(mikroWriteDbContext.STOK_HAREKETLERIs.AsNoTracking(), request.Lookup).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Stock movement document was not found in Mikro write database.");
        EnsureSingleStockMovementDocument(rows);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var now = DateTime.Now;
        if (request.HardDelete)
        {
            await PostRowsAsync(StockMovementGuidDeletePath, rows.Select(row => new { row.sth_Guid }), cancellationToken);
        }
        else
        {
            foreach (var row in rows) { row.sth_iptal = true; row.sth_hidden = true; row.sth_degisti = true; row.sth_lastup_user = user; }
            await PostRowsAsync(StockMovementUpdatePath, rows, cancellationToken);
        }
        return new($"stok-hareketleri/{request.Lookup.DocumentSerie.Trim()}/{request.Lookup.DocumentOrderNo}", rows.Length, now, user, request.HardDelete ? "hard-delete" : "soft-delete");
    }

    private async Task<CompanyOrderDocumentUpdateResponse> UpdateCompanyOrderDocumentMikroApiAsync(
        UpdateCompanyOrderDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateCompanyOrderLookup(request.Lookup); ValidateCompanyOrderUpdate(request);
        await EnsureCompanyOrderReferencesExistAsync(request, cancellationToken);
        var rows = await CreateCompanyOrderQuery(mikroWriteDbContext.SIPARISLERs.AsNoTracking(), request.Lookup).OrderBy(row => row.sip_satirno).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Company order document was not found in Mikro write database.");
        EnsureSingleCompanyOrderDocument(rows);
        var touched = new HashSet<Guid>();
        if (request.Header is not null && HasCompanyOrderHeaderPatch(request.Header)) foreach (var row in rows) { ApplyCompanyOrderHeaderPatch(row, request.Header); touched.Add(row.sip_Guid); }
        var byGuid = rows.ToDictionary(row => row.sip_Guid);
        foreach (var line in request.Lines) { if (!byGuid.TryGetValue(line.OrderGuid, out var row)) throw new KeyNotFoundException($"Company order line was not found: {line.OrderGuid}"); if (ApplyCompanyOrderLinePatch(row, line)) touched.Add(row.sip_Guid); }
        EnsureTouched(touched.Count, request);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        foreach (var row in rows.Where(row => touched.Contains(row.sip_Guid))) { row.sip_lastup_user = user; row.sip_degisti = true; }
        await PostRowsAsync(CompanyOrderUpdatePath, rows.Where(row => touched.Contains(row.sip_Guid)), cancellationToken);
        var document = await ReadWithRetryAsync(() => GetCompanyOrderDocumentAsync(request.Lookup, cancellationToken), cancellationToken);
        return new(new("firma-siparisleri", touched.Count, now, user), document);
    }

    private async Task<MikroDocumentDeleteResponse> DeleteCompanyOrderDocumentMikroApiAsync(DeleteCompanyOrderDocumentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateCompanyOrderLookup(request.Lookup);
        var rows = await CreateCompanyOrderQuery(mikroWriteDbContext.SIPARISLERs.AsNoTracking(), request.Lookup).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Company order document was not found in Mikro write database.");
        EnsureSingleCompanyOrderDocument(rows);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        if (request.HardDelete) await PostRowsAsync(CompanyOrderGuidDeletePath, rows.Select(row => new { row.sip_Guid }), cancellationToken);
        else { foreach (var row in rows) { row.sip_iptal = true; row.sip_hidden = true; row.sip_degisti = true; row.sip_lastup_user = user; } await PostRowsAsync(CompanyOrderUpdatePath, rows, cancellationToken); }
        return new($"firma-siparisleri/{request.Lookup.DocumentSerie.Trim()}/{request.Lookup.DocumentOrderNo}", rows.Length, now, user, request.HardDelete ? "hard-delete" : "soft-delete");
    }

    private async Task<WarehouseOrderDocumentUpdateResponse> UpdateWarehouseOrderDocumentMikroApiAsync(UpdateWarehouseOrderDocumentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateWarehouseOrderLookup(request.Lookup); ValidateWarehouseOrderUpdate(request);
        await EnsureWarehouseOrderReferencesExistAsync(request, cancellationToken);
        var rows = await CreateWarehouseOrderQuery(mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking(), request.Lookup).OrderBy(row => row.ssip_satirno).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Warehouse order document was not found in Mikro write database.");
        EnsureSingleWarehouseOrderDocument(rows);
        var touched = new HashSet<Guid>();
        if (request.Header is not null && HasWarehouseOrderHeaderPatch(request.Header)) foreach (var row in rows) { ApplyWarehouseOrderHeaderPatch(row, request.Header); touched.Add(row.ssip_Guid); }
        var byGuid = rows.ToDictionary(row => row.ssip_Guid);
        foreach (var line in request.Lines) { if (!byGuid.TryGetValue(line.OrderGuid, out var row)) throw new KeyNotFoundException($"Warehouse order line was not found: {line.OrderGuid}"); if (ApplyWarehouseOrderLinePatch(row, line)) touched.Add(row.ssip_Guid); }
        EnsureTouched(touched.Count, request);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        foreach (var row in rows.Where(row => touched.Contains(row.ssip_Guid))) { row.ssip_lastup_user = user; row.ssip_degisti = true; }
        await PostRowsAsync(WarehouseOrderUpdatePath, rows.Where(row => touched.Contains(row.ssip_Guid)), cancellationToken);
        var document = await ReadWithRetryAsync(() => GetWarehouseOrderDocumentAsync(request.Lookup, cancellationToken), cancellationToken);
        return new(new("depo-siparisleri", touched.Count, now, user), document);
    }

    private async Task<MikroDocumentDeleteResponse> DeleteWarehouseOrderDocumentMikroApiAsync(DeleteWarehouseOrderDocumentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateWarehouseOrderLookup(request.Lookup);
        var rows = await CreateWarehouseOrderQuery(mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking(), request.Lookup).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Warehouse order document was not found in Mikro write database.");
        EnsureSingleWarehouseOrderDocument(rows);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        if (request.HardDelete) await PostRowsAsync(WarehouseOrderGuidDeletePath, rows.Select(row => new { row.ssip_Guid }), cancellationToken);
        else { foreach (var row in rows) { row.ssip_iptal = true; row.ssip_hidden = true; row.ssip_degisti = true; row.ssip_lastup_user = user; } await PostRowsAsync(WarehouseOrderUpdatePath, rows, cancellationToken); }
        return new($"depo-siparisleri/{request.Lookup.DocumentSerie.Trim()}/{request.Lookup.DocumentOrderNo}", rows.Length, now, user, request.HardDelete ? "hard-delete" : "soft-delete");
    }

    private async Task<CustomerMovementDocumentUpdateResponse> UpdateCustomerMovementDocumentMikroApiAsync(UpdateCustomerMovementDocumentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateCustomerMovementLookup(request.Lookup); ValidateCustomerMovementUpdate(request);
        await EnsureCustomerMovementReferencesExistAsync(request, cancellationToken);
        var rows = await CreateCustomerMovementQuery(mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AsNoTracking(), request.Lookup).OrderBy(row => row.cha_satir_no).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Customer movement document was not found in Mikro write database.");
        EnsureSingleCustomerMovementDocument(rows);
        var touched = new HashSet<Guid>();
        if (request.Header is not null && HasCustomerMovementHeaderPatch(request.Header)) foreach (var row in rows) { ApplyCustomerMovementHeaderPatch(row, request.Header); touched.Add(row.cha_Guid); }
        var byGuid = rows.ToDictionary(row => row.cha_Guid);
        foreach (var line in request.Lines) { if (!byGuid.TryGetValue(line.MovementGuid, out var row)) throw new KeyNotFoundException($"Customer movement line was not found: {line.MovementGuid}"); if (ApplyCustomerMovementLinePatch(row, line)) touched.Add(row.cha_Guid); }
        EnsureTouched(touched.Count, request);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        foreach (var row in rows.Where(row => touched.Contains(row.cha_Guid))) { row.cha_lastup_user = user; row.cha_degisti = true; }
        await PostBulkRecordsAsync(rows.Where(row => touched.Contains(row.cha_Guid)), "1", cancellationToken);
        var document = await ReadWithRetryAsync(() => GetCustomerMovementDocumentAsync(request.Lookup, cancellationToken), cancellationToken);
        return new(new("cari-hareketleri", touched.Count, now, user), document);
    }

    private async Task<MikroDocumentDeleteResponse> DeleteCustomerMovementDocumentMikroApiAsync(DeleteCustomerMovementDocumentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateCustomerMovementLookup(request.Lookup);
        var rows = await CreateCustomerMovementQuery(mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AsNoTracking(), request.Lookup).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Customer movement document was not found in Mikro write database.");
        EnsureSingleCustomerMovementDocument(rows);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        if (!request.HardDelete) foreach (var row in rows) { row.cha_iptal = true; row.cha_hidden = true; row.cha_degisti = true; row.cha_lastup_user = user; }
        await PostBulkRecordsAsync(rows, request.HardDelete ? "2" : "1", cancellationToken);
        return new($"cari-hareketleri/{request.Lookup.DocumentSerie.Trim()}/{request.Lookup.DocumentOrderNo}", rows.Length, now, user, request.HardDelete ? "hard-delete" : "soft-delete");
    }

    private async Task<InventoryCountDocumentUpdateResponse> UpdateInventoryCountDocumentMikroApiAsync(UpdateInventoryCountDocumentRequest request, CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo); ValidateInventoryCountLookup(request.Lookup); ValidateInventoryCountUpdate(request);
        await EnsureInventoryCountReferencesExistAsync(request, cancellationToken);
        var rows = await CreateInventoryCountQuery(mikroWriteDbContext.SAYIM_SONUCLARIs.AsNoTracking(), request.Lookup).OrderBy(row => row.sym_satirno).ToArrayAsync(cancellationToken);
        if (rows.Length == 0) throw new KeyNotFoundException("Inventory count document was not found in Mikro write database.");
        EnsureSingleInventoryCountDocument(rows); EnsureInventoryCountRowsAreEditable(rows);
        var touched = new HashSet<Guid>();
        if (request.Header is not null && HasInventoryCountHeaderPatch(request.Header)) foreach (var row in rows) { ApplyInventoryCountHeaderPatch(row, request.Header); touched.Add(row.sym_Guid); }
        var byGuid = rows.ToDictionary(row => row.sym_Guid);
        foreach (var line in request.Lines) { if (!byGuid.TryGetValue(line.CountGuid, out var row)) throw new KeyNotFoundException($"Inventory count line was not found: {line.CountGuid}"); if (ApplyInventoryCountLinePatch(row, line)) touched.Add(row.sym_Guid); }
        EnsureTouched(touched.Count, request);
        var user = ResolveMikroUserNo(request.CurrentUserWarehouseNo); var now = DateTime.Now;
        foreach (var row in rows.Where(row => touched.Contains(row.sym_Guid))) { row.sym_lastup_user = user; row.sym_degisti = true; }
        await PostRowsAsync(InventoryCountUpdatePath, rows.Where(row => touched.Contains(row.sym_Guid)), cancellationToken);
        var document = await ReadWithRetryAsync(() => GetInventoryCountDocumentAsync(request.Lookup, cancellationToken), cancellationToken);
        return new(new("sayim-sonuclari", touched.Count, now, user), document);
    }

    private async Task PostRowsAsync<T>(string path, IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        var payload = new { evraklar = new[] { new { satirlar = rows.Select(ToMikroApiRow).ToArray() } } };
        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(path, payload, cancellationToken);
        if (result.IsError) throw new InvalidOperationException(result.ErrorMessage ?? $"Mikro API request failed: {path}");
    }

    private async Task PostStockMovementUpdateRowsAsync(
        IReadOnlyCollection<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken)
    {
        var payload = new { evraklar = new[] { new { satirlar = rows } } };
        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            StockMovementUpdatePath,
            payload,
            cancellationToken);
        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API stock movement update failed.");
        }
    }

    private async Task<StockCardWarehouseSettingsDto> ReadStockWarehouseSettingAsync(
        string stockCode,
        int warehouseNo,
        CancellationToken cancellationToken) =>
        (await GetStockCardWarehouseSettingsAsync(stockCode, warehouseNo, cancellationToken)).Single();

    private Task<STOK_SATIS_FIYAT_LISTELERI?> FindStockSalesPriceAsync(
        string stockCode,
        int warehouseNo,
        int priceListNo,
        int paymentPlanNo,
        byte unitPointer,
        CancellationToken cancellationToken) =>
        mikroWriteDbContext.STOK_SATIS_FIYAT_LISTELERIs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.sfiyat_iptal != true &&
                    item.sfiyat_hidden != true &&
                    item.sfiyat_stokkod == stockCode &&
                    item.sfiyat_listesirano == priceListNo &&
                    item.sfiyat_deposirano == warehouseNo &&
                    item.sfiyat_birim_pntr == unitPointer &&
                    item.sfiyat_odemeplan == paymentPlanNo,
                cancellationToken);

    private async Task PostBulkRecordAsync(
        Dictionary<string, object?> record,
        CancellationToken cancellationToken)
    {
        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            BulkRecordPath,
            new { Kayit = new[] { record } },
            cancellationToken);
        if (result.IsError)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Mikro API record write failed.");
        }
    }

    private static Dictionary<string, object?> SnapshotValues(object row) =>
        row.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, property => property.GetValue(row), StringComparer.Ordinal);

    private static Dictionary<string, object?> BuildPartialUpdateRecord(
        object row,
        IReadOnlyDictionary<string, object?> original,
        string tableNo,
        string guidPropertyName,
        string lastUpdatePropertyName)
    {
        var properties = row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var byName = properties.ToDictionary(property => property.Name, StringComparer.Ordinal);
        var record = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TabloNo"] = tableNo,
            ["KayitTipi"] = "1",
            [guidPropertyName] = byName[guidPropertyName].GetValue(row)
        };

        if (original.TryGetValue(lastUpdatePropertyName, out var lastUpdate) && lastUpdate is not null)
        {
            record[lastUpdatePropertyName] = FormatConcurrencyDate(lastUpdate);
        }

        foreach (var property in properties)
        {
            if (property.Name == guidPropertyName ||
                property.Name == lastUpdatePropertyName ||
                IsMikroTechnicalColumn(property.Name))
            {
                continue;
            }

            var currentValue = property.GetValue(row);
            if (original.TryGetValue(property.Name, out var originalValue) && !Equals(currentValue, originalValue))
            {
                record[property.Name] = NormalizeMikroApiValue(currentValue);
            }
        }

        if (record.Count <= (record.ContainsKey(lastUpdatePropertyName) ? 4 : 3))
        {
            throw new ArgumentException("At least one changed Mikro field is required.", nameof(row));
        }

        return record;
    }

    internal static Dictionary<string, object?> BuildStockMovementUpdateApiRow(
        object row,
        IReadOnlyDictionary<string, object?> original)
    {
        const string guidPropertyName = "sth_Guid";
        const string lastUpdatePropertyName = "sth_lastup_date";
        var properties = row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var byName = properties.ToDictionary(property => property.Name, StringComparer.Ordinal);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [guidPropertyName] = byName[guidPropertyName].GetValue(row)
        };

        if (original.TryGetValue(lastUpdatePropertyName, out var lastUpdate) && lastUpdate is not null)
        {
            result[lastUpdatePropertyName] = FormatConcurrencyDate(lastUpdate);
        }

        foreach (var property in properties)
        {
            if (property.Name == guidPropertyName ||
                property.Name == lastUpdatePropertyName ||
                property.Name.EndsWith("_degisti", StringComparison.OrdinalIgnoreCase) ||
                IsMikroTechnicalColumn(property.Name))
            {
                continue;
            }

            var currentValue = property.GetValue(row);
            if (currentValue is not null &&
                original.TryGetValue(property.Name, out var originalValue) &&
                !Equals(currentValue, originalValue))
            {
                result[property.Name] = NormalizeMikroApiValue(currentValue);
            }
        }

        if (result.Count <= (result.ContainsKey(lastUpdatePropertyName) ? 2 : 1))
        {
            throw new ArgumentException("At least one changed stock movement field is required.", nameof(row));
        }

        return result;
    }

    internal static Dictionary<string, object?> BuildInsertRecord(object row, string tableNo)
    {
        var record = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TabloNo"] = tableNo,
            ["KayitTipi"] = "0"
        };
        foreach (var property in row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsMikroTechnicalColumn(property.Name) ||
                property.Name.EndsWith("_lastup_date", StringComparison.OrdinalIgnoreCase)) continue;
            var value = property.GetValue(row);
            if (value is null) continue;
            record[property.Name] = NormalizeMikroApiValue(value);
        }
        return record;
    }

    private static Dictionary<string, object?> BuildDeleteRecord(
        string tableNo,
        string guidPropertyName,
        Guid guid,
        string lastUpdatePropertyName,
        DateTime? lastUpdate)
    {
        var record = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TabloNo"] = tableNo,
            ["KayitTipi"] = "2",
            [guidPropertyName] = guid
        };
        if (lastUpdate.HasValue)
        {
            record[lastUpdatePropertyName] = FormatConcurrencyDate(lastUpdate.Value);
        }
        return record;
    }

    private async Task PostBulkRecordsAsync<T>(IEnumerable<T> rows, string recordType, CancellationToken cancellationToken)
    {
        var records = rows.Select(row => ToBulkRecord(row!, CustomerMovementTableNo, recordType)).ToArray();
        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(BulkRecordPath, new { Kayit = records }, cancellationToken);
        if (result.IsError) throw new InvalidOperationException(result.ErrorMessage ?? "Mikro API bulk record update failed.");
    }

    private static Dictionary<string, object?> ToBulkRecord(object row, string tableNo, string recordType)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TabloNo"] = tableNo,
            ["KayitTipi"] = recordType
        };
        foreach (var property in row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (recordType == "2" &&
                !property.Name.Equals("cha_Guid", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Equals("cha_lastup_date", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsMikroTechnicalColumn(property.Name)) continue;
            var value = property.GetValue(row);
            if (value is null) continue;
            result[property.Name] = property.Name.EndsWith("_lastup_date", StringComparison.OrdinalIgnoreCase)
                ? FormatConcurrencyDate(value)
                : NormalizeMikroApiValue(value);
        }
        return result;
    }

    private static Dictionary<string, object?> ToMikroApiRow<T>(T row)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in row!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsMikroTechnicalColumn(property.Name)) continue;
            var value = property.GetValue(row);
            if (value is null) continue;
            result[property.Name] = property.Name.EndsWith("_lastup_date", StringComparison.OrdinalIgnoreCase)
                ? FormatConcurrencyDate(value)
                : NormalizeMikroApiValue(value);
        }
        return result;
    }

    private static object? NormalizeMikroApiValue(object? value) => value switch
    {
        DateTime date => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        _ => value
    };

    private static string FormatConcurrencyDate(object value) =>
        Convert.ToDateTime(value, CultureInfo.InvariantCulture)
            .ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static bool IsMikroTechnicalColumn(string name) =>
        name.EndsWith("_DBCno", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_SpecRECno", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_fileid", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_checksum", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_create_user", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_create_date", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_lastup_user", StringComparison.OrdinalIgnoreCase);

    private static void EnsureTouched(int count, object request)
    {
        if (count == 0) throw new ArgumentException("At least one document field must be provided.", nameof(request));
    }

    private static async Task<T> ReadWithRetryAsync<T>(Func<Task<T>> read, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await read(); }
            catch (KeyNotFoundException) when (attempt < 3) { await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken); }
        }
    }
}
