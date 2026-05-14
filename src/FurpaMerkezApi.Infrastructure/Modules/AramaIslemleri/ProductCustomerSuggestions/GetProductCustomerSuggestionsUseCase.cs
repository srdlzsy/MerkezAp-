using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ProductCustomerSuggestions;

public sealed class GetProductCustomerSuggestionsUseCase(MikroDbContext mikroDbContext)
    : IGetProductCustomerSuggestionsUseCase
{
    private const int DefaultTake = 10;
    private const int MaxTake = 25;
    private const int RecentMovementScanLimit = 500;

    public async Task<ProductCustomerSuggestionResponse> ExecuteAsync(
        ProductCustomerSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var stockCode = NormalizeOrNull(request.StockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(request.StockCode));
        var take = NormalizeTake(request.Take);

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_kod == stockCode)
            .Select(item => new StockInfo(
                item.sto_kod,
                item.sto_isim,
                item.sto_sat_cari_kod))
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            return new ProductCustomerSuggestionResponse(
                false,
                stockCode,
                null,
                null,
                null,
                Array.Empty<ProductCustomerSuggestionDto>());
        }

        var suggestions = new Dictionary<string, SuggestionAccumulator>(StringComparer.OrdinalIgnoreCase);
        var defaultSupplierCode = NormalizeOrNull(stock.DefaultSupplierCode);
        string? defaultSupplierName = null;

        if (defaultSupplierCode is not null)
        {
            var defaultSupplier = await mikroDbContext.CARI_HESAPLARs
                .AsNoTracking()
                .Where(customer => customer.cari_kod == defaultSupplierCode)
                .Select(customer => new CustomerInfo(
                    customer.cari_kod ?? string.Empty,
                    customer.cari_unvan1,
                    customer.cari_VergiKimlikNo))
                .FirstOrDefaultAsync(cancellationToken);

            defaultSupplierName = NormalizeOrNull(defaultSupplier?.CustomerName);

            suggestions[defaultSupplierCode] = new SuggestionAccumulator(
                defaultSupplierCode,
                defaultSupplierName ?? defaultSupplierCode,
                NormalizeOrNull(defaultSupplier?.TaxNoOrTckn),
                true);
            suggestions[defaultSupplierCode].AddSource("varsayilan-tedarikci");
        }

        var movementRows = await (
                from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
                join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
                    on movement.sth_cari_kodu equals customer.cari_kod into customerJoin
                from customer in customerJoin.DefaultIfEmpty()
                where movement.sth_iptal != true &&
                      movement.sth_stok_kod == stockCode &&
                      movement.sth_cari_kodu != null &&
                      movement.sth_cari_kodu != string.Empty
                orderby movement.sth_tarih descending, movement.sth_belge_tarih descending, movement.sth_create_date descending
                select new MovementRow(
                    movement.sth_cari_kodu ?? string.Empty,
                    customer != null ? customer.cari_unvan1 : null,
                    customer != null ? customer.cari_VergiKimlikNo : null,
                    movement.sth_tarih ?? movement.sth_belge_tarih ?? movement.sth_create_date,
                    movement.sth_belge_no))
            .Take(RecentMovementScanLimit)
            .ToListAsync(cancellationToken);

        foreach (var row in movementRows)
        {
            var customerCode = NormalizeOrNull(row.CustomerCode);
            if (customerCode is null)
            {
                continue;
            }

            if (!suggestions.TryGetValue(customerCode, out var accumulator))
            {
                accumulator = new SuggestionAccumulator(
                    customerCode,
                    NormalizeOrNull(row.CustomerName) ?? customerCode,
                    NormalizeOrNull(row.TaxNoOrTckn),
                    false);
                suggestions[customerCode] = accumulator;
            }

            accumulator.AddSource("stok-hareketleri");
            accumulator.RegisterMovement(row.MovementDate, NormalizeOrNull(row.DocumentNo));
        }

        var orderedSuggestions = suggestions.Values
            .OrderByDescending(item => item.IsDefaultSupplier)
            .ThenByDescending(item => item.LastMovementDate)
            .ThenByDescending(item => item.MovementCount)
            .ThenBy(item => item.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(item => new ProductCustomerSuggestionDto(
                item.CustomerCode,
                item.CustomerName,
                item.TaxNoOrTckn,
                item.IsDefaultSupplier,
                item.MovementCount,
                item.LastMovementDate,
                item.LastDocumentNo,
                item.Sources
                    .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new ProductCustomerSuggestionResponse(
            true,
            stock.StockCode,
            NormalizeOrNull(stock.StockName),
            defaultSupplierCode,
            defaultSupplierName,
            orderedSuggestions);
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record StockInfo(
        string StockCode,
        string? StockName,
        string? DefaultSupplierCode);

    private sealed record CustomerInfo(
        string CustomerCode,
        string? CustomerName,
        string? TaxNoOrTckn);

    private sealed record MovementRow(
        string CustomerCode,
        string? CustomerName,
        string? TaxNoOrTckn,
        DateTime MovementDate,
        string? DocumentNo);

    private sealed class SuggestionAccumulator(
        string customerCode,
        string customerName,
        string? taxNoOrTckn,
        bool isDefaultSupplier)
    {
        public string CustomerCode { get; } = customerCode;

        public string CustomerName { get; private set; } = customerName;

        public string? TaxNoOrTckn { get; private set; } = taxNoOrTckn;

        public bool IsDefaultSupplier { get; } = isDefaultSupplier;

        public int MovementCount { get; private set; }

        public DateTime? LastMovementDate { get; private set; }

        public string? LastDocumentNo { get; private set; }

        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddSource(string source) =>
            Sources.Add(source);

        public void RegisterMovement(DateTime movementDate, string? documentNo)
        {
            MovementCount++;

            if (!LastMovementDate.HasValue || movementDate > LastMovementDate.Value)
            {
                LastMovementDate = movementDate;
                LastDocumentNo = documentNo;
            }
        }
    }
}
