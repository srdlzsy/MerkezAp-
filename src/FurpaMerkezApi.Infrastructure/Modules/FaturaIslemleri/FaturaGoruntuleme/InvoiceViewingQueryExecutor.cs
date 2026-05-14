using System.Globalization;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class InvoiceViewingQueryExecutor(
    AuthDbContext authDbContext,
    IClock clock)
{
    public async Task<InvoiceViewingListResponse> ListAsync(
        InvoiceViewingListRequest request,
        CancellationToken cancellationToken)
    {
        ValidateListRequest(request);

        var query = BuildListQuery(request);

        if (request.IsProcessed.HasValue)
        {
            query = query.Where(item => item.IsProcessed == request.IsProcessed.Value);
        }

        if (request.IsPrinted.HasValue)
        {
            query = query.Where(item => item.IsPrinted == request.IsPrinted.Value);
        }

        var entities = await query
            .OrderByDescending(item => item.InvoiceDate ?? item.CreateDate)
            .ThenByDescending(item => item.CreateDate)
            .ThenByDescending(item => item.DocumentId)
            .ToListAsync(cancellationToken);
        var items = entities.Select(MapItem).ToList();

        items = ApplySearch(items, request);

        var totalCount = items.Count;
        var pagedItems = items
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArray();

        return new InvoiceViewingListResponse(
            totalCount,
            request.PageNumber,
            request.PageSize,
            pagedItems);
    }

    private IQueryable<UyumsoftInboxInvoice> BuildListQuery(InvoiceViewingListRequest request)
    {
        var startDate = request.StartDate.Date;
        var endDateExclusive = request.EndDate.Date.AddDays(1);

        return authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(item =>
                (item.InvoiceDate.HasValue &&
                 item.InvoiceDate.Value >= startDate &&
                 item.InvoiceDate.Value < endDateExclusive) ||
                (item.CreateDate.HasValue &&
                 item.CreateDate.Value >= startDate &&
                 item.CreateDate.Value < endDateExclusive));
    }

    public async Task<InvoiceViewingListItemDto> GetByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        var normalizedDocumentId = documentId.Trim();
        var entity = await authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .SingleOrDefaultAsync(invoice => invoice.DocumentId == normalizedDocumentId, cancellationToken);
        var item = entity is null ? null : MapItem(entity);

        return item
               ?? throw new KeyNotFoundException($"Invoice viewing document was not found for documentId {documentId}.");
    }

    public async Task<InvoiceViewingListItemDto> UpdatePrintedStateAsync(
        string documentId,
        bool isPrinted,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        var normalizedDocumentId = documentId.Trim();
        var entity = await authDbContext.UyumsoftInboxInvoices
            .SingleOrDefaultAsync(invoice => invoice.DocumentId == normalizedDocumentId, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"Invoice viewing document was not found for documentId {documentId}.");
        }

        entity.SetPrintedState(isPrinted, clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);

        return MapItem(entity);
    }

    private static InvoiceViewingListItemDto MapItem(UyumsoftInboxInvoice item) =>
        new(
            item.DocumentId,
            item.InvoiceId,
            NormalizeCustomerTitle(item.CustomerTitle),
            item.CustomerTcknVkn,
            item.CreateDate,
            item.InvoiceDate,
            item.InvoiceType,
            item.InvoiceTotal,
            item.DespatchId,
            item.IsProcessed,
            item.IsPrinted,
            item.IsStandard,
            item.StatusCode,
            string.IsNullOrWhiteSpace(item.Status) ? MapStatus(item.StatusCode) : item.Status);

    private static void ValidateListRequest(InvoiceViewingListRequest request)
    {
        if (request.PageNumber <= 0)
        {
            throw new ArgumentException("Page number must be greater than zero.", nameof(request.PageNumber));
        }

        if (request.PageSize <= 0)
        {
            throw new ArgumentException("Page size must be greater than zero.", nameof(request.PageSize));
        }

        if (request.EndDate.Date < request.StartDate.Date)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        if (request.SearchField is null && !string.IsNullOrWhiteSpace(request.SearchText))
        {
            throw new ArgumentException("Search field is required when search text is provided.", nameof(request.SearchField));
        }
    }

    private static List<InvoiceViewingListItemDto> ApplySearch(
        List<InvoiceViewingListItemDto> items,
        InvoiceViewingListRequest request)
    {
        if (request.SearchField is null || string.IsNullOrWhiteSpace(request.SearchText))
        {
            return items;
        }

        var searchText = request.SearchText.Trim();

        return request.SearchField.Value switch
        {
            InvoiceViewingSearchField.InvoiceDate => ApplyInvoiceDateSearch(items, searchText),
            InvoiceViewingSearchField.InvoiceId => items
                .Where(item => ContainsIgnoreCase(item.InvoiceId, searchText))
                .ToList(),
            InvoiceViewingSearchField.CustomerTitle => items
                .Where(item => ContainsIgnoreCase(item.CustomerTitle, searchText))
                .ToList(),
            InvoiceViewingSearchField.CustomerTcknVkn => items
                .Where(item => ContainsIgnoreCase(item.CustomerTcknVkn, searchText))
                .ToList(),
            InvoiceViewingSearchField.InvoiceTotal => ApplyInvoiceTotalSearch(items, searchText),
            InvoiceViewingSearchField.DespatchId => items
                .Where(item => ContainsIgnoreCase(item.DespatchId, searchText))
                .ToList(),
            _ => items
        };
    }

    private static List<InvoiceViewingListItemDto> ApplyInvoiceDateSearch(
        IEnumerable<InvoiceViewingListItemDto> items,
        string searchText)
    {
        if (!TryParseDate(searchText, out var invoiceDate))
        {
            return [];
        }

        return items
            .Where(item =>
            {
                var effectiveDate = item.InvoiceDate ?? item.CreateDate;
                return effectiveDate.HasValue && effectiveDate.Value.Date == invoiceDate.Date;
            })
            .ToList();
    }

    private static List<InvoiceViewingListItemDto> ApplyInvoiceTotalSearch(
        IEnumerable<InvoiceViewingListItemDto> items,
        string searchText)
    {
        if (!TryParseDecimal(searchText, out var invoiceTotal))
        {
            return [];
        }

        return items
            .Where(item => item.InvoiceTotal == invoiceTotal)
            .ToList();
    }

    private static string NormalizeCustomerTitle(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.ToUpper(CultureInfo.GetCultureInfo("tr-TR"));

    private static string MapStatus(string statusCode)
    {
        if (int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStatus))
        {
            return parsedStatus switch
            {
                1000 => "Onaylandi",
                1100 => "Onay Bekliyor",
                1200 => "Reddedildi",
                1300 => "Iade Edildi",
                1400 => "E-Arsiv Iptal",
                2000 => "Hata",
                _ => "Bilinmiyor"
            };
        }

        return string.IsNullOrWhiteSpace(statusCode)
            ? "Bilinmiyor"
            : statusCode;
    }

    private static bool ContainsIgnoreCase(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseDate(string value, out DateTime parsedDate) =>
        DateTime.TryParse(
            value,
            CultureInfo.GetCultureInfo("tr-TR"),
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
            out parsedDate) ||
        DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
            out parsedDate);

    private static bool TryParseDecimal(string value, out decimal parsedDecimal) =>
        decimal.TryParse(
            value,
            NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
            CultureInfo.GetCultureInfo("tr-TR"),
            out parsedDecimal) ||
        decimal.TryParse(
            value,
            NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
            CultureInfo.InvariantCulture,
            out parsedDecimal);
}
