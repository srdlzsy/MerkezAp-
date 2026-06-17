using System.Globalization;
using System.Net.Http;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class UyumsoftInboxInvoiceSyncService(
    AuthDbContext authDbContext,
    IUyumsoftConnectedQueryService queryService,
    IClock clock,
    ILogger<UyumsoftInboxInvoiceSyncService> logger)
{
    private const int SyncPageSize = 200;
    private const int MaxSyncPageCount = 250;
    private const int MaxConsecutiveNoChangePages = 2;
    private static readonly IReadOnlyCollection<DateRangeQueryFilterMode> DateRangeQueryFilterModes =
    [
        DateRangeQueryFilterMode.ExecutionDate,
        DateRangeQueryFilterMode.CreateDate
    ];

    public async Task SynchronizeRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (endDate.Date < startDate.Date)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(endDate));
        }

        foreach (var filterMode in DateRangeQueryFilterModes)
        {
            var completed = await SynchronizeDateRangeAsync(
                startDate,
                endDate,
                filterMode,
                cancellationToken);

            if (!completed)
            {
                return;
            }
        }
    }

    public async Task EnsureInvoiceExistsAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document id is required.", nameof(documentId));
        }

        var normalizedDocumentId = documentId.Trim();
        var exists = await authDbContext.UyumsoftInboxInvoices
            .AsNoTracking()
            .AnyAsync(item => item.DocumentId == normalizedDocumentId, cancellationToken);

        if (exists)
        {
            return;
        }

        var items = await FetchByInvoiceIdAsync(normalizedDocumentId, cancellationToken);

        if (items.Count == 0)
        {
            logger.LogWarning(
                "Uyumsoft inbox invoice sync could not resolve invoice for document id {DocumentId}.",
                normalizedDocumentId);
            return;
        }

        var upsertResult = await UpsertAsync(items, cancellationToken);

        if (upsertResult.HasChanges)
        {
            await authDbContext.SaveChangesAsync(cancellationToken);
        }

        authDbContext.ChangeTracker.Clear();
    }

    private async Task<IReadOnlyCollection<ParsedInboxInvoice>> FetchByInvoiceIdAsync(
        string invoiceId,
        CancellationToken cancellationToken)
    {
        var page = await InvokeInboxInvoiceListAsync(
            BuildInvoiceIdPayloadCandidates(invoiceId),
            $"invoice lookup {invoiceId}",
            cancellationToken);

        if (page is null)
        {
            return Array.Empty<ParsedInboxInvoice>();
        }

        return page.Items;
    }

    private async Task<SyncUpsertResult> UpsertAsync(
        IReadOnlyCollection<ParsedInboxInvoice> items,
        CancellationToken cancellationToken)
    {
        var syncTimestampUtc = clock.UtcNow;
        var documentIds = items
            .Select(item => item.DocumentId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var existingItems = await authDbContext.UyumsoftInboxInvoices
            .Where(item => documentIds.Contains(item.DocumentId))
            .ToDictionaryAsync(item => item.DocumentId, cancellationToken);
        var insertedCount = 0;
        var updatedCount = 0;

        foreach (var item in items)
        {
            if (existingItems.TryGetValue(item.DocumentId, out var existing))
            {
                if (!NeedsSynchronization(existing, item))
                {
                    continue;
                }

                existing.ApplySynchronization(
                    item.DocumentId,
                    item.InvoiceId,
                    item.ServiceDocumentId,
                    item.LocalDocumentId,
                    item.CustomerTitle,
                    item.CustomerTcknVkn,
                    item.CreateDate,
                    item.InvoiceDate,
                    item.InvoiceType,
                    item.InvoiceTotal,
                    item.DespatchId,
                    item.IsProcessed,
                    item.IsStandard,
                    item.StatusCode,
                    item.Status,
                    item.EnvelopeStatusCode,
                    syncTimestampUtc);
                updatedCount++;
                continue;
            }

            await authDbContext.UyumsoftInboxInvoices.AddAsync(
                new UyumsoftInboxInvoice(
                    Guid.NewGuid(),
                    item.DocumentId,
                    item.InvoiceId,
                    item.ServiceDocumentId,
                    item.LocalDocumentId,
                    item.CustomerTitle,
                    item.CustomerTcknVkn,
                    item.CreateDate,
                    item.InvoiceDate,
                    item.InvoiceType,
                    item.InvoiceTotal,
                    item.DespatchId,
                    item.IsProcessed,
                    false,
                    item.IsStandard,
                    item.StatusCode,
                    item.Status,
                    item.EnvelopeStatusCode,
                    syncTimestampUtc),
                cancellationToken);
            insertedCount++;
        }

        return new SyncUpsertResult(insertedCount, updatedCount);
    }

    private async Task<ParsedInboxInvoicePage?> InvokeInboxInvoiceListAsync(
        IReadOnlyCollection<InboxInvoiceQueryPayload> payloadCandidates,
        string scenario,
        CancellationToken cancellationToken)
    {
        List<string>? failures = null;
        ParsedInboxInvoicePage? emptyPage = null;

        foreach (var candidate in payloadCandidates)
        {
            try
            {
                var response = await queryService.InvokeGetOperationAsync(
                    UyumsoftConnectedServiceKind.EInvoice,
                    new UyumsoftOperationInvocationRequest(
                        "GetInboxInvoiceList",
                        candidate.Parameters),
                    cancellationToken);

                var page = ParsePage(response);

                if (page.Items.Count > 0 || page.TotalPage > 0)
                {
                    return page;
                }

                emptyPage ??= page;
            }
            catch (InvalidOperationException exception)
            {
                failures ??= [];
                failures.Add($"{candidate.Name}: {exception.Message}");
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(
                    exception,
                    "Uyumsoft GetInboxInvoiceList transport failure for {Scenario} on payload {PayloadName}. Cache fallback will be used.",
                    scenario,
                    candidate.Name);

                return emptyPage;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Uyumsoft GetInboxInvoiceList timed out for {Scenario} on payload {PayloadName}. Cache fallback will be used.",
                    scenario,
                    candidate.Name);

                return emptyPage;
            }
        }

        if (emptyPage is not null)
        {
            return emptyPage;
        }

        if (failures is not null)
        {
            logger.LogWarning(
                "Uyumsoft GetInboxInvoiceList failed for {Scenario}. Attempts: {Attempts}",
                scenario,
                string.Join(" | ", failures));
        }

        return null;
    }

    private async Task<bool> SynchronizeDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        DateRangeQueryFilterMode filterMode,
        CancellationToken cancellationToken)
    {
        var pageIndex = 1;
        int? totalPage = null;
        var seenPageSignatures = new HashSet<string>(StringComparer.Ordinal);
        var consecutiveNoChangePages = 0;
        var filterLabel = filterMode switch
        {
            DateRangeQueryFilterMode.ExecutionDate => "execution-date",
            DateRangeQueryFilterMode.CreateDate => "create-date",
            _ => "unknown"
        };

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pageIndex > MaxSyncPageCount)
            {
                logger.LogWarning(
                    "Uyumsoft inbox invoice sync reached max page limit {MaxSyncPageCount} for {FilterLabel} range {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}. Synchronization stopped to avoid a runaway loop.",
                    MaxSyncPageCount,
                    filterLabel,
                    startDate,
                    endDate);
                break;
            }

            var page = await InvokeInboxInvoiceListAsync(
                BuildDateRangePayloadCandidates(startDate, endDate, pageIndex, SyncPageSize, filterMode),
                $"{filterLabel} range {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}, page {pageIndex}",
                cancellationToken);

            if (page is null)
            {
                return false;
            }

            if (page.Items.Count > 0)
            {
                var pageSignature = BuildPageSignature(page.Items);

                if (!seenPageSignatures.Add(pageSignature))
                {
                    logger.LogWarning(
                        "Uyumsoft inbox invoice sync detected a repeated page for {FilterLabel} range {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}. PageIndex={PageIndex}, ItemCount={ItemCount}, TotalPage={TotalPage}. Synchronization stopped to avoid an infinite loop.",
                        filterLabel,
                        startDate,
                        endDate,
                        pageIndex,
                        page.Items.Count,
                        page.TotalPage);
                    break;
                }

                var upsertResult = await UpsertAsync(page.Items, cancellationToken);

                if (upsertResult.HasChanges)
                {
                    await authDbContext.SaveChangesAsync(cancellationToken);
                    consecutiveNoChangePages = 0;
                }
                else
                {
                    consecutiveNoChangePages++;
                    logger.LogInformation(
                        "Uyumsoft inbox invoice sync found no data changes for {FilterLabel} range {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd} on page {PageIndex}. Consecutive no-change pages: {NoChangePageCount}.",
                        filterLabel,
                        startDate,
                        endDate,
                        pageIndex,
                        consecutiveNoChangePages);
                }

                authDbContext.ChangeTracker.Clear();

                if (consecutiveNoChangePages >= MaxConsecutiveNoChangePages)
                {
                    logger.LogWarning(
                        "Uyumsoft inbox invoice sync stopped after {NoChangePageCount} consecutive no-change pages for {FilterLabel} range {StartDate:yyyy-MM-dd} - {EndDate:yyyy-MM-dd}. This usually means the upstream service is repeating or overlapping pages.",
                        consecutiveNoChangePages,
                        filterLabel,
                        startDate,
                        endDate);
                    break;
                }
            }

            totalPage ??= page.TotalPage > 0 ? page.TotalPage : null;

            if (page.Items.Count == 0)
            {
                break;
            }

            if (totalPage.HasValue && pageIndex >= totalPage.Value)
            {
                break;
            }

            if (page.Items.Count < SyncPageSize)
            {
                break;
            }

            pageIndex++;
        }

        return true;
    }

    private static IReadOnlyCollection<InboxInvoiceQueryPayload> BuildDateRangePayloadCandidates(
        DateTime startDate,
        DateTime endDate,
        int pageNumber,
        int pageSize,
        DateRangeQueryFilterMode filterMode)
    {
        var zeroBasedPageIndex = Math.Max(pageNumber - 1, 0);
        var rangeStart = startDate.Date;
        var rangeEnd = endDate.Date.AddDays(1).AddMilliseconds(-1);

        return
        [
            filterMode switch
            {
                DateRangeQueryFilterMode.ExecutionDate => new InboxInvoiceQueryPayload(
                    "execution-date-ordered",
                    BuildInboxInvoiceListQueryPayload(
                        zeroBasedPageIndex,
                        pageSize,
                        onlyNewestInvoices: false,
                        executionStartDate: rangeStart,
                        executionEndDate: rangeEnd,
                        sortColumn: "ExecutionDate",
                        sortMode: "Descending")),
                DateRangeQueryFilterMode.CreateDate => new InboxInvoiceQueryPayload(
                    "create-date-ordered",
                    BuildInboxInvoiceListQueryPayload(
                        zeroBasedPageIndex,
                        pageSize,
                        onlyNewestInvoices: false,
                        createStartDate: rangeStart,
                        createEndDate: rangeEnd,
                        sortColumn: "CreateDate",
                        sortMode: "Descending")),
                _ => throw new ArgumentOutOfRangeException(nameof(filterMode), filterMode, "Unsupported inbox invoice date filter mode.")
            }
        ];
    }

    private static IReadOnlyCollection<InboxInvoiceQueryPayload> BuildInvoiceIdPayloadCandidates(string invoiceId)
    {
        const int pageSize = 5;
        const int pageIndex = 0;

        return
        [
            new(
                "invoice-ids",
                BuildInboxInvoiceListQueryPayload(
                    pageIndex,
                    pageSize,
                    onlyNewestInvoices: false,
                    invoiceIds: [invoiceId])),
            new(
                "invoice-numbers",
                BuildInboxInvoiceListQueryPayload(
                    pageIndex,
                    pageSize,
                    onlyNewestInvoices: false,
                    invoiceNumbers: [invoiceId]))
        ];
    }

    private static IReadOnlyCollection<UyumsoftOperationParameterRequest> BuildInboxInvoiceListQueryPayload(
        int pageIndex,
        int pageSize,
        bool onlyNewestInvoices,
        DateTime? executionStartDate = null,
        DateTime? executionEndDate = null,
        DateTime? createStartDate = null,
        DateTime? createEndDate = null,
        string? status = null,
        IReadOnlyCollection<string>? invoiceIds = null,
        IReadOnlyCollection<string>? invoiceNumbers = null,
        IReadOnlyCollection<string>? statusInList = null,
        IReadOnlyCollection<string>? statusNotInList = null,
        string? sortColumn = null,
        string? sortMode = null,
        bool? isArchived = null,
        string? targetTitle = null,
        string? targetTcknVkn = null)
    {
        var parameters = new List<UyumsoftOperationParameterRequest>
        {
            Parameter("PageIndex", pageIndex),
            Parameter("PageSize", pageSize),
            Parameter("OnlyNewestInvoices", onlyNewestInvoices)
        };

        AddParameter(parameters, "ExecutionStartDate", executionStartDate);
        AddParameter(parameters, "ExecutionEndDate", executionEndDate);
        AddParameter(parameters, "CreateStartDate", createStartDate);
        AddParameter(parameters, "CreateEndDate", createEndDate);
        AddParameter(parameters, "Status", status);

        if (invoiceIds is not null)
        {
            parameters.AddRange(
                invoiceIds
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Parameter("InvoiceIds", value.Trim())));
        }

        if (invoiceNumbers is not null)
        {
            parameters.AddRange(
                invoiceNumbers
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Parameter("InvoiceNumbers", value.Trim())));
        }

        if (statusInList is not null)
        {
            parameters.AddRange(
                statusInList
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Parameter("StatusInList", value.Trim())));
        }

        if (statusNotInList is not null)
        {
            parameters.AddRange(
                statusNotInList
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Parameter("StatusNotInList", value.Trim())));
        }

        AddParameter(parameters, "SortColumn", sortColumn);
        AddParameter(parameters, "SortMode", sortMode);
        AddParameter(parameters, "IsArchived", isArchived);
        AddParameter(parameters, "TargetTitle", targetTitle);
        AddParameter(parameters, "TargetTcknVkn", targetTcknVkn);

        return parameters;
    }

    private static UyumsoftOperationParameterRequest Parameter(string name, object value) =>
        new(name, Convert.ToString(value, CultureInfo.InvariantCulture));

    private static void AddParameter(
        List<UyumsoftOperationParameterRequest> parameters,
        string name,
        DateTime? value)
    {
        if (value.HasValue)
        {
            parameters.Add(Parameter(name, value.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)));
        }
    }

    private static void AddParameter(
        List<UyumsoftOperationParameterRequest> parameters,
        string name,
        bool? value)
    {
        if (value.HasValue)
        {
            parameters.Add(Parameter(name, value.Value));
        }
    }

    private static void AddParameter(
        List<UyumsoftOperationParameterRequest> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(Parameter(name, value.Trim()));
        }
    }

    private static string BuildPageSignature(IReadOnlyCollection<ParsedInboxInvoice> items)
    {
        var hash = new HashCode();

        foreach (var item in items.OrderBy(entry => entry.DocumentId, StringComparer.Ordinal))
        {
            hash.Add(item.DocumentId, StringComparer.Ordinal);
            hash.Add(item.InvoiceId, StringComparer.Ordinal);
            hash.Add(item.InvoiceDate);
        }

        return $"{items.Count}:{hash.ToHashCode()}";
    }

    private static bool NeedsSynchronization(UyumsoftInboxInvoice existing, ParsedInboxInvoice incoming) =>
        !string.Equals(existing.InvoiceId, NormalizeRequired(incoming.InvoiceId, 150), StringComparison.Ordinal) ||
        !string.Equals(existing.ServiceDocumentId, NormalizeOptional(incoming.ServiceDocumentId, 150), StringComparison.Ordinal) ||
        !string.Equals(existing.LocalDocumentId, NormalizeOptional(incoming.LocalDocumentId, 250), StringComparison.Ordinal) ||
        !string.Equals(existing.CustomerTitle, NormalizeOptional(incoming.CustomerTitle, 255) ?? string.Empty, StringComparison.Ordinal) ||
        !string.Equals(existing.CustomerTcknVkn, NormalizeOptional(incoming.CustomerTcknVkn, 50) ?? string.Empty, StringComparison.Ordinal) ||
        existing.CreateDate != incoming.CreateDate ||
        existing.InvoiceDate != incoming.InvoiceDate ||
        !string.Equals(existing.InvoiceType, NormalizeOptional(incoming.InvoiceType, 80) ?? string.Empty, StringComparison.Ordinal) ||
        existing.InvoiceTotal != incoming.InvoiceTotal ||
        !string.Equals(existing.DespatchId, NormalizeOptional(incoming.DespatchId, 150) ?? string.Empty, StringComparison.Ordinal) ||
        existing.IsProcessed != incoming.IsProcessed ||
        existing.IsStandard != incoming.IsStandard ||
        !string.Equals(existing.StatusCode, NormalizeOptional(incoming.StatusCode, 80) ?? string.Empty, StringComparison.Ordinal) ||
        !string.Equals(existing.Status, NormalizeOptional(incoming.Status, 120) ?? string.Empty, StringComparison.Ordinal) ||
        !string.Equals(existing.EnvelopeStatusCode, NormalizeOptional(incoming.EnvelopeStatusCode, 80), StringComparison.Ordinal);

    private static string NormalizeRequired(string value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static ParsedInboxInvoicePage ParsePage(UyumsoftOperationResponseDto response)
    {
        var valueNode = FindFirstNode(response.Nodes, "Value");
        var totalPage = valueNode is null
            ? 0
            : ReadInt(valueNode, "TotalPage", "TotalPages", "PageCount");
        var itemNodes = ExtractItemNodes(response.Nodes);
        var items = new List<ParsedInboxInvoice>(itemNodes.Count);

        foreach (var itemNode in itemNodes)
        {
            if (!TryParseItem(itemNode, out var item))
            {
                continue;
            }

            items.Add(item);
        }

        return new ParsedInboxInvoicePage(totalPage, items);
    }

    private static IReadOnlyCollection<UyumsoftResponseNodeDto> ExtractItemNodes(
        IReadOnlyCollection<UyumsoftResponseNodeDto> nodes)
    {
        var itemsNode = FindFirstNode(nodes, "Items");

        if (itemsNode is not null)
        {
            var directChildren = itemsNode.Children
                .Where(IsInvoiceListItemNode)
                .ToArray();

            if (directChildren.Length > 0)
            {
                return directChildren;
            }
        }

        return EnumerateNodes(nodes)
            .Where(IsInvoiceListItemNode)
            .ToArray();
    }

    private static bool TryParseItem(
        UyumsoftResponseNodeDto itemNode,
        out ParsedInboxInvoice item)
    {
        item = default!;

        var rawInvoiceId = ReadString(itemNode, "InvoiceId");
        var rawDocumentId = ReadString(itemNode, "DocumentId");
        var localDocumentId = ReadString(itemNode, "LocalDocumentId");
        var documentId = ResolveLookupDocumentId(rawInvoiceId, rawDocumentId, localDocumentId);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return false;
        }

        var displayInvoiceId = ResolveDisplayInvoiceId(rawInvoiceId, rawDocumentId, localDocumentId, documentId);
        var statusCode = ReadString(itemNode, "Status");
        var status = ResolveStatusText(
            ReadString(
                itemNode,
                "StatusText",
                "StatusName",
                "StatusDisplayName"),
            statusCode);

        var isNew = TryReadBool(itemNode, out var parsedIsNew, "IsNew")
            ? parsedIsNew
            : (bool?)null;

        item = new ParsedInboxInvoice(
            documentId,
            string.IsNullOrWhiteSpace(displayInvoiceId) ? documentId : displayInvoiceId,
            NormalizeOptional(rawDocumentId),
            NormalizeOptional(localDocumentId),
            ReadString(itemNode, "TargetTitle", "CustomerTitle", "SenderTitle", "Title"),
            ReadString(itemNode, "TargetTcknVkn", "CustomerTcknVkn", "SenderTcknVkn", "VknTckn"),
            ReadDateTime(itemNode, "CreateDateUtc", "CreateDate"),
            ReadDateTime(itemNode, "InvoiceDate", "DocumentDate", "ExecutionDate", "Date"),
            ReadString(itemNode, "Type", "InvoiceType", "DocumentType"),
            ReadDecimal(itemNode, "PayableAmount", "InvoiceTotal", "TotalAmount"),
            ReadString(itemNode, "DespatchId", "DespatchDocumentId", "DespatchNumber"),
            isNew.HasValue ? !isNew.Value : false,
            ReadBool(itemNode, "IsStandart", "IsStandard"),
            statusCode,
            status,
            ReadString(itemNode, "EnvelopeStatus"));

        return true;
    }

    private static string ResolveLookupDocumentId(
        string rawInvoiceId,
        string rawDocumentId,
        string localDocumentId)
    {
        foreach (var candidate in new[] { rawInvoiceId, rawDocumentId, localDocumentId })
        {
            if (LooksLikeLookupId(candidate))
            {
                return candidate.Trim();
            }
        }

        foreach (var candidate in new[] { rawInvoiceId, rawDocumentId, localDocumentId })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return string.Empty;
    }

    private static string ResolveDisplayInvoiceId(
        string rawInvoiceId,
        string rawDocumentId,
        string localDocumentId,
        string lookupDocumentId)
    {
        foreach (var candidate in new[] { rawDocumentId, localDocumentId, rawInvoiceId })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (string.Equals(candidate.Trim(), lookupDocumentId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!LooksLikeLookupId(candidate))
            {
                return candidate.Trim();
            }
        }

        foreach (var candidate in new[] { rawDocumentId, localDocumentId, rawInvoiceId })
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                !string.Equals(candidate.Trim(), lookupDocumentId, StringComparison.Ordinal))
            {
                return candidate.Trim();
            }
        }

        return lookupDocumentId;
    }

    private static bool LooksLikeLookupId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (Guid.TryParse(value.Trim(), out _) || value.Trim().Length > 30);

    private static UyumsoftResponseNodeDto? FindFirstNode(
        IEnumerable<UyumsoftResponseNodeDto> nodes,
        string name)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var childMatch = FindFirstNode(node.Children, name);

            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static IEnumerable<UyumsoftResponseNodeDto> EnumerateNodes(
        IEnumerable<UyumsoftResponseNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in EnumerateNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private static bool IsInvoiceListItemNode(UyumsoftResponseNodeDto node)
    {
        if (node.Children.Count == 0)
        {
            return false;
        }

        if (node.Name.Contains("InvoiceListItem", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var childNames = node.Children
            .Select(child => child.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return childNames.Contains("InvoiceId") ||
               childNames.Contains("DocumentId") ||
               childNames.Contains("TargetTitle") ||
               childNames.Contains("PayableAmount");
    }

    private static int ReadInt(UyumsoftResponseNodeDto node, params string[] fieldNames)
    {
        var value = ReadString(node, fieldNames);

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static DateTime? ReadDateTime(UyumsoftResponseNodeDto node, params string[] fieldNames)
    {
        var value = ReadString(node, fieldNames);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var invariantDate))
        {
            return invariantDate;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AllowWhiteSpaces,
                out var trDate))
        {
            return trDate;
        }

        return null;
    }

    private static decimal ReadDecimal(UyumsoftResponseNodeDto node, params string[] fieldNames)
    {
        var value = ReadString(node, fieldNames);

        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        if (decimal.TryParse(
                value,
                NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture,
                out var invariantDecimal))
        {
            return invariantDecimal;
        }

        return decimal.TryParse(
            value,
            NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
            CultureInfo.GetCultureInfo("tr-TR"),
            out var trDecimal)
            ? trDecimal
            : 0m;
    }

    private static bool TryReadBool(
        UyumsoftResponseNodeDto node,
        out bool value,
        params string[] fieldNames)
    {
        value = false;
        var raw = ReadString(node, fieldNames);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out var parsedBool))
        {
            value = parsedBool;
            return true;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            value = parsedInt != 0;
            return true;
        }

        return false;
    }

    private static bool ReadBool(UyumsoftResponseNodeDto node, params string[] fieldNames) =>
        TryReadBool(node, out var parsed, fieldNames) && parsed;

    private static string ReadString(UyumsoftResponseNodeDto node, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var child = node.Children.FirstOrDefault(item =>
                string.Equals(item.Name, fieldName, StringComparison.OrdinalIgnoreCase));

            if (child is not null && !string.IsNullOrWhiteSpace(child.Value))
            {
                return child.Value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string ResolveStatusText(string rawStatus, string statusCode)
    {
        if (!string.IsNullOrWhiteSpace(rawStatus))
        {
            return rawStatus.Trim();
        }

        return int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStatus)
            ? parsedStatus switch
            {
                1000 => "Onaylandi",
                1100 => "Onay Bekliyor",
                1200 => "Reddedildi",
                1300 => "Iade Edildi",
                1400 => "E-Arsiv Iptal",
                2000 => "Hata",
                _ => "Bilinmiyor"
            }
            : string.IsNullOrWhiteSpace(statusCode)
                ? "Bilinmiyor"
                : statusCode;
    }

    private sealed record ParsedInboxInvoicePage(
        int TotalPage,
        IReadOnlyCollection<ParsedInboxInvoice> Items);

    private sealed record ParsedInboxInvoice(
        string DocumentId,
        string InvoiceId,
        string? ServiceDocumentId,
        string? LocalDocumentId,
        string CustomerTitle,
        string CustomerTcknVkn,
        DateTime? CreateDate,
        DateTime? InvoiceDate,
        string InvoiceType,
        decimal InvoiceTotal,
        string DespatchId,
        bool IsProcessed,
        bool IsStandard,
        string StatusCode,
        string Status,
        string? EnvelopeStatusCode);

    private sealed record SyncUpsertResult(
        int InsertedCount,
        int UpdatedCount)
    {
        public bool HasChanges => InsertedCount > 0 || UpdatedCount > 0;
    }

    private sealed record InboxInvoiceQueryPayload(
        string Name,
        IReadOnlyCollection<UyumsoftOperationParameterRequest> Parameters);

    private enum DateRangeQueryFilterMode
    {
        ExecutionDate,
        CreateDate
    }

}
