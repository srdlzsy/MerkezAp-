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

        query = ApplySearchQuery(query, request);

        var items = await query
            .OrderByDescending(item => item.InvoiceDate ?? item.CreateDate)
            .ThenByDescending(item => item.CreateDate)
            .ThenByDescending(item => item.DocumentId)
            .Select(item => new InvoiceViewingListItemDto(
                item.DocumentId,
                item.InvoiceId,
                item.CustomerTitle.ToUpper(),
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
                item.Status != string.Empty
                    ? item.Status
                    : item.StatusCode == "1000"
                        ? "Onaylandi"
                        : item.StatusCode == "1100"
                            ? "Onay Bekliyor"
                            : item.StatusCode == "1200"
                                ? "Reddedildi"
                                : item.StatusCode == "1300"
                                    ? "Iade Edildi"
                                    : item.StatusCode == "1400"
                                        ? "E-Arsiv Iptal"
                                        : item.StatusCode == "2000"
                                            ? "Hata"
                                            : item.StatusCode == string.Empty
                                                ? "Bilinmiyor"
                                                : item.StatusCode,
                item.EnvelopeIdentifier,
                item.EnvelopeStatusCode ?? string.Empty,
                item.Message,
                item.TaxTotal,
                item.TaxExclusiveAmount,
                item.DocumentCurrencyCode,
                item.ExchangeRate,
                item.OrderDocumentId,
                item.IsArchived,
                item.InvoiceTipType,
                item.InvoiceTipTypeCode,
                item.IsSeen))
            .ToListAsync(cancellationToken);

        var totalCount = items.Count;

        return new InvoiceViewingListResponse(
            totalCount,
            1,
            totalCount,
            items);
    }

    private static IQueryable<UyumsoftInboxInvoice> ApplySearchQuery(
        IQueryable<UyumsoftInboxInvoice> query,
        InvoiceViewingListRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SearchText))
        {
            return query;
        }

        var searchText = request.SearchText.Trim();
        var pattern = BuildLikePattern(searchText);

        return request.SearchField switch
        {
            InvoiceViewingSearchField.InvoiceDate =>
                TryParseDate(searchText, out var invoiceDate)
                    ? ApplyEffectiveDateFilter(query, invoiceDate)
                    : query.Where(_ => false),
            InvoiceViewingSearchField.InvoiceId =>
                query.Where(item => EF.Functions.Like(item.InvoiceId.ToUpper(), pattern)),
            InvoiceViewingSearchField.DocumentId =>
                query.Where(item => EF.Functions.Like(item.DocumentId.ToUpper(), pattern)),
            InvoiceViewingSearchField.CustomerTitle =>
                query.Where(item => EF.Functions.Like(item.CustomerTitle.ToUpper(), pattern)),
            InvoiceViewingSearchField.CustomerTcknVkn =>
                query.Where(item => EF.Functions.Like(item.CustomerTcknVkn.ToUpper(), pattern)),
            InvoiceViewingSearchField.InvoiceTotal =>
                TryParseDecimal(searchText, out var invoiceTotal)
                    ? query.Where(item => item.InvoiceTotal == invoiceTotal)
                    : query.Where(_ => false),
            InvoiceViewingSearchField.DespatchId =>
                query.Where(item => EF.Functions.Like(item.DespatchId.ToUpper(), pattern)),
            InvoiceViewingSearchField.Status =>
                query.Where(item =>
                    EF.Functions.Like(item.Status.ToUpper(), pattern) ||
                    EF.Functions.Like(item.StatusCode.ToUpper(), pattern) ||
                    (item.EnvelopeStatusCode != null &&
                     EF.Functions.Like(item.EnvelopeStatusCode.ToUpper(), pattern))),
            InvoiceViewingSearchField.InvoiceType =>
                query.Where(item => EF.Functions.Like(item.InvoiceType.ToUpper(), pattern)),
            InvoiceViewingSearchField.EnvelopeIdentifier =>
                query.Where(item => EF.Functions.Like(item.EnvelopeIdentifier.ToUpper(), pattern)),
            InvoiceViewingSearchField.OrderDocumentId =>
                query.Where(item => EF.Functions.Like(item.OrderDocumentId.ToUpper(), pattern)),
            InvoiceViewingSearchField.Message =>
                query.Where(item => EF.Functions.Like(item.Message.ToUpper(), pattern)),
            InvoiceViewingSearchField.Any or null => ApplyAnySearchQuery(query, searchText, pattern),
            _ => ApplyAnySearchQuery(query, searchText, pattern)
        };
    }

    private static IQueryable<UyumsoftInboxInvoice> ApplyAnySearchQuery(
        IQueryable<UyumsoftInboxInvoice> query,
        string searchText,
        string pattern)
    {
        var hasDate = TryParseDate(searchText, out var invoiceDate);
        var dateStart = invoiceDate.Date;
        var dateEndExclusive = dateStart.AddDays(1);
        var hasTotal = TryParseDecimal(searchText, out var invoiceTotal);

        return query.Where(item =>
            EF.Functions.Like(item.InvoiceId.ToUpper(), pattern) ||
            EF.Functions.Like(item.DocumentId.ToUpper(), pattern) ||
            EF.Functions.Like(item.CustomerTitle.ToUpper(), pattern) ||
            EF.Functions.Like(item.CustomerTcknVkn.ToUpper(), pattern) ||
            EF.Functions.Like(item.InvoiceType.ToUpper(), pattern) ||
            EF.Functions.Like(item.DespatchId.ToUpper(), pattern) ||
            EF.Functions.Like(item.OrderDocumentId.ToUpper(), pattern) ||
            EF.Functions.Like(item.Status.ToUpper(), pattern) ||
            EF.Functions.Like(item.StatusCode.ToUpper(), pattern) ||
            EF.Functions.Like(item.EnvelopeIdentifier.ToUpper(), pattern) ||
            (item.EnvelopeStatusCode != null &&
             EF.Functions.Like(item.EnvelopeStatusCode.ToUpper(), pattern)) ||
            EF.Functions.Like(item.Message.ToUpper(), pattern) ||
            (hasDate &&
             (item.InvoiceDate ?? item.CreateDate) >= dateStart &&
             (item.InvoiceDate ?? item.CreateDate) < dateEndExclusive) ||
            (hasTotal && item.InvoiceTotal == invoiceTotal));
    }

    private static IQueryable<UyumsoftInboxInvoice> ApplyEffectiveDateFilter(
        IQueryable<UyumsoftInboxInvoice> query,
        DateTime invoiceDate)
    {
        var dateStart = invoiceDate.Date;
        var dateEndExclusive = dateStart.AddDays(1);

        return query.Where(item =>
            (item.InvoiceDate ?? item.CreateDate) >= dateStart &&
            (item.InvoiceDate ?? item.CreateDate) < dateEndExclusive);
    }

    private IQueryable<UyumsoftInboxInvoice> BuildListQuery(InvoiceViewingListRequest request)
    {
        var startDate = request.StartDate.Date;
        var endDateExclusive = request.EndDate.Date.AddDays(1);

        var query = authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(item =>
                (item.InvoiceDate.HasValue &&
                 item.InvoiceDate.Value >= startDate &&
                 item.InvoiceDate.Value < endDateExclusive) ||
                (!item.InvoiceDate.HasValue &&
                 item.CreateDate.HasValue &&
                 item.CreateDate.Value >= startDate &&
                 item.CreateDate.Value < endDateExclusive));

        query = ApplyStructuredFilters(query, request);

        return query;
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

    public async Task<InvoiceViewingRenderContext> GetRenderContextByLookupIdAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        var normalizedDocumentId = documentId.Trim();
        var entities = await authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .Where(invoice =>
                invoice.DocumentId == normalizedDocumentId ||
                invoice.InvoiceId == normalizedDocumentId ||
                invoice.ServiceDocumentId == normalizedDocumentId ||
                invoice.LocalDocumentId == normalizedDocumentId)
            .Take(5)
            .ToListAsync(cancellationToken);
        var entity = entities
            .OrderByDescending(invoice => string.Equals(invoice.DocumentId, normalizedDocumentId, StringComparison.Ordinal))
            .ThenByDescending(invoice => string.Equals(invoice.InvoiceId, normalizedDocumentId, StringComparison.Ordinal))
            .ThenByDescending(invoice => string.Equals(invoice.ServiceDocumentId, normalizedDocumentId, StringComparison.Ordinal))
            .ThenByDescending(invoice => string.Equals(invoice.LocalDocumentId, normalizedDocumentId, StringComparison.Ordinal))
            .FirstOrDefault();

        return entity is null
            ? throw new KeyNotFoundException($"Invoice viewing document was not found for documentId {documentId}.")
            : new InvoiceViewingRenderContext(
                MapItem(entity),
                BuildLookupCandidates(normalizedDocumentId, entity));
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
            string.IsNullOrWhiteSpace(item.Status) ? MapStatus(item.StatusCode) : item.Status,
            item.EnvelopeIdentifier,
            item.EnvelopeStatusCode ?? string.Empty,
            item.Message,
            item.TaxTotal,
            item.TaxExclusiveAmount,
            item.DocumentCurrencyCode,
            item.ExchangeRate,
            item.OrderDocumentId,
            item.IsArchived,
            item.InvoiceTipType,
            item.InvoiceTipTypeCode,
            item.IsSeen);

    private static IReadOnlyCollection<string> BuildLookupCandidates(
        string requestedDocumentId,
        UyumsoftInboxInvoice item) =>
        new[]
            {
                item.DocumentId,
                item.ServiceDocumentId,
                item.LocalDocumentId,
                item.InvoiceId,
                requestedDocumentId
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void ValidateListRequest(InvoiceViewingListRequest request)
    {
        if (request.PageNumber <= 0)
        {
            throw new ArgumentException("Page number must be greater than zero.", nameof(request.PageNumber));
        }

        if (request.EndDate.Date < request.StartDate.Date)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        if (request.MinInvoiceTotal.HasValue &&
            request.MaxInvoiceTotal.HasValue &&
            request.MaxInvoiceTotal.Value < request.MinInvoiceTotal.Value)
        {
            throw new ArgumentException("Maximum invoice total can not be lower than minimum invoice total.", nameof(request.MaxInvoiceTotal));
        }
    }

    private static IQueryable<UyumsoftInboxInvoice> ApplyStructuredFilters(
        IQueryable<UyumsoftInboxInvoice> query,
        InvoiceViewingListRequest request)
    {
        query = ApplyTextFilter(query, request.InvoiceId, item => item.InvoiceId);
        query = ApplyTextFilter(query, request.DespatchId, item => item.DespatchId);
        query = ApplyTextFilter(query, request.CustomerTitle, item => item.CustomerTitle);
        query = ApplyTextFilter(query, request.CustomerTcknVkn, item => item.CustomerTcknVkn);
        query = ApplyTextFilter(query, request.OrderDocumentId, item => item.OrderDocumentId);
        query = ApplyTextFilter(query, request.InvoiceType, item => item.InvoiceType);

        if (!string.IsNullOrWhiteSpace(request.DocumentId))
        {
            var pattern = BuildLikePattern(request.DocumentId);
            query = query.Where(item =>
                EF.Functions.Like(item.DocumentId.ToUpper(), pattern) ||
                (item.ServiceDocumentId != null && EF.Functions.Like(item.ServiceDocumentId.ToUpper(), pattern)) ||
                (item.LocalDocumentId != null && EF.Functions.Like(item.LocalDocumentId.ToUpper(), pattern)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var pattern = BuildLikePattern(request.Status);
            query = query.Where(item =>
                EF.Functions.Like(item.Status.ToUpper(), pattern) ||
                EF.Functions.Like(item.StatusCode.ToUpper(), pattern) ||
                (item.EnvelopeStatusCode != null && EF.Functions.Like(item.EnvelopeStatusCode.ToUpper(), pattern)));
        }

        if (request.MinInvoiceTotal.HasValue)
        {
            query = query.Where(item => item.InvoiceTotal >= request.MinInvoiceTotal.Value);
        }

        if (request.MaxInvoiceTotal.HasValue)
        {
            query = query.Where(item => item.InvoiceTotal <= request.MaxInvoiceTotal.Value);
        }

        if (request.HasDespatchId.HasValue)
        {
            query = request.HasDespatchId.Value
                ? query.Where(item =>
                    item.DespatchId != string.Empty &&
                    item.DespatchId != "-" &&
                    item.DespatchId != "--" &&
                    item.DespatchId != "---")
                : query.Where(item =>
                    item.DespatchId == string.Empty ||
                    item.DespatchId == "-" ||
                    item.DespatchId == "--" ||
                    item.DespatchId == "---");
        }

        return query;
    }

    private static IQueryable<UyumsoftInboxInvoice> ApplyTextFilter(
        IQueryable<UyumsoftInboxInvoice> query,
        string? value,
        System.Linq.Expressions.Expression<Func<UyumsoftInboxInvoice, string>> propertySelector)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return query;
        }

        var pattern = BuildLikePattern(value);
        return query.Where(item => EF.Functions.Like(EF.Property<string>(item, GetPropertyName(propertySelector)).ToUpper(), pattern));
    }

    private static string GetPropertyName(
        System.Linq.Expressions.Expression<Func<UyumsoftInboxInvoice, string>> propertySelector)
    {
        if (propertySelector.Body is System.Linq.Expressions.MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        throw new ArgumentException("Property selector must point to a string property.", nameof(propertySelector));
    }

    private static string BuildLikePattern(string value) =>
        $"%{value.Trim().ToUpperInvariant()}%";

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

public sealed record InvoiceViewingRenderContext(
    InvoiceViewingListItemDto Summary,
    IReadOnlyCollection<string> LookupIds);
