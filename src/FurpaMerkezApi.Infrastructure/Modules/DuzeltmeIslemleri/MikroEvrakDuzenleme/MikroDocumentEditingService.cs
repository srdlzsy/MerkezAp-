using System.Data;
using FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed class MikroDocumentEditingService(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext)
    : IMikroDocumentEditingService
{
    private const short FallbackMikroUserNo = 39;
    private const short StockSalesPriceFileId = 228;
    private const int DefaultSearchTake = 50;
    private const int MaxSearchTake = 200;

    public async Task<IReadOnlyCollection<StockCardListItemDto>> SearchStockCardsAsync(
        StockCardSearchRequest request,
        CancellationToken cancellationToken)
    {
        var take = request.Take <= 0
            ? DefaultSearchTake
            : Math.Min(request.Take, MaxSearchTake);
        var searchText = request.SearchText?.Trim();

        var query = mikroDbContext.STOKLARs
            .AsNoTracking()
            .AsQueryable();

        if (!request.IncludePassive)
        {
            query = query.Where(stock => stock.sto_pasif_fl != true);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(stock =>
                stock.sto_kod.Contains(searchText) ||
                (stock.sto_isim != null && stock.sto_isim.Contains(searchText)) ||
                (stock.sto_kisa_ismi != null && stock.sto_kisa_ismi.Contains(searchText)));
        }

        return await query
            .OrderBy(stock => stock.sto_kod)
            .Take(take)
            .Select(stock => new StockCardListItemDto(
                stock.sto_kod,
                stock.sto_isim ?? string.Empty,
                stock.sto_kisa_ismi ?? string.Empty,
                stock.sto_sat_cari_kod ?? string.Empty,
                stock.sto_birim1_ad ?? string.Empty,
                stock.sto_anagrup_kod ?? string.Empty,
                stock.sto_altgrup_kod ?? string.Empty,
                stock.sto_kategori_kodu ?? string.Empty,
                stock.sto_pasif_fl ?? false,
                stock.sto_lastup_date))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<StockCardDetailDto> GetStockCardAsync(
        string stockCode,
        CancellationToken cancellationToken)
    {
        var normalizedStockCode = NormalizeRequiredText(stockCode, 25, nameof(stockCode));

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.sto_kod == normalizedStockCode, cancellationToken)
            ?? throw new KeyNotFoundException("Stock card was not found.");

        return MapStockCardDetail(stock);
    }

    public async Task<StockCardUpdateResponse> UpdateStockCardAsync(
        UpdateStockCardRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        var patch = request.Patch ?? throw new ArgumentException("Patch is required.", nameof(request.Patch));
        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var stock = await mikroWriteDbContext.STOKLARs
                    .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
                    ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");

                var changed = ApplyStockCardPatch(stock, patch);
                if (!changed)
                {
                    throw new ArgumentException("At least one stock card field must be provided.", nameof(request.Patch));
                }

                stock.sto_lastup_user = updateUser;
                stock.sto_lastup_date = updatedAt;
                stock.sto_degisti = true;

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new StockCardUpdateResponse(
                    new MikroDocumentUpdateSummary("stok-kartlari", 1, updatedAt, updateUser),
                    MapStockCardDetail(stock));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<IReadOnlyCollection<StockCardWarehouseSettingsDto>> GetStockCardWarehouseSettingsAsync(
        string stockCode,
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var normalizedStockCode = NormalizeRequiredText(stockCode, 25, nameof(stockCode));
        if (warehouseNo.HasValue && warehouseNo.Value <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.sto_kod == normalizedStockCode, cancellationToken)
            ?? throw new KeyNotFoundException("Stock card was not found.");

        var warehouseQuery = mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse =>
                warehouse.dep_no.HasValue &&
                warehouse.dep_no.Value > 0 &&
                warehouse.dep_iptal != true &&
                warehouse.dep_hidden != true);

        if (warehouseNo.HasValue)
        {
            warehouseQuery = warehouseQuery.Where(warehouse => warehouse.dep_no == warehouseNo.Value);
        }

        var warehouses = await warehouseQuery
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new
            {
                WarehouseNo = warehouse.dep_no.GetValueOrDefault(),
                WarehouseName = warehouse.dep_adi ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        if (warehouseNo.HasValue && warehouses.Length == 0)
        {
            throw new KeyNotFoundException($"Warehouse was not found: {warehouseNo.Value}");
        }

        var warehouseNos = warehouses
            .Select(warehouse => warehouse.WarehouseNo)
            .ToArray();
        var details = await mikroDbContext.STOK_DEPO_DETAYLARIs
            .AsNoTracking()
            .Where(detail =>
                detail.sdp_depo_kod == normalizedStockCode &&
                detail.sdp_depo_no.HasValue &&
                warehouseNos.Contains(detail.sdp_depo_no.Value))
            .ToArrayAsync(cancellationToken);
        var detailsByWarehouse = details
            .Where(detail => detail.sdp_depo_no.HasValue)
            .ToDictionary(detail => detail.sdp_depo_no!.Value);

        return warehouses
            .Select(warehouse =>
            {
                detailsByWarehouse.TryGetValue(warehouse.WarehouseNo, out var detail);
                return MapStockCardWarehouseSettings(
                    stock,
                    warehouse.WarehouseNo,
                    warehouse.WarehouseName,
                    detail);
            })
            .ToArray();
    }

    public async Task<StockCardWarehouseUpdateResponse> UpdateStockCardWarehouseSettingsAsync(
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
            throw new ArgumentException(
                "At least one warehouse stock field or resetToGlobal must be provided.",
                nameof(request.Patch));
        }

        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var stock = await mikroWriteDbContext.STOKLARs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
                    ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");
                var warehouse = await mikroWriteDbContext.DEPOLARs
                    .AsNoTracking()
                    .Where(item =>
                        item.dep_no == warehouseNo &&
                        item.dep_iptal != true &&
                        item.dep_hidden != true)
                    .Select(item => new
                    {
                        WarehouseNo = item.dep_no.GetValueOrDefault(),
                        WarehouseName = item.dep_adi ?? string.Empty
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Warehouse was not found: {warehouseNo}");

                var detail = await mikroWriteDbContext.STOK_DEPO_DETAYLARIs
                    .FirstOrDefaultAsync(
                        item => item.sdp_depo_kod == stockCode && item.sdp_depo_no == warehouseNo,
                        cancellationToken);

                if (detail is null &&
                    patch.ResetToGlobal &&
                    !HasStockCardWarehouseValuePatch(patch))
                {
                    await transaction.CommitAsync(cancellationToken);

                    return new StockCardWarehouseUpdateResponse(
                        new MikroDocumentUpdateSummary(
                            $"stok-kartlari/{stockCode}/depolar/{warehouseNo}",
                            0,
                            updatedAt,
                            updateUser),
                        MapStockCardWarehouseSettings(
                            stock,
                            warehouse.WarehouseNo,
                            warehouse.WarehouseName,
                            null));
                }

                if (detail is null)
                {
                    detail = CreateStockCardWarehouseDetail(
                        stockCode,
                        warehouseNo,
                        updateUser,
                        updatedAt);
                    mikroWriteDbContext.STOK_DEPO_DETAYLARIs.Add(detail);
                }

                ApplyStockCardWarehousePatch(detail, patch);
                detail.sdp_lastup_user = updateUser;
                detail.sdp_lastup_date = updatedAt;
                detail.sdp_degisti = true;

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new StockCardWarehouseUpdateResponse(
                    new MikroDocumentUpdateSummary(
                        $"stok-kartlari/{stockCode}/depolar/{warehouseNo}",
                        1,
                        updatedAt,
                        updateUser),
                    MapStockCardWarehouseSettings(
                        stock,
                        warehouse.WarehouseNo,
                        warehouse.WarehouseName,
                        detail));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<MikroDocumentDeleteResponse> DeleteStockCardWarehouseSettingsAsync(
        DeleteStockCardWarehouseSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        var warehouseNo = request.WarehouseNo > 0
            ? request.WarehouseNo
            : throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        var deleteUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var deletedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                _ = await mikroWriteDbContext.STOKLARs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
                    ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");
                _ = await mikroWriteDbContext.DEPOLARs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item =>
                            item.dep_no == warehouseNo &&
                            item.dep_iptal != true &&
                            item.dep_hidden != true,
                        cancellationToken)
                    ?? throw new KeyNotFoundException($"Warehouse was not found: {warehouseNo}");

                var detail = await mikroWriteDbContext.STOK_DEPO_DETAYLARIs
                    .FirstOrDefaultAsync(
                        item => item.sdp_depo_kod == stockCode && item.sdp_depo_no == warehouseNo,
                        cancellationToken);

                if (detail is not null)
                {
                    mikroWriteDbContext.STOK_DEPO_DETAYLARIs.Remove(detail);
                    await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                return new MikroDocumentDeleteResponse(
                    $"stok-kartlari/{stockCode}/depolar/{warehouseNo}",
                    detail is null ? 0 : 1,
                    deletedAt,
                    deleteUser,
                    "physical-delete-override");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<IReadOnlyCollection<WarehouseCardListItemDto>> SearchWarehouseCardsAsync(
        WarehouseCardSearchRequest request,
        CancellationToken cancellationToken)
    {
        var take = request.Take <= 0
            ? DefaultSearchTake
            : Math.Min(request.Take, MaxSearchTake);
        var searchText = request.SearchText?.Trim();
        var hasWarehouseNo = int.TryParse(searchText, out var warehouseNo);

        var query = mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no.HasValue && warehouse.dep_no.Value > 0);

        if (!request.IncludePassive)
        {
            query = query.Where(warehouse =>
                warehouse.dep_iptal != true &&
                warehouse.dep_hidden != true);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(warehouse =>
                (hasWarehouseNo && warehouse.dep_no == warehouseNo) ||
                (warehouse.dep_adi != null && warehouse.dep_adi.Contains(searchText)) ||
                (warehouse.dep_grup_kodu != null && warehouse.dep_grup_kodu.Contains(searchText)) ||
                (warehouse.dep_Il != null && warehouse.dep_Il.Contains(searchText)) ||
                (warehouse.dep_Ilce != null && warehouse.dep_Ilce.Contains(searchText)));
        }

        return await query
            .OrderBy(warehouse => warehouse.dep_no)
            .Take(take)
            .Select(warehouse => new WarehouseCardListItemDto(
                warehouse.dep_no.GetValueOrDefault(),
                warehouse.dep_adi ?? string.Empty,
                warehouse.dep_grup_kodu ?? string.Empty,
                warehouse.dep_tipi ?? 0,
                warehouse.dep_Il ?? string.Empty,
                warehouse.dep_Ilce ?? string.Empty,
                warehouse.dep_iptal ?? false,
                warehouse.dep_hidden ?? false,
                warehouse.dep_lastup_date))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<WarehouseCardDetailDto> GetWarehouseCardAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var warehouse = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.dep_no == warehouseNo, cancellationToken)
            ?? throw new KeyNotFoundException($"Warehouse was not found: {warehouseNo}");

        return MapWarehouseCardDetail(warehouse);
    }

    public async Task<WarehouseCardUpdateResponse> UpdateWarehouseCardAsync(
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

        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var warehouse = await mikroWriteDbContext.DEPOLARs
                    .FirstOrDefaultAsync(item => item.dep_no == warehouseNo, cancellationToken)
                    ?? throw new KeyNotFoundException($"Warehouse was not found in Mikro write database: {warehouseNo}");

                var changed = ApplyWarehouseCardPatch(warehouse, patch);
                if (!changed)
                {
                    throw new ArgumentException("At least one warehouse card field must be provided.", nameof(request.Patch));
                }

                warehouse.dep_lastup_user = updateUser;
                warehouse.dep_lastup_date = updatedAt;
                warehouse.dep_degisti = true;

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new WarehouseCardUpdateResponse(
                    new MikroDocumentUpdateSummary($"depolar/{warehouseNo}", 1, updatedAt, updateUser),
                    MapWarehouseCardDetail(warehouse));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<IReadOnlyCollection<CustomerCardListItemDto>> SearchCustomerCardsAsync(
        CustomerCardSearchRequest request,
        CancellationToken cancellationToken)
    {
        var take = request.Take <= 0
            ? DefaultSearchTake
            : Math.Min(request.Take, MaxSearchTake);
        var searchText = request.SearchText?.Trim();

        var query = mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer => customer.cari_kod != null);

        if (!request.IncludePassive)
        {
            query = query.Where(customer =>
                customer.cari_iptal != true &&
                customer.cari_hidden != true);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(customer =>
                customer.cari_kod!.Contains(searchText) ||
                (customer.cari_unvan1 != null && customer.cari_unvan1.Contains(searchText)) ||
                (customer.cari_unvan2 != null && customer.cari_unvan2.Contains(searchText)) ||
                (customer.cari_VergiKimlikNo != null && customer.cari_VergiKimlikNo.Contains(searchText)));
        }

        return await query
            .OrderBy(customer => customer.cari_kod)
            .Take(take)
            .Select(customer => new CustomerCardListItemDto(
                customer.cari_kod ?? string.Empty,
                customer.cari_unvan1 ?? string.Empty,
                customer.cari_unvan2 ?? string.Empty,
                customer.cari_vdaire_adi ?? string.Empty,
                customer.cari_VergiKimlikNo ?? string.Empty,
                customer.cari_grup_kodu ?? string.Empty,
                customer.cari_bolge_kodu ?? string.Empty,
                customer.cari_temsilci_kodu ?? string.Empty,
                customer.cari_firma_acik_kapal ?? false,
                customer.cari_cari_kilitli_flg ?? false,
                customer.cari_lastup_date))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CustomerCardDetailDto> GetCustomerCardAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerCode = NormalizeRequiredText(customerCode, 25, nameof(customerCode));

        var customer = await mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.cari_kod == normalizedCustomerCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer was not found: {normalizedCustomerCode}");

        return MapCustomerCardDetail(customer);
    }

    public async Task<CustomerCardUpdateResponse> UpdateCustomerCardAsync(
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

        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var customer = await mikroWriteDbContext.CARI_HESAPLARs
                    .FirstOrDefaultAsync(item => item.cari_kod == customerCode, cancellationToken)
                    ?? throw new KeyNotFoundException(
                        $"Customer was not found in Mikro write database: {customerCode}");

                await EnsureCustomerCardReferencesExistAsync(patch, cancellationToken);

                var changed = ApplyCustomerCardPatch(customer, patch);
                if (!changed)
                {
                    throw new ArgumentException("At least one customer card field must be provided.", nameof(request.Patch));
                }

                customer.cari_lastup_user = updateUser;
                customer.cari_lastup_date = updatedAt;
                customer.cari_degisti = true;

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CustomerCardUpdateResponse(
                    new MikroDocumentUpdateSummary($"cariler/{customerCode}", 1, updatedAt, updateUser),
                    MapCustomerCardDetail(customer));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<IReadOnlyCollection<StockSalesPriceDto>> GetStockSalesPricesAsync(
        string stockCode,
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var normalizedStockCode = NormalizeRequiredText(stockCode, 25, nameof(stockCode));
        if (warehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.sto_kod == normalizedStockCode, cancellationToken)
            ?? throw new KeyNotFoundException("Stock card was not found.");

        var query = mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs
            .AsNoTracking()
            .Where(item =>
                item.sfiyat_stokkod == normalizedStockCode &&
                item.sfiyat_iptal != true &&
                item.sfiyat_hidden != true);

        if (warehouseNo.HasValue)
        {
            query = query.Where(item => item.sfiyat_deposirano == warehouseNo.Value);
        }

        var rows = await query
            .OrderBy(item => item.sfiyat_deposirano)
            .ThenBy(item => item.sfiyat_listesirano)
            .ThenBy(item => item.sfiyat_birim_pntr)
            .ThenBy(item => item.sfiyat_odemeplan)
            .ToArrayAsync(cancellationToken);

        var warehouseNos = rows
            .Select(item => item.sfiyat_deposirano.GetValueOrDefault())
            .Where(item => item > 0)
            .Distinct()
            .ToArray();
        var priceListNos = rows
            .Select(item => item.sfiyat_listesirano.GetValueOrDefault())
            .Where(item => item > 0)
            .Distinct()
            .ToArray();

        var warehouseNames = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item => item.dep_no.HasValue && warehouseNos.Contains(item.dep_no.Value))
            .ToDictionaryAsync(
                item => item.dep_no!.Value,
                item => item.dep_adi ?? string.Empty,
                cancellationToken);
        var priceListNames = await mikroDbContext.STOK_SATIS_FIYAT_LISTE_TANIMLARIs
            .AsNoTracking()
            .Where(item => item.sfl_sirano.HasValue && priceListNos.Contains(item.sfl_sirano.Value))
            .ToDictionaryAsync(
                item => item.sfl_sirano!.Value,
                item => item.sfl_aciklama ?? string.Empty,
                cancellationToken);

        return rows
            .Select(item => MapStockSalesPrice(item, stock, warehouseNames, priceListNames))
            .ToArray();
    }

    public async Task<StockSalesPriceUpsertResponse> UpsertStockSalesPriceAsync(
        UpsertStockSalesPriceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        ValidateStockSalesPriceRequest(request);

        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var stock = await mikroWriteDbContext.STOKLARs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.sto_kod == stockCode, cancellationToken)
                    ?? throw new KeyNotFoundException("Stock card was not found in Mikro write database.");
                var warehouse = await mikroWriteDbContext.DEPOLARs
                    .AsNoTracking()
                    .Where(item =>
                        item.dep_no == request.WarehouseNo &&
                        item.dep_iptal != true &&
                        item.dep_hidden != true)
                    .Select(item => new
                    {
                        WarehouseNo = item.dep_no.GetValueOrDefault(),
                        WarehouseName = item.dep_adi ?? string.Empty
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Warehouse was not found: {request.WarehouseNo}");
                var priceList = await mikroWriteDbContext.STOK_SATIS_FIYAT_LISTE_TANIMLARIs
                    .AsNoTracking()
                    .Where(item =>
                        item.sfl_sirano == request.PriceListNo &&
                        item.sfl_iptal != true &&
                        item.sfl_hidden != true)
                    .Select(item => new
                    {
                        PriceListNo = item.sfl_sirano.GetValueOrDefault(),
                        PriceListName = item.sfl_aciklama ?? string.Empty
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new KeyNotFoundException($"Active sales price list was not found: {request.PriceListNo}");

                var row = await mikroWriteDbContext.STOK_SATIS_FIYAT_LISTELERIs
                    .FirstOrDefaultAsync(
                        item =>
                            item.sfiyat_stokkod == stockCode &&
                            item.sfiyat_listesirano == request.PriceListNo &&
                            item.sfiyat_deposirano == request.WarehouseNo &&
                            item.sfiyat_birim_pntr == request.UnitPointer &&
                            item.sfiyat_odemeplan == request.PaymentPlanNo,
                        cancellationToken);

                var created = row is null;
                var previousPrice = row?.sfiyat_fiyati;
                if (row is null)
                {
                    row = CreateStockSalesPrice(request, stockCode, updateUser, updatedAt);
                    mikroWriteDbContext.STOK_SATIS_FIYAT_LISTELERIs.Add(row);
                }
                else
                {
                    row.sfiyat_iptal = false;
                    row.sfiyat_hidden = false;
                    row.sfiyat_kilitli = false;
                    row.sfiyat_degisti = true;
                    row.sfiyat_lastup_user = updateUser;
                    row.sfiyat_lastup_date = updatedAt;
                    row.sfiyat_fiyati = request.Price;
                    row.sfiyat_doviz = request.CurrencyType;
                    row.sfiyat_deg_nedeni = request.ChangeReason;
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var warehouseNames = new Dictionary<int, string>
                {
                    [warehouse.WarehouseNo] = warehouse.WarehouseName
                };
                var priceListNames = new Dictionary<int, string>
                {
                    [priceList.PriceListNo] = priceList.PriceListName
                };

                return new StockSalesPriceUpsertResponse(
                    new MikroDocumentUpdateSummary(
                        $"stok-kartlari/{stockCode}/satis-fiyatlari/{request.WarehouseNo}",
                        1,
                        updatedAt,
                        updateUser),
                    created,
                    previousPrice,
                    MapStockSalesPrice(row, stock, warehouseNames, priceListNames));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<MikroDocumentDeleteResponse> DeleteStockSalesPriceAsync(
        DeleteStockSalesPriceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        var stockCode = NormalizeRequiredText(request.StockCode, 25, nameof(request.StockCode));
        ValidateStockSalesPriceKey(request.WarehouseNo, request.PriceListNo, request.PaymentPlanNo, request.UnitPointer);

        var deleteUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var deletedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var row = await mikroWriteDbContext.STOK_SATIS_FIYAT_LISTELERIs
                    .FirstOrDefaultAsync(
                        item =>
                            item.sfiyat_iptal != true &&
                            item.sfiyat_stokkod == stockCode &&
                            item.sfiyat_listesirano == request.PriceListNo &&
                            item.sfiyat_deposirano == request.WarehouseNo &&
                            item.sfiyat_birim_pntr == request.UnitPointer &&
                            item.sfiyat_odemeplan == request.PaymentPlanNo,
                        cancellationToken)
                    ?? throw new KeyNotFoundException("Active stock sales price was not found in Mikro write database.");

                row.sfiyat_iptal = true;
                row.sfiyat_hidden = true;
                row.sfiyat_kilitli = true;
                row.sfiyat_degisti = true;
                row.sfiyat_lastup_user = deleteUser;
                row.sfiyat_lastup_date = deletedAt;

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new MikroDocumentDeleteResponse(
                    $"stok-kartlari/{stockCode}/satis-fiyatlari/{request.WarehouseNo}",
                    1,
                    deletedAt,
                    deleteUser,
                    "soft-delete");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<StockMovementDocumentDto> GetStockMovementDocumentAsync(
        StockMovementDocumentLookupRequest request,
        CancellationToken cancellationToken)
    {
        ValidateStockMovementLookup(request);

        var rows = await CreateStockMovementQuery(
                mikroDbContext.STOK_HAREKETLERIs.AsNoTracking(),
                request)
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToArrayAsync(cancellationToken);

        if (rows.Length == 0)
        {
            throw new KeyNotFoundException("Stock movement document was not found.");
        }

        EnsureSingleStockMovementDocument(rows);

        return await MapStockMovementDocumentAsync(mikroDbContext, rows, cancellationToken);
    }

    public async Task<StockMovementDocumentUpdateResponse> UpdateStockMovementDocumentAsync(
        UpdateStockMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        ValidateStockMovementLookup(request.Lookup);
        ValidateStockMovementUpdate(request);

        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var rows = await CreateStockMovementQuery(mikroWriteDbContext.STOK_HAREKETLERIs, request.Lookup)
                    .OrderBy(movement => movement.sth_satirno)
                    .ThenBy(movement => movement.sth_stok_kod)
                    .ToArrayAsync(cancellationToken);

                if (rows.Length == 0)
                {
                    throw new KeyNotFoundException("Stock movement document was not found in Mikro write database.");
                }

                EnsureSingleStockMovementDocument(rows);
                await EnsureStockMovementReferencesExistAsync(request, cancellationToken);

                var touchedRows = new HashSet<Guid>();
                if (request.Header is not null && HasStockMovementHeaderPatch(request.Header))
                {
                    foreach (var row in rows)
                    {
                        ApplyStockMovementHeaderPatch(row, request.Header);
                        touchedRows.Add(row.sth_Guid);
                    }
                }

                if (request.Lines.Count > 0)
                {
                    var rowsByGuid = rows.ToDictionary(row => row.sth_Guid);
                    foreach (var line in request.Lines)
                    {
                        if (!rowsByGuid.TryGetValue(line.MovementGuid, out var row))
                        {
                            throw new KeyNotFoundException($"Stock movement line was not found: {line.MovementGuid}");
                        }

                        if (ApplyStockMovementLinePatch(row, line))
                        {
                            touchedRows.Add(row.sth_Guid);
                        }
                    }
                }

                if (touchedRows.Count == 0)
                {
                    throw new ArgumentException("At least one stock movement field must be provided.", nameof(request));
                }

                foreach (var row in rows.Where(row => touchedRows.Contains(row.sth_Guid)))
                {
                    row.sth_lastup_user = updateUser;
                    row.sth_lastup_date = updatedAt;
                    row.sth_degisti = true;
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new StockMovementDocumentUpdateResponse(
                    new MikroDocumentUpdateSummary("stok-hareketleri", touchedRows.Count, updatedAt, updateUser),
                    await MapStockMovementDocumentAsync(mikroWriteDbContext, rows, cancellationToken));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<MikroDocumentDeleteResponse> DeleteStockMovementDocumentAsync(
        DeleteStockMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        ValidateStockMovementLookup(request.Lookup);

        var deleteUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var deletedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var rows = await CreateStockMovementQuery(mikroWriteDbContext.STOK_HAREKETLERIs, request.Lookup)
                    .OrderBy(movement => movement.sth_satirno)
                    .ThenBy(movement => movement.sth_stok_kod)
                    .ToArrayAsync(cancellationToken);

                if (rows.Length == 0)
                {
                    throw new KeyNotFoundException("Stock movement document was not found in Mikro write database.");
                }

                EnsureSingleStockMovementDocument(rows);

                foreach (var row in rows)
                {
                    row.sth_iptal = true;
                    row.sth_hidden = true;
                    row.sth_degisti = true;
                    row.sth_lastup_user = deleteUser;
                    row.sth_lastup_date = deletedAt;
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new MikroDocumentDeleteResponse(
                    "stok-hareketleri",
                    rows.Length,
                    deletedAt,
                    deleteUser,
                    "soft-delete");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<CustomerMovementDocumentDto> GetCustomerMovementDocumentAsync(
        CustomerMovementDocumentLookupRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCustomerMovementLookup(request);

        var rows = await CreateCustomerMovementQuery(
                mikroDbContext.CARI_HESAP_HAREKETLERIs.AsNoTracking(),
                request)
            .OrderBy(movement => movement.cha_satir_no)
            .ThenBy(movement => movement.cha_kod)
            .ToArrayAsync(cancellationToken);

        if (rows.Length == 0)
        {
            throw new KeyNotFoundException("Customer movement document was not found.");
        }

        EnsureSingleCustomerMovementDocument(rows);

        return await MapCustomerMovementDocumentAsync(mikroDbContext, rows, cancellationToken);
    }

    public async Task<CustomerMovementDocumentUpdateResponse> UpdateCustomerMovementDocumentAsync(
        UpdateCustomerMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        ValidateCustomerMovementLookup(request.Lookup);
        ValidateCustomerMovementUpdate(request);

        var updateUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var updatedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var rows = await CreateCustomerMovementQuery(mikroWriteDbContext.CARI_HESAP_HAREKETLERIs, request.Lookup)
                    .OrderBy(movement => movement.cha_satir_no)
                    .ThenBy(movement => movement.cha_kod)
                    .ToArrayAsync(cancellationToken);

                if (rows.Length == 0)
                {
                    throw new KeyNotFoundException("Customer movement document was not found in Mikro write database.");
                }

                EnsureSingleCustomerMovementDocument(rows);
                await EnsureCustomerMovementReferencesExistAsync(request, cancellationToken);

                var touchedRows = new HashSet<Guid>();
                if (request.Header is not null && HasCustomerMovementHeaderPatch(request.Header))
                {
                    foreach (var row in rows)
                    {
                        ApplyCustomerMovementHeaderPatch(row, request.Header);
                        touchedRows.Add(row.cha_Guid);
                    }
                }

                if (request.Lines.Count > 0)
                {
                    var rowsByGuid = rows.ToDictionary(row => row.cha_Guid);
                    foreach (var line in request.Lines)
                    {
                        if (!rowsByGuid.TryGetValue(line.MovementGuid, out var row))
                        {
                            throw new KeyNotFoundException($"Customer movement line was not found: {line.MovementGuid}");
                        }

                        if (ApplyCustomerMovementLinePatch(row, line))
                        {
                            touchedRows.Add(row.cha_Guid);
                        }
                    }
                }

                if (touchedRows.Count == 0)
                {
                    throw new ArgumentException("At least one customer movement field must be provided.", nameof(request));
                }

                foreach (var row in rows.Where(row => touchedRows.Contains(row.cha_Guid)))
                {
                    row.cha_lastup_user = updateUser;
                    row.cha_lastup_date = updatedAt;
                    row.cha_degisti = true;
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CustomerMovementDocumentUpdateResponse(
                    new MikroDocumentUpdateSummary("cari-hareketleri", touchedRows.Count, updatedAt, updateUser),
                    await MapCustomerMovementDocumentAsync(mikroWriteDbContext, rows, cancellationToken));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<MikroDocumentDeleteResponse> DeleteCustomerMovementDocumentAsync(
        DeleteCustomerMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUpdateUser(request.CurrentUserWarehouseNo);
        ValidateCustomerMovementLookup(request.Lookup);

        var deleteUser = ResolveMikroUserNo(request.CurrentUserWarehouseNo);
        var deletedAt = DateTime.Now;
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var rows = await CreateCustomerMovementQuery(mikroWriteDbContext.CARI_HESAP_HAREKETLERIs, request.Lookup)
                    .OrderBy(movement => movement.cha_satir_no)
                    .ThenBy(movement => movement.cha_kod)
                    .ToArrayAsync(cancellationToken);

                if (rows.Length == 0)
                {
                    throw new KeyNotFoundException("Customer movement document was not found in Mikro write database.");
                }

                EnsureSingleCustomerMovementDocument(rows);

                foreach (var row in rows)
                {
                    row.cha_iptal = true;
                    row.cha_hidden = true;
                    row.cha_degisti = true;
                    row.cha_lastup_user = deleteUser;
                    row.cha_lastup_date = deletedAt;
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new MikroDocumentDeleteResponse(
                    "cari-hareketleri",
                    rows.Length,
                    deletedAt,
                    deleteUser,
                    "soft-delete");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private IQueryable<STOK_HAREKETLERI> CreateStockMovementQuery(
        IQueryable<STOK_HAREKETLERI> source,
        StockMovementDocumentLookupRequest request)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var query = source.Where(movement =>
            movement.sth_iptal != true &&
            movement.sth_evrakno_seri == documentSerie &&
            movement.sth_evrakno_sira == request.DocumentOrderNo);

        if (request.DocumentType.HasValue)
        {
            query = query.Where(movement => movement.sth_evraktip == request.DocumentType);
        }

        if (request.MovementType.HasValue)
        {
            query = query.Where(movement => movement.sth_tip == request.MovementType);
        }

        if (request.MovementKind.HasValue)
        {
            query = query.Where(movement => movement.sth_cins == request.MovementKind);
        }

        if (request.NormalReturn.HasValue)
        {
            query = query.Where(movement => movement.sth_normal_iade == request.NormalReturn);
        }

        if (request.WarehouseNo.HasValue)
        {
            var warehouseNo = request.WarehouseNo.Value;
            query = query.Where(movement =>
                movement.sth_giris_depo_no == warehouseNo ||
                movement.sth_cikis_depo_no == warehouseNo);
        }

        return query;
    }

    private IQueryable<CARI_HESAP_HAREKETLERI> CreateCustomerMovementQuery(
        IQueryable<CARI_HESAP_HAREKETLERI> source,
        CustomerMovementDocumentLookupRequest request)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var query = source.Where(movement =>
            movement.cha_iptal != true &&
            movement.cha_evrakno_seri == documentSerie &&
            movement.cha_evrakno_sira == request.DocumentOrderNo);

        if (request.DocumentType.HasValue)
        {
            query = query.Where(movement => movement.cha_evrak_tip == request.DocumentType);
        }

        if (request.MovementType.HasValue)
        {
            query = query.Where(movement => movement.cha_tip == request.MovementType);
        }

        if (request.MovementKind.HasValue)
        {
            query = query.Where(movement => movement.cha_cinsi == request.MovementKind);
        }

        if (request.NormalReturn.HasValue)
        {
            query = query.Where(movement => movement.cha_normal_Iade == request.NormalReturn);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            var customerCode = request.CustomerCode.Trim();
            query = query.Where(movement =>
                movement.cha_kod == customerCode ||
                movement.cha_ciro_cari_kodu == customerCode);
        }

        return query;
    }

    private async Task<StockMovementDocumentDto> MapStockMovementDocumentAsync(
        MikroDbContext lookupContext,
        IReadOnlyCollection<STOK_HAREKETLERI> rows,
        CancellationToken cancellationToken)
    {
        var stockCodes = rows
            .Select(row => row.sth_stok_kod)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var customerCodes = rows
            .Select(row => row.sth_cari_kodu)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warehouseNos = rows
            .SelectMany(row => new[]
            {
                row.sth_giris_depo_no,
                row.sth_cikis_depo_no,
                row.sth_nakliyedeposu
            })
            .Where(value => value.HasValue && value.Value > 0)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();

        var stocks = await LoadStocksAsync(lookupContext, stockCodes, cancellationToken);
        var customers = await LoadCustomersAsync(lookupContext, customerCodes, cancellationToken);
        var warehouses = await LoadWarehousesAsync(lookupContext, warehouseNos, cancellationToken);
        var first = rows.First();

        var lines = rows
            .OrderBy(row => row.sth_satirno)
            .ThenBy(row => row.sth_stok_kod)
            .Select(row =>
            {
                var stock = ResolveStock(stocks, row.sth_stok_kod);
                var unitPointer = NormalizeUnitPointer(row.sth_birim_pntr);
                var quantity = row.sth_miktar ?? 0d;
                var amount = row.sth_tutar ?? 0d;

                return new StockMovementDocumentLineDto(
                    row.sth_Guid,
                    row.sth_satirno ?? 0,
                    row.sth_malkbl_sevk_tarihi,
                    row.sth_stok_kod ?? string.Empty,
                    stock?.sto_isim ?? string.Empty,
                    unitPointer,
                    ResolveUnitName(unitPointer, stock),
                    quantity,
                    row.sth_miktar2 ?? 0d,
                    quantity == 0d ? 0d : amount / quantity,
                    amount,
                    row.sth_iskonto1 ?? 0d,
                    row.sth_iskonto2 ?? 0d,
                    row.sth_iskonto3 ?? 0d,
                    row.sth_iskonto4 ?? 0d,
                    row.sth_iskonto5 ?? 0d,
                    row.sth_iskonto6 ?? 0d,
                    row.sth_masraf1 ?? 0d,
                    row.sth_masraf2 ?? 0d,
                    row.sth_masraf3 ?? 0d,
                    row.sth_masraf4 ?? 0d,
                    row.sth_vergi_pntr ?? 0,
                    row.sth_vergi ?? 0d,
                    row.sth_netagirlik ?? 0d,
                    row.sth_brutagirlik ?? 0d,
                    row.sth_aciklama ?? string.Empty,
                    row.sth_parti_kodu ?? string.Empty,
                    row.sth_lot_no ?? 0,
                    row.sth_proje_kodu ?? string.Empty,
                    row.sth_cari_srm_merkezi ?? string.Empty,
                    row.sth_stok_srm_merkezi ?? string.Empty,
                    row.sth_giris_depo_no ?? 0,
                    row.sth_cikis_depo_no ?? 0,
                    row.sth_lastup_date);
            })
            .ToArray();

        var customer = ResolveCustomer(customers, first.sth_cari_kodu);
        var inputWarehouseName = ResolveWarehouseName(warehouses, first.sth_giris_depo_no);
        var outputWarehouseName = ResolveWarehouseName(warehouses, first.sth_cikis_depo_no);
        var shippingWarehouseName = ResolveWarehouseName(warehouses, first.sth_nakliyedeposu);

        var header = new StockMovementDocumentHeaderDto(
            first.sth_evrakno_seri ?? string.Empty,
            first.sth_evrakno_sira ?? 0,
            first.sth_evraktip ?? 0,
            rows.Select(row => row.sth_tip ?? 0).Distinct().OrderBy(value => value).ToArray(),
            first.sth_cins ?? 0,
            first.sth_normal_iade ?? 0,
            first.sth_tarih,
            first.sth_belge_tarih,
            first.sth_malkbl_sevk_tarihi,
            first.sth_belge_no ?? string.Empty,
            first.sth_cari_kodu ?? string.Empty,
            BuildCustomerTitle(customer),
            first.sth_giris_depo_no ?? 0,
            inputWarehouseName,
            first.sth_cikis_depo_no ?? 0,
            outputWarehouseName,
            first.sth_nakliyedeposu ?? 0,
            shippingWarehouseName,
            first.sth_aciklama ?? string.Empty,
            first.sth_HareketGrupKodu1 ?? string.Empty,
            first.sth_HareketGrupKodu2 ?? string.Empty,
            first.sth_HareketGrupKodu3 ?? string.Empty,
            first.sth_cari_srm_merkezi ?? string.Empty,
            first.sth_stok_srm_merkezi ?? string.Empty,
            first.sth_proje_kodu ?? string.Empty,
            lines.Length,
            lines.Sum(line => line.Quantity),
            lines.Sum(line => line.Amount),
            rows.Min(row => row.sth_create_date),
            rows.Max(row => row.sth_lastup_date));

        return new StockMovementDocumentDto(header, lines);
    }

    private async Task<CustomerMovementDocumentDto> MapCustomerMovementDocumentAsync(
        MikroDbContext lookupContext,
        IReadOnlyCollection<CARI_HESAP_HAREKETLERI> rows,
        CancellationToken cancellationToken)
    {
        var customerCodes = rows
            .SelectMany(row => new[] { row.cha_kod, row.cha_ciro_cari_kodu })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var customers = await LoadCustomersAsync(lookupContext, customerCodes, cancellationToken);
        var first = rows.First();

        var lines = rows
            .OrderBy(row => row.cha_satir_no)
            .ThenBy(row => row.cha_kod)
            .Select(row =>
            {
                var customer = ResolveCustomer(customers, row.cha_kod);

                return new CustomerMovementDocumentLineDto(
                    row.cha_Guid,
                    row.cha_satir_no ?? 0,
                    row.cha_kod ?? string.Empty,
                    row.cha_ciro_cari_kodu ?? string.Empty,
                    BuildCustomerTitle(customer),
                    row.cha_tip ?? 0,
                    row.cha_cinsi ?? 0,
                    row.cha_normal_Iade ?? 0,
                    row.cha_miktari ?? 0d,
                    row.cha_meblag ?? 0d,
                    row.cha_aratoplam ?? 0d,
                    row.cha_vade ?? 0,
                    row.cha_ft_iskonto1 ?? 0d,
                    row.cha_ft_iskonto2 ?? 0d,
                    row.cha_ft_iskonto3 ?? 0d,
                    row.cha_ft_iskonto4 ?? 0d,
                    row.cha_ft_iskonto5 ?? 0d,
                    row.cha_ft_iskonto6 ?? 0d,
                    row.cha_ft_masraf1 ?? 0d,
                    row.cha_ft_masraf2 ?? 0d,
                    row.cha_ft_masraf3 ?? 0d,
                    row.cha_ft_masraf4 ?? 0d,
                    row.cha_vergi1 ?? 0d,
                    row.cha_vergi2 ?? 0d,
                    row.cha_vergi3 ?? 0d,
                    row.cha_vergi4 ?? 0d,
                    row.cha_vergi5 ?? 0d,
                    row.cha_aciklama ?? string.Empty,
                    row.cha_satici_kodu ?? string.Empty,
                    row.cha_projekodu ?? string.Empty,
                    row.cha_srmrkkodu ?? string.Empty,
                    row.cha_lastup_date);
            })
            .ToArray();

        var firstCustomer = ResolveCustomer(customers, first.cha_kod);
        var header = new CustomerMovementDocumentHeaderDto(
            first.cha_evrakno_seri ?? string.Empty,
            first.cha_evrakno_sira ?? 0,
            first.cha_evrak_tip ?? 0,
            rows.Select(row => row.cha_tip ?? 0).Distinct().OrderBy(value => value).ToArray(),
            first.cha_cinsi ?? 0,
            first.cha_normal_Iade ?? 0,
            first.cha_tarihi,
            first.cha_belge_tarih,
            first.cha_belge_no ?? string.Empty,
            first.cha_kod ?? string.Empty,
            first.cha_ciro_cari_kodu ?? string.Empty,
            BuildCustomerTitle(firstCustomer),
            first.cha_aciklama ?? string.Empty,
            first.cha_satici_kodu ?? string.Empty,
            first.cha_projekodu ?? string.Empty,
            first.cha_srmrkkodu ?? string.Empty,
            lines.Length,
            lines.Sum(line => line.Quantity),
            lines.Sum(line => line.Amount),
            lines.Sum(line => line.SubAmount),
            rows.Min(row => row.cha_create_date),
            rows.Max(row => row.cha_lastup_date));

        return new CustomerMovementDocumentDto(header, lines);
    }

    private static async Task<Dictionary<string, STOKLAR>> LoadStocksAsync(
        MikroDbContext lookupContext,
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        if (stockCodes.Count == 0)
        {
            return new Dictionary<string, STOKLAR>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await lookupContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stockCodes.Contains(stock.sto_kod))
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(stock => stock.sto_kod, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, CARI_HESAPLAR>> LoadCustomersAsync(
        MikroDbContext lookupContext,
        IReadOnlyCollection<string> customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Count == 0)
        {
            return new Dictionary<string, CARI_HESAPLAR>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await lookupContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer => customer.cari_kod != null && customerCodes.Contains(customer.cari_kod))
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(customer => !string.IsNullOrWhiteSpace(customer.cari_kod))
            .ToDictionary(customer => customer.cari_kod!, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<int, string>> LoadWarehousesAsync(
        MikroDbContext lookupContext,
        IReadOnlyCollection<int> warehouseNos,
        CancellationToken cancellationToken)
    {
        if (warehouseNos.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var rows = await lookupContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no.HasValue && warehouseNos.Contains(warehouse.dep_no.Value))
            .Select(warehouse => new { WarehouseNo = warehouse.dep_no.GetValueOrDefault(), warehouse.dep_adi })
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(warehouse => warehouse.WarehouseNo, warehouse => warehouse.dep_adi ?? string.Empty);
    }

    private async Task EnsureCustomerCardReferencesExistAsync(
        CustomerCardPatchDto patch,
        CancellationToken cancellationToken)
    {
        var customerCodes = new[] { patch.ParentCustomerCode }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRequiredText(value!, 25, nameof(patch.ParentCustomerCode)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warehouseNos = new[] { patch.DefaultInputWarehouseNo, patch.DefaultOutputWarehouseNo }
            .Where(value => value.HasValue && value.Value > 0)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();

        await EnsureCustomerCodesExistAsync(customerCodes, cancellationToken);
        await EnsureWarehouseNosExistAsync(warehouseNos, cancellationToken);
    }

    private async Task EnsureStockMovementReferencesExistAsync(
        UpdateStockMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var stockCodes = request.Lines
            .Select(line => line.StockCode)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRequiredText(value!, 25, nameof(StockMovementLinePatchDto.StockCode)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var customerCodes = new[] { request.Header?.CustomerCode }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRequiredText(value!, 25, nameof(StockMovementHeaderPatchDto.CustomerCode)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warehouseNos = request.Lines
            .SelectMany(line => new[] { line.InputWarehouseNo, line.OutputWarehouseNo })
            .Concat(new[]
            {
                request.Header?.InputWarehouseNo,
                request.Header?.OutputWarehouseNo,
                request.Header?.ShippingWarehouseNo
            })
            .Where(value => value.HasValue && value.Value > 0)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();

        await EnsureStockCodesExistAsync(stockCodes, cancellationToken);
        await EnsureCustomerCodesExistAsync(customerCodes, cancellationToken);
        await EnsureWarehouseNosExistAsync(warehouseNos, cancellationToken);
    }

    private async Task EnsureCustomerMovementReferencesExistAsync(
        UpdateCustomerMovementDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var customerCodes = request.Lines
            .SelectMany(line => new[] { line.CustomerCode, line.TurnoverCustomerCode })
            .Concat(new[] { request.Header?.CustomerCode, request.Header?.TurnoverCustomerCode })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeRequiredText(value!, 25, "CustomerCode"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await EnsureCustomerCodesExistAsync(customerCodes, cancellationToken);
    }

    private async Task EnsureStockCodesExistAsync(
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        if (stockCodes.Count == 0)
        {
            return;
        }

        var existingCodes = await mikroWriteDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock => stockCodes.Contains(stock.sto_kod))
            .Select(stock => stock.sto_kod)
            .ToArrayAsync(cancellationToken);
        var missingCodes = stockCodes
            .Except(existingCodes, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missingCodes.Length > 0)
        {
            throw new KeyNotFoundException($"Stock card was not found: {string.Join(", ", missingCodes)}");
        }
    }

    private async Task EnsureCustomerCodesExistAsync(
        IReadOnlyCollection<string> customerCodes,
        CancellationToken cancellationToken)
    {
        if (customerCodes.Count == 0)
        {
            return;
        }

        var existingCodes = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer => customerCodes.Contains(customer.cari_kod))
            .Select(customer => customer.cari_kod)
            .ToArrayAsync(cancellationToken);
        var missingCodes = customerCodes
            .Except(existingCodes, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missingCodes.Length > 0)
        {
            throw new KeyNotFoundException($"Customer was not found: {string.Join(", ", missingCodes)}");
        }
    }

    private async Task EnsureWarehouseNosExistAsync(
        IReadOnlyCollection<int> warehouseNos,
        CancellationToken cancellationToken)
    {
        if (warehouseNos.Count == 0)
        {
            return;
        }

        var existingNos = await mikroWriteDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no.HasValue && warehouseNos.Contains(warehouse.dep_no.Value))
            .Select(warehouse => warehouse.dep_no.GetValueOrDefault())
            .ToArrayAsync(cancellationToken);
        var missingNos = warehouseNos
            .Except(existingNos)
            .ToArray();

        if (missingNos.Length > 0)
        {
            throw new KeyNotFoundException($"Warehouse was not found: {string.Join(", ", missingNos)}");
        }
    }

    private static WarehouseCardDetailDto MapWarehouseCardDetail(DEPOLAR warehouse) =>
        new(
            warehouse.dep_Guid,
            warehouse.dep_no ?? 0,
            warehouse.dep_adi ?? string.Empty,
            warehouse.dep_grup_kodu ?? string.Empty,
            warehouse.dep_tipi ?? 0,
            warehouse.dep_DepoSevkOtoFiyat ?? 0,
            warehouse.dep_hareket_tipi ?? 0,
            warehouse.dep_muh_kodu ?? string.Empty,
            warehouse.dep_sor_mer_kodu ?? string.Empty,
            warehouse.dep_proje_kodu ?? string.Empty,
            warehouse.dep_DepoSevkUygFiyat ?? 0,
            warehouse.dep_KilitTarihi,
            warehouse.dep_cadde ?? string.Empty,
            warehouse.dep_mahalle ?? string.Empty,
            warehouse.dep_sokak ?? string.Empty,
            warehouse.dep_Semt ?? string.Empty,
            warehouse.dep_Apt_No ?? string.Empty,
            warehouse.dep_Daire_No ?? string.Empty,
            warehouse.dep_posta_Kodu ?? string.Empty,
            warehouse.dep_Ilce ?? string.Empty,
            warehouse.dep_Il ?? string.Empty,
            warehouse.dep_Ulke ?? string.Empty,
            warehouse.dep_Adres_kodu ?? string.Empty,
            warehouse.dep_gps_enlem ?? 0d,
            warehouse.dep_gps_boylam ?? 0d,
            warehouse.dep_yetkili_email ?? string.Empty,
            warehouse.dep_tel_ulke_kodu ?? string.Empty,
            warehouse.dep_tel_bolge_kodu ?? string.Empty,
            warehouse.dep_tel_no1 ?? string.Empty,
            warehouse.dep_tel_no2 ?? string.Empty,
            warehouse.dep_tel_faxno ?? string.Empty,
            warehouse.dep_envanter_harici_fl ?? false,
            warehouse.dep_detay_takibi ?? 0,
            warehouse.dep_bolge_kodu ?? string.Empty,
            warehouse.dep_gidiste_eirsaliye_fl ?? false,
            warehouse.dep_geliste_eirsaliye_fl ?? false,
            warehouse.dep_iptal ?? false,
            warehouse.dep_hidden ?? false,
            warehouse.dep_kilitli ?? false,
            warehouse.dep_create_date,
            warehouse.dep_lastup_date);

    private static CustomerCardDetailDto MapCustomerCardDetail(CARI_HESAPLAR customer) =>
        new(
            customer.cari_Guid,
            customer.cari_kod ?? string.Empty,
            customer.cari_unvan1 ?? string.Empty,
            customer.cari_unvan2 ?? string.Empty,
            customer.cari_hareket_tipi ?? 0,
            customer.cari_baglanti_tipi ?? 0,
            customer.cari_stok_alim_cinsi ?? 0,
            customer.cari_stok_satim_cinsi ?? 0,
            customer.cari_muh_kod ?? string.Empty,
            customer.cari_muh_kod1 ?? string.Empty,
            customer.cari_muh_kod2 ?? string.Empty,
            customer.cari_doviz_cinsi ?? 0,
            customer.cari_doviz_cinsi1 ?? 0,
            customer.cari_doviz_cinsi2 ?? 0,
            customer.cari_vdaire_adi ?? string.Empty,
            customer.cari_vdaire_no ?? string.Empty,
            customer.cari_sicil_no ?? string.Empty,
            customer.cari_VergiKimlikNo ?? string.Empty,
            customer.cari_satis_fk ?? 0,
            customer.cari_odeme_cinsi ?? 0,
            customer.cari_odeme_gunu ?? 0,
            customer.cari_odemeplan_no ?? 0,
            customer.cari_opsiyon_gun ?? 0,
            customer.cari_fatura_adres_no ?? 0,
            customer.cari_sevk_adres_no ?? 0,
            customer.cari_Ana_cari_kodu ?? string.Empty,
            customer.cari_sektor_kodu ?? string.Empty,
            customer.cari_bolge_kodu ?? string.Empty,
            customer.cari_grup_kodu ?? string.Empty,
            customer.cari_temsilci_kodu ?? string.Empty,
            customer.cari_firma_acik_kapal ?? false,
            customer.cari_cari_kilitli_flg ?? false,
            customer.cari_efatura_fl ?? false,
            customer.cari_def_efatura_cinsi ?? 0,
            customer.cari_eirsaliye_fl ?? false,
            customer.cari_def_eirsaliye_cinsi ?? 0,
            customer.cari_wwwadresi ?? string.Empty,
            customer.cari_EMail ?? string.Empty,
            customer.cari_CepTel ?? string.Empty,
            customer.cari_VarsayilanGirisDepo ?? 0,
            customer.cari_VarsayilanCikisDepo ?? 0,
            customer.cari_KEP_adresi ?? string.Empty,
            customer.cari_mutabakat_mail_adresi ?? string.Empty,
            customer.cari_mersis_no ?? string.Empty,
            customer.cari_vergidairekodu ?? string.Empty,
            customer.cari_Perakende_fl ?? false,
            customer.cari_create_date,
            customer.cari_lastup_date);

    private static StockCardDetailDto MapStockCardDetail(STOKLAR stock) =>
        new(
            stock.sto_kod,
            stock.sto_isim ?? string.Empty,
            stock.sto_kisa_ismi ?? string.Empty,
            stock.sto_yabanci_isim ?? string.Empty,
            stock.sto_sat_cari_kod ?? string.Empty,
            stock.sto_cins ?? 0,
            stock.sto_doviz_cinsi ?? 0,
            stock.sto_detay_takip ?? 0,
            stock.sto_birim1_ad ?? string.Empty,
            stock.sto_birim2_ad ?? string.Empty,
            stock.sto_birim3_ad ?? string.Empty,
            stock.sto_birim4_ad ?? string.Empty,
            stock.sto_perakende_vergi ?? 0,
            stock.sto_toptan_vergi ?? 0,
            stock.sto_kategori_kodu ?? string.Empty,
            stock.sto_anagrup_kod ?? string.Empty,
            stock.sto_altgrup_kod ?? string.Empty,
            stock.sto_marka_kodu ?? string.Empty,
            stock.sto_sektor_kodu ?? string.Empty,
            stock.sto_reyon_kodu ?? string.Empty,
            stock.sto_uretici_kodu ?? string.Empty,
            stock.sto_urun_sorkod ?? string.Empty,
            stock.sto_yer_kod ?? string.Empty,
            ToBool(stock.sto_satis_dursun),
            ToBool(stock.sto_siparis_dursun),
            ToBool(stock.sto_malkabul_dursun),
            stock.sto_pasif_fl ?? false,
            stock.sto_iskon_yapilamaz ?? false,
            stock.sto_create_date,
            stock.sto_lastup_date);

    private static StockCardWarehouseSettingsDto MapStockCardWarehouseSettings(
        STOKLAR stock,
        int warehouseNo,
        string warehouseName,
        STOK_DEPO_DETAYLARI? detail)
    {
        var globalSalesStopped = ToBool(stock.sto_satis_dursun);
        var globalOrderStopped = ToBool(stock.sto_siparis_dursun);
        var globalReceivingStopped = ToBool(stock.sto_malkabul_dursun);
        var globalIsPassive = stock.sto_pasif_fl ?? false;
        var globalDiscountDisabled = stock.sto_iskon_yapilamaz ?? false;

        return new StockCardWarehouseSettingsDto(
            stock.sto_kod,
            warehouseNo,
            warehouseName,
            detail is not null,
            HasStockCardWarehouseOverride(detail),
            globalSalesStopped,
            globalOrderStopped,
            globalReceivingStopped,
            globalIsPassive,
            globalDiscountDisabled,
            detail?.sdp_satisdursun.HasValue == true
                ? ToBool(detail.sdp_satisdursun)
                : globalSalesStopped,
            detail?.sdp_sipdursun.HasValue == true
                ? ToBool(detail.sdp_sipdursun)
                : globalOrderStopped,
            detail?.sdp_malkabuldursun.HasValue == true
                ? ToBool(detail.sdp_malkabuldursun)
                : globalReceivingStopped,
            detail?.sdp_Pasif_fl ?? globalIsPassive,
            detail?.sdp_IskontoYapilamaz ?? globalDiscountDisabled,
            detail?.sdp_lastup_date);
    }

    private static StockSalesPriceDto MapStockSalesPrice(
        STOK_SATIS_FIYAT_LISTELERI row,
        STOKLAR stock,
        IReadOnlyDictionary<int, string> warehouseNames,
        IReadOnlyDictionary<int, string> priceListNames)
    {
        var warehouseNo = row.sfiyat_deposirano.GetValueOrDefault();
        var priceListNo = row.sfiyat_listesirano.GetValueOrDefault();
        var unitPointer = NormalizeUnitPointer(row.sfiyat_birim_pntr);

        return new StockSalesPriceDto(
            row.sfiyat_Guid,
            row.sfiyat_stokkod ?? stock.sto_kod,
            priceListNo,
            priceListNames.GetValueOrDefault(priceListNo, string.Empty),
            warehouseNo,
            warehouseNames.GetValueOrDefault(warehouseNo, string.Empty),
            row.sfiyat_odemeplan.GetValueOrDefault(),
            unitPointer,
            ResolveUnitName(unitPointer, stock),
            row.sfiyat_fiyati.GetValueOrDefault(),
            row.sfiyat_doviz.GetValueOrDefault(),
            row.sfiyat_deg_nedeni.GetValueOrDefault(),
            row.sfiyat_create_date,
            row.sfiyat_lastup_date);
    }

    private static STOK_SATIS_FIYAT_LISTELERI CreateStockSalesPrice(
        UpsertStockSalesPriceRequest request,
        string stockCode,
        short updateUser,
        DateTime updatedAt) =>
        new()
        {
            sfiyat_Guid = Guid.NewGuid(),
            sfiyat_DBCno = 0,
            sfiyat_SpecRECno = 0,
            sfiyat_iptal = false,
            sfiyat_fileid = StockSalesPriceFileId,
            sfiyat_hidden = false,
            sfiyat_kilitli = false,
            sfiyat_degisti = true,
            sfiyat_checksum = 0,
            sfiyat_create_user = updateUser,
            sfiyat_create_date = updatedAt,
            sfiyat_lastup_user = updateUser,
            sfiyat_lastup_date = updatedAt,
            sfiyat_special1 = string.Empty,
            sfiyat_special2 = string.Empty,
            sfiyat_special3 = string.Empty,
            sfiyat_stokkod = stockCode,
            sfiyat_listesirano = request.PriceListNo,
            sfiyat_deposirano = request.WarehouseNo,
            sfiyat_odemeplan = request.PaymentPlanNo,
            sfiyat_birim_pntr = request.UnitPointer,
            sfiyat_fiyati = request.Price,
            sfiyat_doviz = request.CurrencyType,
            sfiyat_iskontokod = string.Empty,
            sfiyat_deg_nedeni = request.ChangeReason,
            sfiyat_primyuzdesi = 0d,
            sfiyat_kampanyakod = string.Empty,
            sfiyat_doviz_kuru = 0d
        };

    private static STOK_DEPO_DETAYLARI CreateStockCardWarehouseDetail(
        string stockCode,
        int warehouseNo,
        short updateUser,
        DateTime updatedAt) =>
        new()
        {
            sdp_Guid = Guid.NewGuid(),
            sdp_DBCno = 0,
            sdp_SpecRECno = 0,
            sdp_iptal = false,
            sdp_hidden = false,
            sdp_kilitli = false,
            sdp_degisti = true,
            sdp_checksum = 0,
            sdp_create_user = updateUser,
            sdp_create_date = updatedAt,
            sdp_lastup_user = updateUser,
            sdp_lastup_date = updatedAt,
            sdp_depo_kod = stockCode,
            sdp_depo_no = warehouseNo
        };

    private static void ApplyStockCardWarehousePatch(
        STOK_DEPO_DETAYLARI detail,
        StockCardWarehousePatchDto patch)
    {
        if (patch.ResetToGlobal)
        {
            detail.sdp_satisdursun = null;
            detail.sdp_sipdursun = null;
            detail.sdp_malkabuldursun = null;
            detail.sdp_Pasif_fl = null;
            detail.sdp_IskontoYapilamaz = null;
        }

        if (patch.SalesStopped.HasValue)
        {
            detail.sdp_satisdursun = ToByteFlag(patch.SalesStopped.Value);
        }

        if (patch.OrderStopped.HasValue)
        {
            detail.sdp_sipdursun = ToByteFlag(patch.OrderStopped.Value);
        }

        if (patch.ReceivingStopped.HasValue)
        {
            detail.sdp_malkabuldursun = ToByteFlag(patch.ReceivingStopped.Value);
        }

        if (patch.IsPassive.HasValue)
        {
            detail.sdp_Pasif_fl = patch.IsPassive.Value;
        }

        if (patch.DiscountDisabled.HasValue)
        {
            detail.sdp_IskontoYapilamaz = patch.DiscountDisabled.Value;
        }
    }

    private static bool HasStockCardWarehousePatch(StockCardWarehousePatchDto patch) =>
        patch.ResetToGlobal ||
        HasStockCardWarehouseValuePatch(patch);

    private static bool HasStockCardWarehouseValuePatch(StockCardWarehousePatchDto patch) =>
        patch.SalesStopped.HasValue ||
        patch.OrderStopped.HasValue ||
        patch.ReceivingStopped.HasValue ||
        patch.IsPassive.HasValue ||
        patch.DiscountDisabled.HasValue;

    private static bool HasStockCardWarehouseOverride(STOK_DEPO_DETAYLARI? detail) =>
        detail is not null &&
        (detail.sdp_satisdursun.HasValue ||
         detail.sdp_sipdursun.HasValue ||
         detail.sdp_malkabuldursun.HasValue ||
         detail.sdp_Pasif_fl.HasValue ||
         detail.sdp_IskontoYapilamaz.HasValue);

    private static bool ApplyWarehouseCardPatch(DEPOLAR warehouse, WarehouseCardPatchDto patch)
    {
        var changed = false;
        SetIfPresent(patch.Name, value => warehouse.dep_adi = NormalizeText(value, 50, nameof(patch.Name)), ref changed);
        SetIfPresent(patch.GroupCode, value => warehouse.dep_grup_kodu = NormalizeText(value, 25, nameof(patch.GroupCode)), ref changed);
        SetIfPresent(patch.WarehouseType, value => warehouse.dep_tipi = value, ref changed);
        SetIfPresent(patch.ShipmentAutoPriceType, value => warehouse.dep_DepoSevkOtoFiyat = value, ref changed);
        SetIfPresent(patch.MovementType, value => warehouse.dep_hareket_tipi = value, ref changed);
        SetIfPresent(patch.AccountingCode, value => warehouse.dep_muh_kodu = NormalizeText(value, 40, nameof(patch.AccountingCode)), ref changed);
        SetIfPresent(patch.ResponsibilityCenter, value => warehouse.dep_sor_mer_kodu = NormalizeText(value, 25, nameof(patch.ResponsibilityCenter)), ref changed);
        SetIfPresent(patch.ProjectCode, value => warehouse.dep_proje_kodu = NormalizeText(value, 25, nameof(patch.ProjectCode)), ref changed);
        SetIfPresent(patch.ShipmentAppliedPriceNo, value => warehouse.dep_DepoSevkUygFiyat = ValidateNonNegative(value, nameof(patch.ShipmentAppliedPriceNo)), ref changed);
        SetIfPresent(patch.LockDate, value => warehouse.dep_KilitTarihi = value.Date, ref changed);
        SetIfPresent(patch.Street, value => warehouse.dep_cadde = NormalizeText(value, 50, nameof(patch.Street)), ref changed);
        SetIfPresent(patch.Neighborhood, value => warehouse.dep_mahalle = NormalizeText(value, 50, nameof(patch.Neighborhood)), ref changed);
        SetIfPresent(patch.Avenue, value => warehouse.dep_sokak = NormalizeText(value, 50, nameof(patch.Avenue)), ref changed);
        SetIfPresent(patch.Quarter, value => warehouse.dep_Semt = NormalizeText(value, 25, nameof(patch.Quarter)), ref changed);
        SetIfPresent(patch.ApartmentNo, value => warehouse.dep_Apt_No = NormalizeText(value, 10, nameof(patch.ApartmentNo)), ref changed);
        SetIfPresent(patch.ApartmentUnitNo, value => warehouse.dep_Daire_No = NormalizeText(value, 10, nameof(patch.ApartmentUnitNo)), ref changed);
        SetIfPresent(patch.PostalCode, value => warehouse.dep_posta_Kodu = NormalizeText(value, 8, nameof(patch.PostalCode)), ref changed);
        SetIfPresent(patch.District, value => warehouse.dep_Ilce = NormalizeText(value, 50, nameof(patch.District)), ref changed);
        SetIfPresent(patch.City, value => warehouse.dep_Il = NormalizeText(value, 50, nameof(patch.City)), ref changed);
        SetIfPresent(patch.Country, value => warehouse.dep_Ulke = NormalizeText(value, 50, nameof(patch.Country)), ref changed);
        SetIfPresent(patch.AddressCode, value => warehouse.dep_Adres_kodu = NormalizeText(value, 10, nameof(patch.AddressCode)), ref changed);
        SetIfPresent(patch.Latitude, value => warehouse.dep_gps_enlem = ValidateLatitude(value, nameof(patch.Latitude)), ref changed);
        SetIfPresent(patch.Longitude, value => warehouse.dep_gps_boylam = ValidateLongitude(value, nameof(patch.Longitude)), ref changed);
        SetIfPresent(patch.AuthorizedEmail, value => warehouse.dep_yetkili_email = NormalizeText(value, 50, nameof(patch.AuthorizedEmail)), ref changed);
        SetIfPresent(patch.PhoneCountryCode, value => warehouse.dep_tel_ulke_kodu = NormalizeText(value, 5, nameof(patch.PhoneCountryCode)), ref changed);
        SetIfPresent(patch.PhoneAreaCode, value => warehouse.dep_tel_bolge_kodu = NormalizeText(value, 5, nameof(patch.PhoneAreaCode)), ref changed);
        SetIfPresent(patch.PhoneNo1, value => warehouse.dep_tel_no1 = NormalizeText(value, 10, nameof(patch.PhoneNo1)), ref changed);
        SetIfPresent(patch.PhoneNo2, value => warehouse.dep_tel_no2 = NormalizeText(value, 10, nameof(patch.PhoneNo2)), ref changed);
        SetIfPresent(patch.FaxNo, value => warehouse.dep_tel_faxno = NormalizeText(value, 10, nameof(patch.FaxNo)), ref changed);
        SetIfPresent(patch.ExcludedFromInventory, value => warehouse.dep_envanter_harici_fl = value, ref changed);
        SetIfPresent(patch.DetailTrackingType, value => warehouse.dep_detay_takibi = value, ref changed);
        SetIfPresent(patch.RegionCode, value => warehouse.dep_bolge_kodu = NormalizeText(value, 25, nameof(patch.RegionCode)), ref changed);
        SetIfPresent(patch.OutgoingEDespatchEnabled, value => warehouse.dep_gidiste_eirsaliye_fl = value, ref changed);
        SetIfPresent(patch.IncomingEDespatchEnabled, value => warehouse.dep_geliste_eirsaliye_fl = value, ref changed);
        SetIfPresent(patch.IsPassive, value => warehouse.dep_iptal = value, ref changed);
        SetIfPresent(patch.IsHidden, value => warehouse.dep_hidden = value, ref changed);
        SetIfPresent(patch.IsLocked, value => warehouse.dep_kilitli = value, ref changed);

        return changed;
    }

    private static bool ApplyCustomerCardPatch(CARI_HESAPLAR customer, CustomerCardPatchDto patch)
    {
        var changed = false;
        SetIfPresent(patch.Title1, value => customer.cari_unvan1 = NormalizeText(value, 127, nameof(patch.Title1)), ref changed);
        SetIfPresent(patch.Title2, value => customer.cari_unvan2 = NormalizeText(value, 127, nameof(patch.Title2)), ref changed);
        SetIfPresent(patch.MovementType, value => customer.cari_hareket_tipi = value, ref changed);
        SetIfPresent(patch.ConnectionType, value => customer.cari_baglanti_tipi = value, ref changed);
        SetIfPresent(patch.PurchaseStockType, value => customer.cari_stok_alim_cinsi = value, ref changed);
        SetIfPresent(patch.SalesStockType, value => customer.cari_stok_satim_cinsi = value, ref changed);
        SetIfPresent(patch.AccountingCode, value => customer.cari_muh_kod = NormalizeText(value, 40, nameof(patch.AccountingCode)), ref changed);
        SetIfPresent(patch.AccountingCode1, value => customer.cari_muh_kod1 = NormalizeText(value, 40, nameof(patch.AccountingCode1)), ref changed);
        SetIfPresent(patch.AccountingCode2, value => customer.cari_muh_kod2 = NormalizeText(value, 40, nameof(patch.AccountingCode2)), ref changed);
        SetIfPresent(patch.CurrencyType, value => customer.cari_doviz_cinsi = value, ref changed);
        SetIfPresent(patch.CurrencyType1, value => customer.cari_doviz_cinsi1 = value, ref changed);
        SetIfPresent(patch.CurrencyType2, value => customer.cari_doviz_cinsi2 = value, ref changed);
        SetIfPresent(patch.TaxOffice, value => customer.cari_vdaire_adi = NormalizeText(value, 50, nameof(patch.TaxOffice)), ref changed);
        SetIfPresent(patch.TaxOfficeNo, value => customer.cari_vdaire_no = NormalizeText(value, 15, nameof(patch.TaxOfficeNo)), ref changed);
        SetIfPresent(patch.RegistryNo, value => customer.cari_sicil_no = NormalizeText(value, 15, nameof(patch.RegistryNo)), ref changed);
        SetIfPresent(patch.TaxNo, value => customer.cari_VergiKimlikNo = NormalizeText(value, 10, nameof(patch.TaxNo)), ref changed);
        SetIfPresent(patch.SalesPriceListNo, value => customer.cari_satis_fk = ValidateNonNegative(value, nameof(patch.SalesPriceListNo)), ref changed);
        SetIfPresent(patch.PaymentType, value => customer.cari_odeme_cinsi = value, ref changed);
        SetIfPresent(patch.PaymentDay, value => customer.cari_odeme_gunu = value, ref changed);
        SetIfPresent(patch.PaymentPlanNo, value => customer.cari_odemeplan_no = ValidateNonNegative(value, nameof(patch.PaymentPlanNo)), ref changed);
        SetIfPresent(patch.OptionDay, value => customer.cari_opsiyon_gun = ValidateNonNegative(value, nameof(patch.OptionDay)), ref changed);
        SetIfPresent(patch.InvoiceAddressNo, value => customer.cari_fatura_adres_no = ValidateNonNegative(value, nameof(patch.InvoiceAddressNo)), ref changed);
        SetIfPresent(patch.ShippingAddressNo, value => customer.cari_sevk_adres_no = ValidateNonNegative(value, nameof(patch.ShippingAddressNo)), ref changed);
        SetIfPresent(patch.ParentCustomerCode, value => customer.cari_Ana_cari_kodu = NormalizeText(value, 25, nameof(patch.ParentCustomerCode)), ref changed);
        SetIfPresent(patch.SectorCode, value => customer.cari_sektor_kodu = NormalizeText(value, 25, nameof(patch.SectorCode)), ref changed);
        SetIfPresent(patch.RegionCode, value => customer.cari_bolge_kodu = NormalizeText(value, 25, nameof(patch.RegionCode)), ref changed);
        SetIfPresent(patch.GroupCode, value => customer.cari_grup_kodu = NormalizeText(value, 25, nameof(patch.GroupCode)), ref changed);
        SetIfPresent(patch.RepresentativeCode, value => customer.cari_temsilci_kodu = NormalizeText(value, 25, nameof(patch.RepresentativeCode)), ref changed);
        SetIfPresent(patch.IsClosed, value => customer.cari_firma_acik_kapal = value, ref changed);
        SetIfPresent(patch.IsLocked, value => customer.cari_cari_kilitli_flg = value, ref changed);
        SetIfPresent(patch.EInvoiceEnabled, value => customer.cari_efatura_fl = value, ref changed);
        SetIfPresent(patch.DefaultEInvoiceType, value => customer.cari_def_efatura_cinsi = value, ref changed);
        SetIfPresent(patch.EDespatchEnabled, value => customer.cari_eirsaliye_fl = value, ref changed);
        SetIfPresent(patch.DefaultEDespatchType, value => customer.cari_def_eirsaliye_cinsi = value, ref changed);
        SetIfPresent(patch.Website, value => customer.cari_wwwadresi = NormalizeText(value, 30, nameof(patch.Website)), ref changed);
        SetIfPresent(patch.Email, value => customer.cari_EMail = NormalizeText(value, 127, nameof(patch.Email)), ref changed);
        SetIfPresent(patch.MobilePhone, value => customer.cari_CepTel = NormalizeText(value, 20, nameof(patch.MobilePhone)), ref changed);
        SetIfPresent(patch.DefaultInputWarehouseNo, value => customer.cari_VarsayilanGirisDepo = ValidateNonNegative(value, nameof(patch.DefaultInputWarehouseNo)), ref changed);
        SetIfPresent(patch.DefaultOutputWarehouseNo, value => customer.cari_VarsayilanCikisDepo = ValidateNonNegative(value, nameof(patch.DefaultOutputWarehouseNo)), ref changed);
        SetIfPresent(patch.KepAddress, value => customer.cari_KEP_adresi = NormalizeText(value, 80, nameof(patch.KepAddress)), ref changed);
        SetIfPresent(patch.ReconciliationEmail, value => customer.cari_mutabakat_mail_adresi = NormalizeText(value, 80, nameof(patch.ReconciliationEmail)), ref changed);
        SetIfPresent(patch.MersisNo, value => customer.cari_mersis_no = NormalizeText(value, 25, nameof(patch.MersisNo)), ref changed);
        SetIfPresent(patch.TaxOfficeCode, value => customer.cari_vergidairekodu = NormalizeText(value, 10, nameof(patch.TaxOfficeCode)), ref changed);
        SetIfPresent(patch.RetailCustomer, value => customer.cari_Perakende_fl = value, ref changed);

        return changed;
    }

    private static bool ApplyStockCardPatch(STOKLAR stock, StockCardPatchDto patch)
    {
        var changed = false;
        SetIfPresent(patch.Name, value => stock.sto_isim = NormalizeText(value, 127, nameof(patch.Name)), ref changed);
        SetIfPresent(patch.ShortName, value => stock.sto_kisa_ismi = NormalizeText(value, 50, nameof(patch.ShortName)), ref changed);
        SetIfPresent(patch.ForeignName, value => stock.sto_yabanci_isim = NormalizeText(value, 127, nameof(patch.ForeignName)), ref changed);
        SetIfPresent(patch.SupplierCode, value => stock.sto_sat_cari_kod = NormalizeText(value, 25, nameof(patch.SupplierCode)), ref changed);
        SetIfPresent(patch.StockType, value => stock.sto_cins = value, ref changed);
        SetIfPresent(patch.CurrencyType, value => stock.sto_doviz_cinsi = value, ref changed);
        SetIfPresent(patch.TrackingType, value => stock.sto_detay_takip = value, ref changed);
        SetIfPresent(patch.Unit1Name, value => stock.sto_birim1_ad = NormalizeText(value, 10, nameof(patch.Unit1Name)), ref changed);
        SetIfPresent(patch.Unit2Name, value => stock.sto_birim2_ad = NormalizeText(value, 10, nameof(patch.Unit2Name)), ref changed);
        SetIfPresent(patch.Unit3Name, value => stock.sto_birim3_ad = NormalizeText(value, 10, nameof(patch.Unit3Name)), ref changed);
        SetIfPresent(patch.Unit4Name, value => stock.sto_birim4_ad = NormalizeText(value, 10, nameof(patch.Unit4Name)), ref changed);
        SetIfPresent(patch.RetailTaxPointer, value => stock.sto_perakende_vergi = value, ref changed);
        SetIfPresent(patch.WholesaleTaxPointer, value => stock.sto_toptan_vergi = value, ref changed);
        SetIfPresent(patch.CategoryCode, value => stock.sto_kategori_kodu = NormalizeText(value, 25, nameof(patch.CategoryCode)), ref changed);
        SetIfPresent(patch.MainGroupCode, value => stock.sto_anagrup_kod = NormalizeText(value, 25, nameof(patch.MainGroupCode)), ref changed);
        SetIfPresent(patch.SubGroupCode, value => stock.sto_altgrup_kod = NormalizeText(value, 25, nameof(patch.SubGroupCode)), ref changed);
        SetIfPresent(patch.BrandCode, value => stock.sto_marka_kodu = NormalizeText(value, 25, nameof(patch.BrandCode)), ref changed);
        SetIfPresent(patch.SectorCode, value => stock.sto_sektor_kodu = NormalizeText(value, 25, nameof(patch.SectorCode)), ref changed);
        SetIfPresent(patch.RayonCode, value => stock.sto_reyon_kodu = NormalizeText(value, 25, nameof(patch.RayonCode)), ref changed);
        SetIfPresent(patch.ManufacturerCode, value => stock.sto_uretici_kodu = NormalizeText(value, 25, nameof(patch.ManufacturerCode)), ref changed);
        SetIfPresent(patch.ResponsibilityCode, value => stock.sto_urun_sorkod = NormalizeText(value, 25, nameof(patch.ResponsibilityCode)), ref changed);
        SetIfPresent(patch.ShelfCode, value => stock.sto_yer_kod = NormalizeText(value, 25, nameof(patch.ShelfCode)), ref changed);
        SetIfPresent(patch.SalesStopped, value => stock.sto_satis_dursun = ToByteFlag(value), ref changed);
        SetIfPresent(patch.OrderStopped, value => stock.sto_siparis_dursun = ToByteFlag(value), ref changed);
        SetIfPresent(patch.ReceivingStopped, value => stock.sto_malkabul_dursun = ToByteFlag(value), ref changed);
        SetIfPresent(patch.IsPassive, value => stock.sto_pasif_fl = value, ref changed);
        SetIfPresent(patch.DiscountDisabled, value => stock.sto_iskon_yapilamaz = value, ref changed);

        return changed;
    }

    private static void ApplyStockMovementHeaderPatch(
        STOK_HAREKETLERI row,
        StockMovementHeaderPatchDto patch)
    {
        if (patch.MovementDate.HasValue) row.sth_tarih = patch.MovementDate.Value.Date;
        if (patch.DocumentDate.HasValue) row.sth_belge_tarih = patch.DocumentDate.Value.Date;
        if (patch.GoodsAcceptanceDate.HasValue) row.sth_malkbl_sevk_tarihi = patch.GoodsAcceptanceDate.Value.Date;
        if (patch.DocumentNo is not null) row.sth_belge_no = NormalizeText(patch.DocumentNo, 50, nameof(patch.DocumentNo));
        if (patch.CustomerCode is not null) row.sth_cari_kodu = NormalizeText(patch.CustomerCode, 25, nameof(patch.CustomerCode));
        if (patch.InputWarehouseNo.HasValue) row.sth_giris_depo_no = ValidateNonNegative(patch.InputWarehouseNo.Value, nameof(patch.InputWarehouseNo));
        if (patch.OutputWarehouseNo.HasValue) row.sth_cikis_depo_no = ValidateNonNegative(patch.OutputWarehouseNo.Value, nameof(patch.OutputWarehouseNo));
        if (patch.ShippingWarehouseNo.HasValue) row.sth_nakliyedeposu = ValidateNonNegative(patch.ShippingWarehouseNo.Value, nameof(patch.ShippingWarehouseNo));
        if (patch.Description is not null) row.sth_aciklama = NormalizeText(patch.Description, 50, nameof(patch.Description));
        if (patch.MovementGroupCode1 is not null) row.sth_HareketGrupKodu1 = NormalizeText(patch.MovementGroupCode1, 25, nameof(patch.MovementGroupCode1));
        if (patch.MovementGroupCode2 is not null) row.sth_HareketGrupKodu2 = NormalizeText(patch.MovementGroupCode2, 25, nameof(patch.MovementGroupCode2));
        if (patch.MovementGroupCode3 is not null) row.sth_HareketGrupKodu3 = NormalizeText(patch.MovementGroupCode3, 25, nameof(patch.MovementGroupCode3));
        if (patch.CustomerResponsibilityCenter is not null) row.sth_cari_srm_merkezi = NormalizeText(patch.CustomerResponsibilityCenter, 25, nameof(patch.CustomerResponsibilityCenter));
        if (patch.StockResponsibilityCenter is not null) row.sth_stok_srm_merkezi = NormalizeText(patch.StockResponsibilityCenter, 25, nameof(patch.StockResponsibilityCenter));
        if (patch.ProjectCode is not null) row.sth_proje_kodu = NormalizeText(patch.ProjectCode, 25, nameof(patch.ProjectCode));
    }

    private static bool ApplyStockMovementLinePatch(
        STOK_HAREKETLERI row,
        StockMovementLinePatchDto patch)
    {
        var changed = false;
        SetIfPresent(patch.RowNo, value => row.sth_satirno = ValidateNonNegative(value, nameof(patch.RowNo)), ref changed);
        SetIfPresent(patch.GoodsAcceptanceDate, value => row.sth_malkbl_sevk_tarihi = value.Date, ref changed);
        SetIfPresent(patch.StockCode, value => row.sth_stok_kod = NormalizeText(value, 25, nameof(patch.StockCode)), ref changed);
        SetIfPresent(patch.UnitPointer, value => row.sth_birim_pntr = ValidateUnitPointer(value, nameof(patch.UnitPointer)), ref changed);
        SetIfPresent(patch.Quantity, value => row.sth_miktar = ValidateNonNegative(value, nameof(patch.Quantity)), ref changed);
        SetIfPresent(patch.SecondaryQuantity, value => row.sth_miktar2 = ValidateNonNegative(value, nameof(patch.SecondaryQuantity)), ref changed);
        SetIfPresent(patch.Amount, value => row.sth_tutar = ValidateNonNegative(value, nameof(patch.Amount)), ref changed);
        SetIfPresent(patch.Discount1, value => row.sth_iskonto1 = ValidateNonNegative(value, nameof(patch.Discount1)), ref changed);
        SetIfPresent(patch.Discount2, value => row.sth_iskonto2 = ValidateNonNegative(value, nameof(patch.Discount2)), ref changed);
        SetIfPresent(patch.Discount3, value => row.sth_iskonto3 = ValidateNonNegative(value, nameof(patch.Discount3)), ref changed);
        SetIfPresent(patch.Discount4, value => row.sth_iskonto4 = ValidateNonNegative(value, nameof(patch.Discount4)), ref changed);
        SetIfPresent(patch.Discount5, value => row.sth_iskonto5 = ValidateNonNegative(value, nameof(patch.Discount5)), ref changed);
        SetIfPresent(patch.Discount6, value => row.sth_iskonto6 = ValidateNonNegative(value, nameof(patch.Discount6)), ref changed);
        SetIfPresent(patch.Expense1, value => row.sth_masraf1 = ValidateNonNegative(value, nameof(patch.Expense1)), ref changed);
        SetIfPresent(patch.Expense2, value => row.sth_masraf2 = ValidateNonNegative(value, nameof(patch.Expense2)), ref changed);
        SetIfPresent(patch.Expense3, value => row.sth_masraf3 = ValidateNonNegative(value, nameof(patch.Expense3)), ref changed);
        SetIfPresent(patch.Expense4, value => row.sth_masraf4 = ValidateNonNegative(value, nameof(patch.Expense4)), ref changed);
        SetIfPresent(patch.TaxPointer, value => row.sth_vergi_pntr = value, ref changed);
        SetIfPresent(patch.TaxAmount, value => row.sth_vergi = ValidateNonNegative(value, nameof(patch.TaxAmount)), ref changed);
        SetIfPresent(patch.NetWeight, value => row.sth_netagirlik = ValidateNonNegative(value, nameof(patch.NetWeight)), ref changed);
        SetIfPresent(patch.GrossWeight, value => row.sth_brutagirlik = ValidateNonNegative(value, nameof(patch.GrossWeight)), ref changed);
        SetIfPresent(patch.Description, value => row.sth_aciklama = NormalizeText(value, 50, nameof(patch.Description)), ref changed);
        SetIfPresent(patch.PartyCode, value => row.sth_parti_kodu = NormalizeText(value, 25, nameof(patch.PartyCode)), ref changed);
        SetIfPresent(patch.LotNo, value => row.sth_lot_no = ValidateNonNegative(value, nameof(patch.LotNo)), ref changed);
        SetIfPresent(patch.ProjectCode, value => row.sth_proje_kodu = NormalizeText(value, 25, nameof(patch.ProjectCode)), ref changed);
        SetIfPresent(patch.CustomerResponsibilityCenter, value => row.sth_cari_srm_merkezi = NormalizeText(value, 25, nameof(patch.CustomerResponsibilityCenter)), ref changed);
        SetIfPresent(patch.StockResponsibilityCenter, value => row.sth_stok_srm_merkezi = NormalizeText(value, 25, nameof(patch.StockResponsibilityCenter)), ref changed);
        SetIfPresent(patch.InputWarehouseNo, value => row.sth_giris_depo_no = ValidateNonNegative(value, nameof(patch.InputWarehouseNo)), ref changed);
        SetIfPresent(patch.OutputWarehouseNo, value => row.sth_cikis_depo_no = ValidateNonNegative(value, nameof(patch.OutputWarehouseNo)), ref changed);

        return changed;
    }

    private static void ApplyCustomerMovementHeaderPatch(
        CARI_HESAP_HAREKETLERI row,
        CustomerMovementHeaderPatchDto patch)
    {
        if (patch.MovementDate.HasValue) row.cha_tarihi = patch.MovementDate.Value.Date;
        if (patch.DocumentDate.HasValue) row.cha_belge_tarih = patch.DocumentDate.Value.Date;
        if (patch.DocumentNo is not null) row.cha_belge_no = NormalizeText(patch.DocumentNo, 50, nameof(patch.DocumentNo));
        if (patch.CustomerCode is not null) row.cha_kod = NormalizeText(patch.CustomerCode, 25, nameof(patch.CustomerCode));
        if (patch.TurnoverCustomerCode is not null) row.cha_ciro_cari_kodu = NormalizeText(patch.TurnoverCustomerCode, 25, nameof(patch.TurnoverCustomerCode));
        if (patch.Description is not null) row.cha_aciklama = NormalizeText(patch.Description, 40, nameof(patch.Description));
        if (patch.SellerCode is not null) row.cha_satici_kodu = NormalizeText(patch.SellerCode, 25, nameof(patch.SellerCode));
        if (patch.ProjectCode is not null) row.cha_projekodu = NormalizeText(patch.ProjectCode, 25, nameof(patch.ProjectCode));
        if (patch.ResponsibilityCenter is not null) row.cha_srmrkkodu = NormalizeText(patch.ResponsibilityCenter, 25, nameof(patch.ResponsibilityCenter));
    }

    private static bool ApplyCustomerMovementLinePatch(
        CARI_HESAP_HAREKETLERI row,
        CustomerMovementLinePatchDto patch)
    {
        var changed = false;
        SetIfPresent(patch.RowNo, value => row.cha_satir_no = ValidateNonNegative(value, nameof(patch.RowNo)), ref changed);
        SetIfPresent(patch.CustomerCode, value => row.cha_kod = NormalizeText(value, 25, nameof(patch.CustomerCode)), ref changed);
        SetIfPresent(patch.TurnoverCustomerCode, value => row.cha_ciro_cari_kodu = NormalizeText(value, 25, nameof(patch.TurnoverCustomerCode)), ref changed);
        SetIfPresent(patch.Quantity, value => row.cha_miktari = ValidateNonNegative(value, nameof(patch.Quantity)), ref changed);
        SetIfPresent(patch.Amount, value => row.cha_meblag = ValidateNonNegative(value, nameof(patch.Amount)), ref changed);
        SetIfPresent(patch.SubAmount, value => row.cha_aratoplam = ValidateNonNegative(value, nameof(patch.SubAmount)), ref changed);
        SetIfPresent(patch.DueDay, value => row.cha_vade = ValidateNonNegative(value, nameof(patch.DueDay)), ref changed);
        SetIfPresent(patch.Discount1, value => row.cha_ft_iskonto1 = ValidateNonNegative(value, nameof(patch.Discount1)), ref changed);
        SetIfPresent(patch.Discount2, value => row.cha_ft_iskonto2 = ValidateNonNegative(value, nameof(patch.Discount2)), ref changed);
        SetIfPresent(patch.Discount3, value => row.cha_ft_iskonto3 = ValidateNonNegative(value, nameof(patch.Discount3)), ref changed);
        SetIfPresent(patch.Discount4, value => row.cha_ft_iskonto4 = ValidateNonNegative(value, nameof(patch.Discount4)), ref changed);
        SetIfPresent(patch.Discount5, value => row.cha_ft_iskonto5 = ValidateNonNegative(value, nameof(patch.Discount5)), ref changed);
        SetIfPresent(patch.Discount6, value => row.cha_ft_iskonto6 = ValidateNonNegative(value, nameof(patch.Discount6)), ref changed);
        SetIfPresent(patch.Expense1, value => row.cha_ft_masraf1 = ValidateNonNegative(value, nameof(patch.Expense1)), ref changed);
        SetIfPresent(patch.Expense2, value => row.cha_ft_masraf2 = ValidateNonNegative(value, nameof(patch.Expense2)), ref changed);
        SetIfPresent(patch.Expense3, value => row.cha_ft_masraf3 = ValidateNonNegative(value, nameof(patch.Expense3)), ref changed);
        SetIfPresent(patch.Expense4, value => row.cha_ft_masraf4 = ValidateNonNegative(value, nameof(patch.Expense4)), ref changed);
        SetIfPresent(patch.Tax1, value => row.cha_vergi1 = ValidateNonNegative(value, nameof(patch.Tax1)), ref changed);
        SetIfPresent(patch.Tax2, value => row.cha_vergi2 = ValidateNonNegative(value, nameof(patch.Tax2)), ref changed);
        SetIfPresent(patch.Tax3, value => row.cha_vergi3 = ValidateNonNegative(value, nameof(patch.Tax3)), ref changed);
        SetIfPresent(patch.Tax4, value => row.cha_vergi4 = ValidateNonNegative(value, nameof(patch.Tax4)), ref changed);
        SetIfPresent(patch.Tax5, value => row.cha_vergi5 = ValidateNonNegative(value, nameof(patch.Tax5)), ref changed);
        SetIfPresent(patch.Description, value => row.cha_aciklama = NormalizeText(value, 40, nameof(patch.Description)), ref changed);
        SetIfPresent(patch.SellerCode, value => row.cha_satici_kodu = NormalizeText(value, 25, nameof(patch.SellerCode)), ref changed);
        SetIfPresent(patch.ProjectCode, value => row.cha_projekodu = NormalizeText(value, 25, nameof(patch.ProjectCode)), ref changed);
        SetIfPresent(patch.ResponsibilityCenter, value => row.cha_srmrkkodu = NormalizeText(value, 25, nameof(patch.ResponsibilityCenter)), ref changed);

        return changed;
    }

    private static void EnsureSingleStockMovementDocument(IReadOnlyCollection<STOK_HAREKETLERI> rows)
    {
        var documentCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_evraktip,
                row.sth_cins,
                row.sth_normal_iade
            })
            .Distinct()
            .Count();

        if (documentCount > 1)
        {
            throw new InvalidOperationException(
                "More than one stock movement document matched. Add documentType, movementKind or normalReturn filters.");
        }
    }

    private static void EnsureSingleCustomerMovementDocument(IReadOnlyCollection<CARI_HESAP_HAREKETLERI> rows)
    {
        var documentCount = rows
            .Select(row => new
            {
                row.cha_evrakno_seri,
                row.cha_evrakno_sira,
                row.cha_evrak_tip,
                row.cha_cinsi,
                row.cha_normal_Iade
            })
            .Distinct()
            .Count();

        if (documentCount > 1)
        {
            throw new InvalidOperationException(
                "More than one customer movement document matched. Add documentType, movementKind or normalReturn filters.");
        }
    }

    private static void ValidateStockMovementLookup(StockMovementDocumentLookupRequest request)
    {
        _ = NormalizeRequiredText(request.DocumentSerie, 20, nameof(request.DocumentSerie));
        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }
    }

    private static void ValidateCustomerMovementLookup(CustomerMovementDocumentLookupRequest request)
    {
        _ = NormalizeRequiredText(request.DocumentSerie, 20, nameof(request.DocumentSerie));
        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.CustomerCode is not null)
        {
            _ = NormalizeText(request.CustomerCode, 25, nameof(request.CustomerCode));
        }
    }

    private static void ValidateStockSalesPriceRequest(UpsertStockSalesPriceRequest request)
    {
        ValidateStockSalesPriceKey(
            request.WarehouseNo,
            request.PriceListNo,
            request.PaymentPlanNo,
            request.UnitPointer);

        if (double.IsNaN(request.Price) || double.IsInfinity(request.Price) || request.Price <= 0d)
        {
            throw new ArgumentException("Price must be a finite value greater than zero.", nameof(request.Price));
        }
    }

    private static void ValidateStockSalesPriceKey(
        int warehouseNo,
        int priceListNo,
        int paymentPlanNo,
        byte unitPointer)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        if (priceListNo <= 0)
        {
            throw new ArgumentException("Price list no must be greater than zero.", nameof(priceListNo));
        }

        if (paymentPlanNo < 0)
        {
            throw new ArgumentException("Payment plan no can not be negative.", nameof(paymentPlanNo));
        }

        _ = ValidateUnitPointer(unitPointer, nameof(unitPointer));
    }

    private static void ValidateStockMovementUpdate(UpdateStockMovementDocumentRequest request)
    {
        if (request.Lines is null)
        {
            throw new ArgumentException("Lines collection is required.", nameof(request.Lines));
        }

        var duplicateLine = request.Lines
            .GroupBy(line => line.MovementGuid)
            .FirstOrDefault(group => group.Key == Guid.Empty || group.Count() > 1)
            ?.Key;

        if (duplicateLine is not null)
        {
            throw new ArgumentException("Line movement guid values must be unique and non-empty.", nameof(request.Lines));
        }
    }

    private static void ValidateCustomerMovementUpdate(UpdateCustomerMovementDocumentRequest request)
    {
        if (request.Lines is null)
        {
            throw new ArgumentException("Lines collection is required.", nameof(request.Lines));
        }

        var duplicateLine = request.Lines
            .GroupBy(line => line.MovementGuid)
            .FirstOrDefault(group => group.Key == Guid.Empty || group.Count() > 1)
            ?.Key;

        if (duplicateLine is not null)
        {
            throw new ArgumentException("Line movement guid values must be unique and non-empty.", nameof(request.Lines));
        }
    }

    private static bool HasStockMovementHeaderPatch(StockMovementHeaderPatchDto patch) =>
        patch.MovementDate.HasValue ||
        patch.DocumentDate.HasValue ||
        patch.GoodsAcceptanceDate.HasValue ||
        patch.DocumentNo is not null ||
        patch.CustomerCode is not null ||
        patch.InputWarehouseNo.HasValue ||
        patch.OutputWarehouseNo.HasValue ||
        patch.ShippingWarehouseNo.HasValue ||
        patch.Description is not null ||
        patch.MovementGroupCode1 is not null ||
        patch.MovementGroupCode2 is not null ||
        patch.MovementGroupCode3 is not null ||
        patch.CustomerResponsibilityCenter is not null ||
        patch.StockResponsibilityCenter is not null ||
        patch.ProjectCode is not null;

    private static bool HasCustomerMovementHeaderPatch(CustomerMovementHeaderPatchDto patch) =>
        patch.MovementDate.HasValue ||
        patch.DocumentDate.HasValue ||
        patch.DocumentNo is not null ||
        patch.CustomerCode is not null ||
        patch.TurnoverCustomerCode is not null ||
        patch.Description is not null ||
        patch.SellerCode is not null ||
        patch.ProjectCode is not null ||
        patch.ResponsibilityCenter is not null;

    private static bool HasWarehouseCardPatch(WarehouseCardPatchDto patch) =>
        patch.Name is not null ||
        patch.GroupCode is not null ||
        patch.WarehouseType.HasValue ||
        patch.ShipmentAutoPriceType.HasValue ||
        patch.MovementType.HasValue ||
        patch.AccountingCode is not null ||
        patch.ResponsibilityCenter is not null ||
        patch.ProjectCode is not null ||
        patch.ShipmentAppliedPriceNo.HasValue ||
        patch.LockDate.HasValue ||
        patch.Street is not null ||
        patch.Neighborhood is not null ||
        patch.Avenue is not null ||
        patch.Quarter is not null ||
        patch.ApartmentNo is not null ||
        patch.ApartmentUnitNo is not null ||
        patch.PostalCode is not null ||
        patch.District is not null ||
        patch.City is not null ||
        patch.Country is not null ||
        patch.AddressCode is not null ||
        patch.Latitude.HasValue ||
        patch.Longitude.HasValue ||
        patch.AuthorizedEmail is not null ||
        patch.PhoneCountryCode is not null ||
        patch.PhoneAreaCode is not null ||
        patch.PhoneNo1 is not null ||
        patch.PhoneNo2 is not null ||
        patch.FaxNo is not null ||
        patch.ExcludedFromInventory.HasValue ||
        patch.DetailTrackingType.HasValue ||
        patch.RegionCode is not null ||
        patch.OutgoingEDespatchEnabled.HasValue ||
        patch.IncomingEDespatchEnabled.HasValue ||
        patch.IsPassive.HasValue ||
        patch.IsHidden.HasValue ||
        patch.IsLocked.HasValue;

    private static bool HasCustomerCardPatch(CustomerCardPatchDto patch) =>
        patch.Title1 is not null ||
        patch.Title2 is not null ||
        patch.MovementType.HasValue ||
        patch.ConnectionType.HasValue ||
        patch.PurchaseStockType.HasValue ||
        patch.SalesStockType.HasValue ||
        patch.AccountingCode is not null ||
        patch.AccountingCode1 is not null ||
        patch.AccountingCode2 is not null ||
        patch.CurrencyType.HasValue ||
        patch.CurrencyType1.HasValue ||
        patch.CurrencyType2.HasValue ||
        patch.TaxOffice is not null ||
        patch.TaxOfficeNo is not null ||
        patch.RegistryNo is not null ||
        patch.TaxNo is not null ||
        patch.SalesPriceListNo.HasValue ||
        patch.PaymentType.HasValue ||
        patch.PaymentDay.HasValue ||
        patch.PaymentPlanNo.HasValue ||
        patch.OptionDay.HasValue ||
        patch.InvoiceAddressNo.HasValue ||
        patch.ShippingAddressNo.HasValue ||
        patch.ParentCustomerCode is not null ||
        patch.SectorCode is not null ||
        patch.RegionCode is not null ||
        patch.GroupCode is not null ||
        patch.RepresentativeCode is not null ||
        patch.IsClosed.HasValue ||
        patch.IsLocked.HasValue ||
        patch.EInvoiceEnabled.HasValue ||
        patch.DefaultEInvoiceType.HasValue ||
        patch.EDespatchEnabled.HasValue ||
        patch.DefaultEDespatchType.HasValue ||
        patch.Website is not null ||
        patch.Email is not null ||
        patch.MobilePhone is not null ||
        patch.DefaultInputWarehouseNo.HasValue ||
        patch.DefaultOutputWarehouseNo.HasValue ||
        patch.KepAddress is not null ||
        patch.ReconciliationEmail is not null ||
        patch.MersisNo is not null ||
        patch.TaxOfficeCode is not null ||
        patch.RetailCustomer.HasValue;

    private static STOKLAR? ResolveStock(IReadOnlyDictionary<string, STOKLAR> stocks, string? stockCode) =>
        !string.IsNullOrWhiteSpace(stockCode) && stocks.TryGetValue(stockCode, out var stock)
            ? stock
            : null;

    private static CARI_HESAPLAR? ResolveCustomer(IReadOnlyDictionary<string, CARI_HESAPLAR> customers, string? customerCode) =>
        !string.IsNullOrWhiteSpace(customerCode) && customers.TryGetValue(customerCode, out var customer)
            ? customer
            : null;

    private static string ResolveWarehouseName(IReadOnlyDictionary<int, string> warehouses, int? warehouseNo) =>
        warehouseNo.HasValue && warehouses.TryGetValue(warehouseNo.Value, out var name)
            ? name
            : string.Empty;

    private static byte NormalizeUnitPointer(byte? unitPointer) =>
        unitPointer is >= 1 and <= 4 ? unitPointer.Value : (byte)1;

    private static string ResolveUnitName(byte unitPointer, STOKLAR? stock) =>
        unitPointer switch
        {
            2 => stock?.sto_birim2_ad ?? stock?.sto_birim1_ad ?? string.Empty,
            3 => stock?.sto_birim3_ad ?? stock?.sto_birim1_ad ?? string.Empty,
            4 => stock?.sto_birim4_ad ?? stock?.sto_birim1_ad ?? string.Empty,
            _ => stock?.sto_birim1_ad ?? string.Empty
        };

    private static string BuildCustomerTitle(CARI_HESAPLAR? customer) =>
        customer is null
            ? string.Empty
            : string.Join(
                " ",
                new[] { customer.cari_unvan1, customer.cari_unvan2 }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));

    private static string NormalizeRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return NormalizeText(value, maxLength, parameterName);
    }

    private static string NormalizeText(string value, int maxLength, string parameterName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not be longer than {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static void ValidateUpdateUser(int warehouseNo)
    {
        if (warehouseNo < 0)
        {
            throw new ArgumentException("Current user warehouse no can not be negative.", nameof(warehouseNo));
        }
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

    private static double ValidateLatitude(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < -90d or > 90d)
        {
            throw new ArgumentException("Latitude must be between -90 and 90.", parameterName);
        }

        return value;
    }

    private static double ValidateLongitude(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value is < -180d or > 180d)
        {
            throw new ArgumentException("Longitude must be between -180 and 180.", parameterName);
        }

        return value;
    }

    private static byte ValidateUnitPointer(byte value, string parameterName)
    {
        if (value is < 1 or > 4)
        {
            throw new ArgumentException("Unit pointer must be between 1 and 4.", parameterName);
        }

        return value;
    }

    private static short ResolveMikroUserNo(int warehouseNo) =>
        warehouseNo is > 0 and <= short.MaxValue
            ? Convert.ToInt16(warehouseNo)
            : FallbackMikroUserNo;

    private static byte ToByteFlag(bool value) => value ? (byte)1 : (byte)0;

    private static bool ToBool(byte? value) => value.GetValueOrDefault() != 0;

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
}
