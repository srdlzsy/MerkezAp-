using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UyumsoftInvoice = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Invoice;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class UyumsoftInboxInvoiceSyncService(
    AuthDbContext authDbContext,
    IOptions<UyumsoftConnectedServicesOptions> uyumsoftOptions,
    IClock clock,
    IConfiguration configuration,
    InvoiceViewingSynchronizationProgressStore synchronizationProgressStore,
    ILogger<UyumsoftInboxInvoiceSyncService> logger)
{
    private const int SyncPageSize = 20;
    private const int DefaultExecutionLookAheadDays = 15;
    private const int MaxExecutionLookAheadDays = 60;
    private static readonly Regex InvoiceOpenTagRegex = new(
        @"<(?:(?<prefix>[A-Za-z_][\w.-]*):)?Invoice(?:\s|>|/)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly IReadOnlyCollection<DateRangeQueryFilterMode> DateRangeQueryFilterModes =
    [
        DateRangeQueryFilterMode.ExecutionDate
    ];

    public async Task<InvoiceViewingSynchronizationResponse> SynchronizeRangeAsync(
        DateTime startDate,
        DateTime endDate,
        bool includeStatuses,
        CancellationToken cancellationToken)
    {
        if (endDate.Date < startDate.Date)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(endDate));
        }

        var fetchedCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var matchedCount = 0;
        var sourceTotalCount = 0;

        try
        {
            foreach (var filterMode in DateRangeQueryFilterModes)
            {
                var result = await SynchronizeDateRangeAsync(
                    startDate,
                    endDate,
                    filterMode,
                    includeStatuses,
                    cancellationToken);

                fetchedCount += result.FetchedCount;
                matchedCount += result.MatchedCount;
                insertedCount += result.InsertedCount;
                updatedCount += result.UpdatedCount;
                sourceTotalCount = Math.Max(sourceTotalCount, result.SourceTotalCount);
            }

            var response = new InvoiceViewingSynchronizationResponse(
                startDate.Date,
                endDate.Date,
                includeStatuses,
                sourceTotalCount,
                fetchedCount,
                matchedCount,
                insertedCount,
                updatedCount);

            synchronizationProgressStore.Complete(
                response.SourceTotalCount,
                response.FetchedCount,
                response.MatchedCount,
                response.InsertedCount,
                response.UpdatedCount);

            return response;
        }
        catch (Exception exception)
        {
            synchronizationProgressStore.Fail(exception.Message);
            throw;
        }
    }

    public InvoiceViewingSynchronizationProgressResponse GetProgress() =>
        synchronizationProgressStore.Get();


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

        var upsertResult = await UpsertAsync(
            items,
            updateStatuses: true,
            cancellationToken);

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
        var page = await InvokeInboxInvoicesAsync(
            BuildInvoiceIdPayloadCandidates(invoiceId),
            $"invoice lookup {invoiceId}",
            includeStatuses: true,
            cancellationToken);

        if (page is null)
        {
            return Array.Empty<ParsedInboxInvoice>();
        }

        return page.Items;
    }

    private async Task<SyncUpsertResult> UpsertAsync(
        IReadOnlyCollection<ParsedInboxInvoice> items,
        bool updateStatuses,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return new SyncUpsertResult(0, 0);
        }

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
                var itemToApply = updateStatuses
                    ? item
                    : KeepExistingStatusValues(existing, item);

                if (!NeedsSynchronization(existing, itemToApply))
                {
                    continue;
                }

                existing.ApplySynchronization(
                    itemToApply.DocumentId,
                    itemToApply.InvoiceId,
                    itemToApply.ServiceDocumentId,
                    itemToApply.LocalDocumentId,
                    itemToApply.CustomerTitle,
                    itemToApply.CustomerTcknVkn,
                    itemToApply.CreateDate,
                    itemToApply.InvoiceDate,
                    itemToApply.InvoiceType,
                    itemToApply.InvoiceTotal,
                    itemToApply.DespatchId,
                    itemToApply.IsProcessed,
                    itemToApply.IsStandard,
                    itemToApply.StatusCode,
                    itemToApply.Status,
                    itemToApply.EnvelopeStatusCode,
                    itemToApply.EnvelopeIdentifier,
                    itemToApply.Message,
                    itemToApply.TaxTotal,
                    itemToApply.TaxExclusiveAmount,
                    itemToApply.DocumentCurrencyCode,
                    itemToApply.ExchangeRate,
                    itemToApply.OrderDocumentId,
                    itemToApply.IsArchived,
                    itemToApply.InvoiceTipType,
                    itemToApply.InvoiceTipTypeCode,
                    itemToApply.IsSeen,
                    syncTimestampUtc);
                updatedCount++;
                continue;
            }

            authDbContext.UyumsoftInboxInvoices.Add(
                new(
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
                    item.EnvelopeIdentifier,
                    item.Message,
                    item.TaxTotal,
                    item.TaxExclusiveAmount,
                    item.DocumentCurrencyCode,
                    item.ExchangeRate,
                    item.OrderDocumentId,
                    item.IsArchived,
                    item.InvoiceTipType,
                    item.InvoiceTipTypeCode,
                    item.IsSeen,
                    syncTimestampUtc));
            insertedCount++;
        }

        return new SyncUpsertResult(insertedCount, updatedCount);
    }

    private async Task<ParsedInboxInvoicePage?> InvokeInboxInvoicesAsync(
        IReadOnlyCollection<InboxInvoiceQueryPayload> payloadCandidates,
        string scenario,
        bool includeStatuses,
        CancellationToken cancellationToken)
    {
        var config = ResolveEInvoiceOptions();
        var client = UyumsoftWcfClientHelper.CreateInvoiceClient(config);

        try
        {
            return await InvokeInboxInvoicesAsync(
                client,
                UyumsoftWcfClientHelper.CreateInvoiceUserInfo(config),
                payloadCandidates,
                scenario,
                includeStatuses,
                cancellationToken);
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private async Task<ParsedInboxInvoicePage?> InvokeInboxInvoicesAsync(
        UyumsoftInvoice.BasicIntegrationClient client,
        UyumsoftInvoice.UserInformation userInfo,
        IReadOnlyCollection<InboxInvoiceQueryPayload> payloadCandidates,
        string scenario,
        bool includeStatuses,
        CancellationToken cancellationToken)
    {
        List<string>? failures = null;
        ParsedInboxInvoicePage? emptyPage = null;

        foreach (var candidate in payloadCandidates)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await client.GetInboxInvoicesAsync(userInfo, candidate.Query)
                    .WaitAsync(cancellationToken);

                if (!response.IsSucceded)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(response.Message)
                            ? "Uyumsoft GetInboxInvoices request was rejected."
                            : response.Message);
                }

                var page = ParseInvoiceInfoPage(response);

                if (includeStatuses && page.Items.Count > 0)
                {
                    page = page with
                    {
                        Items = await EnrichWithStatusesAsync(
                            client,
                            userInfo,
                            page.Items,
                            cancellationToken)
                    };
                }

                stopwatch.Stop();
                logger.LogInformation(
                    "Uyumsoft GetInboxInvoices completed for {Scenario} on payload {PayloadName}. PageIndex={PageIndex}, PageSize={PageSize}, Items={ItemCount}, TotalCount={TotalCount}, TotalPage={TotalPage}, IncludeStatuses={IncludeStatuses}, ElapsedMs={ElapsedMs}.",
                    scenario,
                    candidate.Name,
                    candidate.Query.PageIndex,
                    candidate.Query.PageSize,
                    page.Items.Count,
                    page.TotalCount,
                    page.TotalPage,
                    includeStatuses,
                    stopwatch.ElapsedMilliseconds);

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
                UyumsoftWcfClientHelper.Abort(client);
                logger.LogWarning(
                    exception,
                    "Uyumsoft GetInboxInvoices transport failure for {Scenario} on payload {PayloadName}. Cache fallback will be used.",
                    scenario,
                    candidate.Name);

                if (emptyPage is not null)
                {
                    return emptyPage;
                }

                throw new InvalidOperationException(
                    $"Uyumsoft GetInboxInvoices transport failure for {scenario}: {exception.Message}",
                    exception);
            }
            catch (CommunicationException exception)
            {
                UyumsoftWcfClientHelper.Abort(client);
                logger.LogWarning(
                    exception,
                    "Uyumsoft GetInboxInvoices communication failure for {Scenario} on payload {PayloadName}. Cache fallback will be used.",
                    scenario,
                    candidate.Name);

                if (emptyPage is not null)
                {
                    return emptyPage;
                }

                throw new InvalidOperationException(
                    $"Uyumsoft GetInboxInvoices communication failure for {scenario}: {exception.Message}",
                    exception);
            }
            catch (TimeoutException exception)
            {
                stopwatch.Stop();
                UyumsoftWcfClientHelper.Abort(client);
                logger.LogWarning(
                    exception,
                    "Uyumsoft GetInboxInvoices timeout for {Scenario} on payload {PayloadName}. PageIndex={PageIndex}, PageSize={PageSize}, IncludeStatuses={IncludeStatuses}, ElapsedMs={ElapsedMs}, SendTimeoutSeconds={SendTimeoutSeconds}.",
                    scenario,
                    candidate.Name,
                    candidate.Query.PageIndex,
                    candidate.Query.PageSize,
                    includeStatuses,
                    stopwatch.ElapsedMilliseconds,
                    client.Endpoint.Binding.SendTimeout.TotalSeconds);

                throw new TimeoutException(
                    "Uyumsoft zaman asimi: senkronizasyon sirasinda Uyumsoft yaniti beklenen surede donmedi. Daha kucuk tarih araligi deneyin.",
                    exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                UyumsoftWcfClientHelper.Abort(client);
                logger.LogWarning(
                    exception,
                    "Uyumsoft GetInboxInvoices timed out for {Scenario} on payload {PayloadName}. Cache fallback will be used.",
                    scenario,
                    candidate.Name);

                if (emptyPage is not null)
                {
                    return emptyPage;
                }

                throw new TimeoutException(
                    "Uyumsoft zaman asimi: senkronizasyon sirasinda Uyumsoft yaniti beklenen surede donmedi. Daha kucuk tarih araligi deneyin.",
                    exception);
            }
        }

        if (emptyPage is not null)
        {
            return emptyPage;
        }

        if (failures is not null)
        {
            var failureMessage =
                $"Uyumsoft GetInboxInvoices failed for {scenario}. Attempts: {string.Join(" | ", failures)}";

            logger.LogWarning(
                "{FailureMessage}",
                failureMessage);

            throw new InvalidOperationException(failureMessage);
        }

        return null;
    }

    private static async Task<IReadOnlyCollection<ParsedInboxInvoice>> EnrichWithStatusesAsync(
        UyumsoftInvoice.BasicIntegrationClient client,
        UyumsoftInvoice.UserInformation userInfo,
        IReadOnlyCollection<ParsedInboxInvoice> items,
        CancellationToken cancellationToken)
    {
        var invoiceIds = items
            .Select(item => item.DocumentId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var response = await client.GetInboxInvoiceStatusWithLogsAsync(userInfo, invoiceIds)
            .WaitAsync(cancellationToken);

        if (!response.IsSucceded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? "Uyumsoft GetInboxInvoiceStatusWithLogs request was rejected."
                    : response.Message);
        }

        var statusesByInvoiceId = (response.Value ?? [])
            .Where(status => !string.IsNullOrWhiteSpace(status.InvoiceId))
            .GroupBy(status => status.InvoiceId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var enrichedItems = new List<ParsedInboxInvoice>(items.Count);
        var missingInvoiceIds = new List<string>();

        foreach (var item in items)
        {
            var status = FindStatus(statusesByInvoiceId, item);

            if (status is null)
            {
                missingInvoiceIds.Add(item.DocumentId);
                continue;
            }

            enrichedItems.Add(item with
            {
                StatusCode = status.StatusCode.ToString(CultureInfo.InvariantCulture),
                Status = MapInvoiceStatus(status.Status),
                EnvelopeStatusCode = status.EnvelopeStatusCode.ToString(CultureInfo.InvariantCulture),
                Message = NormalizeOptional(status.Message) ?? item.Message
            });
        }

        if (missingInvoiceIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Uyumsoft did not return status information for {missingInvoiceIds.Count} invoice(s): " +
                string.Join(", ", missingInvoiceIds));
        }

        return enrichedItems;
    }

    private static UyumsoftInvoice.InvoiceStatusWithLogInfo? FindStatus(
        IReadOnlyDictionary<string, UyumsoftInvoice.InvoiceStatusWithLogInfo> statusesByInvoiceId,
        ParsedInboxInvoice item)
    {
        foreach (var candidate in new[]
                 {
                     item.DocumentId,
                     item.ServiceDocumentId,
                     item.InvoiceId,
                     item.LocalDocumentId
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                statusesByInvoiceId.TryGetValue(candidate.Trim(), out var status))
            {
                return status;
            }
        }

        return null;
    }

    private static string MapInvoiceStatus(UyumsoftInvoice.InvoiceStatus status) =>
        status switch
        {
            UyumsoftInvoice.InvoiceStatus.NotPrepared => "Hazirlanmadi",
            UyumsoftInvoice.InvoiceStatus.NotSend => "Gonderilmedi",
            UyumsoftInvoice.InvoiceStatus.Draft => "Taslak",
            UyumsoftInvoice.InvoiceStatus.Canceled => "Iptal Edildi",
            UyumsoftInvoice.InvoiceStatus.Queued => "Kuyrukta",
            UyumsoftInvoice.InvoiceStatus.Processing => "Isleniyor",
            UyumsoftInvoice.InvoiceStatus.SentToGib => "GIB'e Gonderildi",
            UyumsoftInvoice.InvoiceStatus.Approved => "Onaylandi",
            UyumsoftInvoice.InvoiceStatus.WaitingForAprovement => "Onay Bekliyor",
            UyumsoftInvoice.InvoiceStatus.Declined => "Reddedildi",
            UyumsoftInvoice.InvoiceStatus.Return => "Iade Edildi",
            UyumsoftInvoice.InvoiceStatus.EArchivedCanceled => "E-Arsiv Iptal",
            UyumsoftInvoice.InvoiceStatus.Error => "Hata",
            _ => status.ToString()
        };

    private async Task<SyncRunResult> SynchronizeDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        DateRangeQueryFilterMode filterMode,
        bool includeStatuses,
        CancellationToken cancellationToken)
    {
        var pageIndex = 1;
        int? totalPage = null;
        var fetchedCount = 0;
        var sourceTotalCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var matchedCount = 0;
        var processedDocumentIds = new HashSet<string>(StringComparer.Ordinal);
        var seenPageSignatures = new HashSet<string>(StringComparer.Ordinal);
        var invoiceDateStart = startDate.Date;
        var invoiceDateEndExclusive = endDate.Date.AddDays(1);
        var queryStartDate = startDate.Date;
        var queryEndDate = ResolveExecutionQueryEndDate(endDate);
        var filterLabel = filterMode switch
        {
            DateRangeQueryFilterMode.ExecutionDate => "execution-date",
            DateRangeQueryFilterMode.CreateDate => "create-date",
            _ => "unknown"
        };
        var config = ResolveEInvoiceOptions();
        var client = UyumsoftWcfClientHelper.CreateInvoiceClient(config);
        var userInfo = UyumsoftWcfClientHelper.CreateInvoiceUserInfo(config);

        synchronizationProgressStore.Start(
            startDate,
            endDate,
            includeStatuses,
            queryStartDate,
            queryEndDate,
            SyncPageSize);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = await InvokeInboxInvoicesAsync(
                    client,
                    userInfo,
                    BuildDateRangePayloadCandidates(queryStartDate, queryEndDate, pageIndex, SyncPageSize, filterMode),
                    $"{filterLabel} invoice range {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}, " +
                    $"query range {queryStartDate:yyyy-MM-dd HH:mm:ss} - {queryEndDate:yyyy-MM-dd HH:mm:ss}, page {pageIndex}",
                    includeStatuses,
                    cancellationToken);

                if (page is null)
                {
                    throw new InvalidOperationException(
                        $"Uyumsoft GetInboxInvoices failed for {filterLabel} range " +
                        $"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}.");
                }

                sourceTotalCount = Math.Max(sourceTotalCount, page.TotalCount);
                var pageMatchedCount = 0;
                var pageInsertedCount = 0;
                var pageUpdatedCount = 0;

                if (page.Items.Count > 0)
                {
                    var pageSignature = BuildPageSignature(page.Items);

                    if (!seenPageSignatures.Add(pageSignature))
                    {
                        throw new InvalidOperationException(
                            $"Uyumsoft GetInboxInvoices repeated page {pageIndex} for {filterLabel} range " +
                            $"{startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}.");
                    }

                    fetchedCount += page.Items.Count;

                    var uniquePageItems = page.Items
                        .Where(item =>
                            item.InvoiceDate.HasValue &&
                            item.InvoiceDate.Value >= invoiceDateStart &&
                            item.InvoiceDate.Value < invoiceDateEndExclusive)
                        .GroupBy(item => item.DocumentId, StringComparer.Ordinal)
                        .Select(group => group.Last())
                        .Where(item => processedDocumentIds.Add(item.DocumentId))
                        .ToArray();
                    var upsertResult = await UpsertAsync(
                        uniquePageItems,
                        includeStatuses,
                        cancellationToken);

                    if (upsertResult.HasChanges)
                    {
                        await authDbContext.SaveChangesAsync(cancellationToken);
                    }

                    authDbContext.ChangeTracker.Clear();

                    pageMatchedCount = uniquePageItems.Length;
                    pageInsertedCount = upsertResult.InsertedCount;
                    pageUpdatedCount = upsertResult.UpdatedCount;

                    matchedCount += pageMatchedCount;
                    insertedCount += pageInsertedCount;
                    updatedCount += pageUpdatedCount;

                    logger.LogInformation(
                        "Uyumsoft inbox invoice sync page persisted for {FilterLabel} invoice range {StartDate} - {EndDate}, query range {QueryStartDate} - {QueryEndDate}. PageIndex={PageIndex}, PageSize={PageSize}, Items={ItemCount}, MatchedItems={MatchedItemCount}, MatchedTotal={MatchedTotal}, TotalCount={TotalCount}, TotalPage={TotalPage}, Inserted={InsertedCount}, Updated={UpdatedCount}.",
                        filterLabel,
                        startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        queryStartDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        queryEndDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        pageIndex - 1,
                        SyncPageSize,
                        page.Items.Count,
                        pageMatchedCount,
                        matchedCount,
                        page.TotalCount,
                        page.TotalPage,
                        pageInsertedCount,
                        pageUpdatedCount);
                }

                totalPage ??= page.TotalPage > 0 ? page.TotalPage : null;

                synchronizationProgressStore.ReportPage(
                    pageIndex - 1,
                    pageIndex,
                    SyncPageSize,
                    page.TotalCount,
                    page.TotalPage,
                    fetchedCount,
                    matchedCount,
                    insertedCount,
                    updatedCount,
                    page.Items.Count,
                    pageMatchedCount,
                    pageInsertedCount,
                    pageUpdatedCount);

                if (page.Items.Count == 0)
                {
                    break;
                }

                if (sourceTotalCount > 0 && fetchedCount >= sourceTotalCount)
                {
                    break;
                }

                if (sourceTotalCount == 0 && totalPage.HasValue && pageIndex >= totalPage.Value)
                {
                    break;
                }

                if (sourceTotalCount == 0 && !totalPage.HasValue && page.Items.Count < SyncPageSize)
                {
                    break;
                }

                pageIndex++;
            }
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }

        if (sourceTotalCount > 0 && fetchedCount != sourceTotalCount)
        {
            throw new InvalidOperationException(
                $"Uyumsoft GetInboxInvoices reported {sourceTotalCount} item(s), but " +
                $"{fetchedCount} item(s) were fetched for {filterLabel} range " +
                $"{queryStartDate:yyyy-MM-dd HH:mm:ss} - {queryEndDate:yyyy-MM-dd HH:mm:ss}.");
        }

        return new SyncRunResult(
            sourceTotalCount,
            fetchedCount,
            matchedCount,
            insertedCount,
            updatedCount);
    }

    private DateTime ResolveExecutionQueryEndDate(DateTime endDate)
    {
        var configuredLookAheadDays =
            configuration.GetValue<int?>("FaturaGoruntuleme:SynchronizationExecutionLookAheadDays") ??
            configuration.GetValue<int?>("InvoiceViewingSynchronization:ExecutionLookAheadDays");
        var lookAheadDays = Math.Clamp(
            configuredLookAheadDays.GetValueOrDefault(DefaultExecutionLookAheadDays),
            0,
            MaxExecutionLookAheadDays);

        var lookAheadEndDate = endDate.Date.AddDays(lookAheadDays + 1).AddMilliseconds(-1);
        var nowLocal = ResolveLocalNow();

        return lookAheadEndDate > nowLocal
            ? nowLocal
            : lookAheadEndDate;
    }

    private DateTime ResolveLocalNow()
    {
        var utcNow = clock.UtcNow.Kind == DateTimeKind.Utc
            ? clock.UtcNow
            : DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);

        return utcNow.ToLocalTime();
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
        var rangeEnd = endDate;

        return
        [
            filterMode switch
            {
                DateRangeQueryFilterMode.ExecutionDate => new InboxInvoiceQueryPayload(
                    "execution-date-ordered",
                    BuildInboxInvoiceQuery(
                        zeroBasedPageIndex,
                        pageSize,
                        onlyNewestInvoices: false,
                        executionStartDate: rangeStart,
                        executionEndDate: rangeEnd,
                        sortColumn: "ExecutionDate",
                        sortMode: "Descending")),
                DateRangeQueryFilterMode.CreateDate => new InboxInvoiceQueryPayload(
                    "create-date-ordered",
                    BuildInboxInvoiceQuery(
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
                BuildInboxInvoiceQuery(
                    pageIndex,
                    pageSize,
                    onlyNewestInvoices: false,
                    invoiceIds: [invoiceId])),
            new(
                "invoice-numbers",
                BuildInboxInvoiceQuery(
                    pageIndex,
                    pageSize,
                    onlyNewestInvoices: false,
                    invoiceNumbers: [invoiceId]))
        ];
    }

    private static UyumsoftInvoice.InboxInvoiceQueryModel BuildInboxInvoiceQuery(
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
        var query = new UyumsoftInvoice.InboxInvoiceQueryModel
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            OnlyNewestInvoices = onlyNewestInvoices,
            ExecutionStartDate = executionStartDate,
            ExecutionEndDate = executionEndDate,
            InvoiceIds = invoiceIds?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray(),
            InvoiceNumbers = invoiceNumbers?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray()
        };

        return query;
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

    private static ParsedInboxInvoice KeepExistingStatusValues(
        UyumsoftInboxInvoice existing,
        ParsedInboxInvoice incoming) =>
        incoming with
        {
            StatusCode = existing.StatusCode,
            Status = existing.Status,
            EnvelopeStatusCode = existing.EnvelopeStatusCode,
            EnvelopeIdentifier = existing.EnvelopeIdentifier,
            Message = existing.Message
        };

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
        !string.Equals(existing.EnvelopeStatusCode, NormalizeOptional(incoming.EnvelopeStatusCode, 80), StringComparison.Ordinal) ||
        !string.Equals(existing.EnvelopeIdentifier, NormalizeOptional(incoming.EnvelopeIdentifier, 150) ?? string.Empty, StringComparison.Ordinal) ||
        !string.Equals(existing.Message, NormalizeOptional(incoming.Message, 500) ?? string.Empty, StringComparison.Ordinal) ||
        existing.TaxTotal != incoming.TaxTotal ||
        existing.TaxExclusiveAmount != incoming.TaxExclusiveAmount ||
        !string.Equals(existing.DocumentCurrencyCode, NormalizeOptional(incoming.DocumentCurrencyCode, 10) ?? string.Empty, StringComparison.Ordinal) ||
        existing.ExchangeRate != incoming.ExchangeRate ||
        !string.Equals(existing.OrderDocumentId, NormalizeOptional(incoming.OrderDocumentId, 150) ?? string.Empty, StringComparison.Ordinal) ||
        existing.IsArchived != incoming.IsArchived ||
        !string.Equals(existing.InvoiceTipType, NormalizeOptional(incoming.InvoiceTipType, 80) ?? string.Empty, StringComparison.Ordinal) ||
        existing.InvoiceTipTypeCode != incoming.InvoiceTipTypeCode ||
        existing.IsSeen != incoming.IsSeen;

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

        var totalCount = valueNode is null
            ? items.Count
            : ReadInt(valueNode, "TotalCount");

        return new ParsedInboxInvoicePage(totalCount, totalPage, items);
    }

    private static ParsedInboxInvoicePage ParseInvoiceInfoPage(UyumsoftInvoice.InvoicesResponse response)
    {
        var value = response.Value;
        var items = value?.Items ?? [];
        var mappedItems = items
            .Select(MapInvoiceInfo)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();

        if (mappedItems.Length != items.Length)
        {
            throw new InvalidOperationException(
                $"Uyumsoft GetInboxInvoices returned {items.Length} item(s), but " +
                $"{mappedItems.Length} item(s) contained a usable Invoice payload.");
        }

        return new ParsedInboxInvoicePage(
            value?.TotalCount ?? mappedItems.Length,
            value?.TotalPages ?? 0,
            mappedItems);
    }

    private static ParsedInboxInvoice? MapInvoiceInfo(UyumsoftInvoice.InvoiceInfo invoiceInfo)
    {
        var invoice = invoiceInfo.Invoice;

        if (invoice is null)
        {
            return null;
        }

        var invoiceId = NormalizeReferenceValue(invoice.ID?.Value);
        var documentId = NormalizeReferenceValue(invoice.UUID?.Value);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            documentId = invoiceId;
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return null;
        }

        var despatchId = NormalizeReferenceValues(
            invoice.DespatchDocumentReference?
                .Select(reference => reference.ID?.Value)
            ?? []);
        var orderDocumentId = NormalizeReferenceValues(
            invoice.OrderReference is null
                ? []
                : [invoice.OrderReference.ID?.Value]);
        var taxTotal = invoice.TaxTotal?
            .Select(tax => tax.TaxAmount?.Value ?? 0m)
            .DefaultIfEmpty(0m)
            .Sum() ?? 0m;
        var createDate = invoiceInfo.CreateDateUtc == default
            ? (DateTime?)null
            : invoiceInfo.CreateDateUtc;

        return new ParsedInboxInvoice(
            documentId,
            string.IsNullOrWhiteSpace(invoiceId) ? documentId : invoiceId,
            documentId,
            NormalizeOptional(invoiceInfo.LocalDocumentId),
            NormalizeOptional(invoiceInfo.TargetCustomer?.Title) ?? ResolveInvoicePartyTitle(invoice.AccountingSupplierParty?.Party),
            NormalizeOptional(invoiceInfo.TargetCustomer?.VknTckn) ?? ResolveInvoicePartyTaxNumber(invoice.AccountingSupplierParty?.Party),
            createDate,
            invoice.IssueDate?.Value ?? createDate,
            NormalizeOptional(invoice.InvoiceTypeCode?.Value) ?? string.Empty,
            invoice.LegalMonetaryTotal?.PayableAmount?.Value ?? 0m,
            string.Join(", ", despatchId),
            false,
            false,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            NormalizeOptional(invoiceInfo.ExtraInformation) ?? string.Empty,
            taxTotal,
            invoice.LegalMonetaryTotal?.TaxExclusiveAmount?.Value ?? 0m,
            NormalizeOptional(invoice.DocumentCurrencyCode?.Value) ?? string.Empty,
            invoice.PricingExchangeRate?.CalculationRate?.Value ?? 0m,
            string.Join(", ", orderDocumentId),
            false,
            NormalizeOptional(invoice.InvoiceTypeCode?.Value) ?? string.Empty,
            0,
            null);
    }

    private static IReadOnlyCollection<UyumsoftResponseNodeDto> ExtractItemNodes(
        IReadOnlyCollection<UyumsoftResponseNodeDto> nodes)
    {
        var itemsNode = FindFirstNode(nodes, "Items");

        if (itemsNode is not null)
        {
            var directChildren = itemsNode.Children
                .Where(IsInboxInvoiceItemNode)
                .ToArray();

            if (directChildren.Length > 0)
            {
                return directChildren;
            }
        }

        return EnumerateNodes(nodes)
            .Where(IsInboxInvoiceItemNode)
            .ToArray();
    }

    private static bool TryParseItem(
        UyumsoftResponseNodeDto itemNode,
        out ParsedInboxInvoice item)
    {
        item = default!;

        if (TryParseInvoiceInfoItem(itemNode, out item))
        {
            return true;
        }

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
        var isSeen = TryReadBool(itemNode, out var parsedIsSeen, "IsSeen")
            ? parsedIsSeen
            : (bool?)null;
        var orderDocumentId = NormalizeReferenceValue(ReadString(itemNode, "OrderDocumentId", "OrderId", "OrderNumber"));
        var despatchId = NormalizeReferenceValue(
            ReadString(
                itemNode,
                "DespatchId",
                "DespatchDocumentId",
                "DespatchNumber",
                "DespatchDocumentReference",
                "DespatchDocumentReferenceId"));

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
            despatchId,
            isNew.HasValue ? !isNew.Value : false,
            ReadBool(itemNode, "IsStandart", "IsStandard"),
            statusCode,
            status,
            ReadString(itemNode, "EnvelopeStatus"),
            ReadString(itemNode, "EnvelopeIdentifier"),
            ReadString(itemNode, "Message"),
            ReadDecimal(itemNode, "TaxTotal"),
            ReadDecimal(itemNode, "TaxExclusiveAmount"),
            ReadString(itemNode, "DocumentCurrencyCode", "CurrencyCode"),
            ReadDecimal(itemNode, "ExchangeRate"),
            orderDocumentId,
            ReadBool(itemNode, "IsArchived"),
            ReadString(itemNode, "InvoiceTipType"),
            ReadInt(itemNode, "InvoiceTipTypeCode"),
            isSeen);

        return true;
    }

    private static bool TryParseInvoiceInfoItem(
        UyumsoftResponseNodeDto itemNode,
        out ParsedInboxInvoice item)
    {
        item = default!;

        var invoiceXml = ReadString(itemNode, "Invoice");

        if (!TryParseInvoiceCandidate(invoiceXml, out var invoiceDocument) || invoiceDocument.Root is null)
        {
            return false;
        }

        var invoiceRoot = invoiceDocument.Root;
        var invoiceId = GetChildValue(invoiceRoot, "ID");
        var invoiceUuid = GetChildValue(invoiceRoot, "UUID");
        var documentId = NormalizeReferenceValue(invoiceUuid);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            documentId = NormalizeReferenceValue(invoiceId);
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            return false;
        }

        var issueDate = ReadDateTimeValue(GetChildValue(invoiceRoot, "IssueDate"));
        var invoiceType = GetChildValue(invoiceRoot, "InvoiceTypeCode") ?? string.Empty;
        var legalMonetaryTotal = FindChild(invoiceRoot, "LegalMonetaryTotal");
        var supplierParty = FindChild(FindChild(invoiceRoot, "AccountingSupplierParty"), "Party");

        var references = ReadInvoiceDocumentReferences(invoiceDocument);
        var taxTotal = invoiceRoot
            .Elements()
            .Where(element => string.Equals(element.Name.LocalName, "TaxTotal", StringComparison.OrdinalIgnoreCase))
            .Select(element => ReadDecimalValue(GetPathValue(element, "TaxAmount")))
            .DefaultIfEmpty(0m)
            .Sum();

        item = new ParsedInboxInvoice(
            documentId,
            string.IsNullOrWhiteSpace(invoiceId) ? documentId : invoiceId,
            null,
            NormalizeOptional(ReadString(itemNode, "LocalDocumentId")),
            ResolvePartyTitle(supplierParty),
            ResolvePartyTaxNumber(supplierParty),
            ReadDateTime(itemNode, "CreateDateUtc", "CreateDate"),
            issueDate,
            invoiceType,
            ReadDecimalValue(GetPathValue(legalMonetaryTotal, "PayableAmount")),
            references.DespatchId,
            false,
            false,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            ReadString(itemNode, "ExtraInformation"),
            taxTotal,
            ReadDecimalValue(GetPathValue(legalMonetaryTotal, "TaxExclusiveAmount")),
            GetChildValue(invoiceRoot, "DocumentCurrencyCode") ?? string.Empty,
            ReadDecimalValue(GetPathValue(FindChild(invoiceRoot, "PricingExchangeRate"), "CalculationRate")),
            references.OrderDocumentId,
            false,
            invoiceType,
            0,
            null);

        return true;
    }

    private static bool IsMissingReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        return normalized is "-" or "--" or "---" ||
               string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "n/a", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "na", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "yok", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReferenceValue(string? value) =>
        IsMissingReference(value)
            ? string.Empty
            : value!.Trim();

    private static IReadOnlyCollection<string> NormalizeReferenceValues(IEnumerable<string?> values) =>
        values
            .Select(NormalizeReferenceValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static InvoiceDocumentReferences ReadInvoiceDocumentReferences(XDocument invoiceDocument)
    {
        var despatchIds = NormalizeReferenceValues(
            invoiceDocument.Root?
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "DespatchDocumentReference", StringComparison.OrdinalIgnoreCase))
                .Select(ReadReferenceId)
            ?? []);

        var orderIds = NormalizeReferenceValues(
            invoiceDocument.Root?
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "OrderReference", StringComparison.OrdinalIgnoreCase))
                .Select(ReadReferenceId)
            ?? []);

        return new InvoiceDocumentReferences(
            string.Join(", ", despatchIds),
            string.Join(", ", orderIds));
    }

    private static string ReadReferenceId(XElement reference) =>
        NormalizeReferenceValue(
            reference.Elements()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "ID", StringComparison.OrdinalIgnoreCase))
                ?.Value);

    private static bool TryExtractInvoiceDocument(
        UyumsoftOperationResponseDto response,
        out XDocument invoiceDocument)
    {
        invoiceDocument = default!;
        var candidates = new List<string?>(capacity: response.Nodes.Count + 2)
        {
            response.ResponsePayloadJson,
            response.ScalarValue
        };

        candidates.AddRange(response.Nodes.SelectMany(FlattenNodeValues));

        foreach (var candidate in candidates)
        {
            if (TryParseInvoiceCandidate(candidate, out invoiceDocument))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string?> FlattenNodeValues(UyumsoftResponseNodeDto node)
    {
        yield return node.Value;

        foreach (var child in node.Children)
        {
            foreach (var value in FlattenNodeValues(child))
            {
                yield return value;
            }
        }
    }

    private static bool TryParseInvoiceCandidate(
        string? candidate,
        out XDocument invoiceDocument)
    {
        invoiceDocument = default!;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var decoded = WebUtility.HtmlDecode(candidate.Trim());

        if (TryParseInvoiceCandidateText(decoded, out invoiceDocument))
        {
            return true;
        }

        try
        {
            var unescaped = Regex.Unescape(decoded);
            return !string.Equals(unescaped, decoded, StringComparison.Ordinal) &&
                   TryParseInvoiceCandidateText(unescaped, out invoiceDocument);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryParseInvoiceCandidateText(
        string candidate,
        out XDocument invoiceDocument)
    {
        invoiceDocument = default!;

        if (TryParseInvoiceXml(candidate, out invoiceDocument))
        {
            return true;
        }

        if (!TryExtractInvoiceXmlSnippet(candidate, out var snippet))
        {
            return false;
        }

        return TryParseInvoiceXml(snippet, out invoiceDocument);
    }

    private static bool TryExtractInvoiceXmlSnippet(
        string candidate,
        out string snippet)
    {
        snippet = string.Empty;

        var match = InvoiceOpenTagRegex.Match(candidate);

        if (!match.Success)
        {
            return false;
        }

        var prefix = match.Groups["prefix"].Success ? $"{match.Groups["prefix"].Value}:" : string.Empty;
        var closingTag = $"</{prefix}Invoice>";
        var endIndex = candidate.LastIndexOf(closingTag, StringComparison.OrdinalIgnoreCase);

        if (endIndex <= match.Index)
        {
            return false;
        }

        snippet = candidate[match.Index..(endIndex + closingTag.Length)];
        return true;
    }

    private static bool TryParseInvoiceXml(
        string xmlContent,
        out XDocument invoiceDocument)
    {
        invoiceDocument = default!;

        try
        {
            var parsedDocument = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
            var invoiceElement = parsedDocument.Root?
                .DescendantsAndSelf()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "Invoice", StringComparison.OrdinalIgnoreCase) &&
                    element.HasElements);

            if (invoiceElement is null)
            {
                return false;
            }

            invoiceDocument = new XDocument(invoiceElement);
            return true;
        }
        catch
        {
            return false;
        }
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

    private static bool IsInboxInvoiceItemNode(UyumsoftResponseNodeDto node) =>
        IsInvoiceListItemNode(node) || IsInvoiceInfoItemNode(node);

    private static bool IsInvoiceInfoItemNode(UyumsoftResponseNodeDto node)
    {
        if (node.Children.Count == 0)
        {
            return false;
        }

        var childNames = node.Children
            .Select(child => child.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return childNames.Contains("Invoice") &&
               (childNames.Contains("CreateDateUtc") ||
                childNames.Contains("LocalDocumentId") ||
                childNames.Contains("TargetCustomer") ||
                childNames.Contains("ExtraInformation"));
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

    private static XElement? FindChild(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element =>
            string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string? GetChildValue(XElement? parent, string localName) =>
        NormalizeReferenceValue(FindChild(parent, localName)?.Value) is { Length: > 0 } value
            ? value
            : null;

    private static string? GetPathValue(XElement? parent, params string[] localNames)
    {
        var current = parent;

        foreach (var localName in localNames)
        {
            current = FindChild(current, localName);

            if (current is null)
            {
                return null;
            }
        }

        return NormalizeReferenceValue(current!.Value) is { Length: > 0 } value
            ? value
            : null;
    }

    private static string ResolvePartyTitle(XElement? partyElement)
    {
        var title = GetPathValue(partyElement, "PartyName", "Name") ??
                    GetPathValue(partyElement, "PartyLegalEntity", "RegistrationName");

        return title ?? string.Empty;
    }

    private static string ResolvePartyTaxNumber(XElement? partyElement)
    {
        var taxNumber = GetPathValue(partyElement, "PartyIdentification", "ID") ??
                        GetPathValue(partyElement, "PartyTaxScheme", "CompanyID") ??
                        GetPathValue(partyElement, "PartyLegalEntity", "CompanyID");

        return taxNumber ?? string.Empty;
    }

    private static string ResolveInvoicePartyTitle(UyumsoftInvoice.PartyType? party)
    {
        if (party is null)
        {
            return string.Empty;
        }

        return NormalizeOptional(party.PartyName?.Name?.Value) ??
               NormalizeOptional(party.PartyLegalEntity?.FirstOrDefault()?.RegistrationName?.Value) ??
               NormalizeOptional(party.PartyTaxScheme?.RegistrationName?.Value) ??
               string.Empty;
    }

    private static string ResolveInvoicePartyTaxNumber(UyumsoftInvoice.PartyType? party)
    {
        if (party is null)
        {
            return string.Empty;
        }

        return NormalizeOptional(party.PartyIdentification?.FirstOrDefault()?.ID?.Value) ??
               NormalizeOptional(party.PartyTaxScheme?.CompanyID?.Value) ??
               NormalizeOptional(party.PartyLegalEntity?.FirstOrDefault()?.CompanyID?.Value) ??
               string.Empty;
    }

    private static DateTime? ReadDateTimeValue(string? value)
    {
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

        return DateTime.TryParse(
            value,
            CultureInfo.GetCultureInfo("tr-TR"),
            DateTimeStyles.AllowWhiteSpaces,
            out var trDate)
            ? trDate
            : null;
    }

    private static decimal ReadDecimalValue(string? value)
    {
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private UyumsoftServiceEndpointOptions ResolveEInvoiceOptions()
    {
        var catalog = UyumsoftConnectedServiceCatalog.GetService(UyumsoftConnectedServiceKind.EInvoice);
        var configured = uyumsoftOptions.Value.EInvoice;
        var resolved = configured with
        {
            EndpointUrl = string.IsNullOrWhiteSpace(configured.EndpointUrl)
                ? catalog.DefaultEndpointUrl
                : configured.EndpointUrl,
            WsdlUrl = string.IsNullOrWhiteSpace(configured.WsdlUrl)
                ? catalog.DefaultWsdlUrl
                : configured.WsdlUrl,
            ContractName = string.IsNullOrWhiteSpace(configured.ContractName)
                ? catalog.ContractName
                : configured.ContractName
        };

        if (string.IsNullOrWhiteSpace(resolved.EndpointUrl))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} endpoint configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(resolved.Username))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} username configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(resolved.Password))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} password configuration is required.");
        }

        return resolved;
    }

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
        int TotalCount,
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
        string? EnvelopeStatusCode,
        string EnvelopeIdentifier,
        string Message,
        decimal TaxTotal,
        decimal TaxExclusiveAmount,
        string DocumentCurrencyCode,
        decimal ExchangeRate,
        string OrderDocumentId,
        bool IsArchived,
        string InvoiceTipType,
        int InvoiceTipTypeCode,
        bool? IsSeen);

    private sealed record InvoiceDocumentReferences(
        string DespatchId,
        string OrderDocumentId);

    private sealed record SyncUpsertResult(
        int InsertedCount,
        int UpdatedCount)
    {
        public bool HasChanges => InsertedCount > 0 || UpdatedCount > 0;
    }

    private sealed record SyncRunResult(
        int SourceTotalCount,
        int FetchedCount,
        int MatchedCount,
        int InsertedCount,
        int UpdatedCount);

    private sealed record InboxInvoiceQueryPayload(
        string Name,
        UyumsoftInvoice.InboxInvoiceQueryModel Query);

    private enum DateRangeQueryFilterMode
    {
        ExecutionDate,
        CreateDate
    }

}
