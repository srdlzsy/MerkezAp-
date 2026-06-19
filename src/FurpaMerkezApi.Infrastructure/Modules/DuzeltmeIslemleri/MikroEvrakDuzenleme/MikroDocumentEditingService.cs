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
