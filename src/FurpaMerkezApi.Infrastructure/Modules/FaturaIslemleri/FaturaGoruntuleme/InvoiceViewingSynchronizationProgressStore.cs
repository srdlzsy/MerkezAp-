using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class InvoiceViewingSynchronizationProgressStore
{
    private readonly object gate = new();
    private InvoiceViewingSynchronizationProgressResponse latest = CreateIdleProgress();

    public InvoiceViewingSynchronizationProgressResponse Get()
    {
        lock (gate)
        {
            return latest;
        }
    }

    public InvoiceViewingSynchronizationProgressResponse Queue(
        DateTime startDate,
        DateTime endDate,
        bool includeStatuses,
        int pageSize)
    {
        var now = DateTime.UtcNow;

        lock (gate)
        {
            latest = new InvoiceViewingSynchronizationProgressResponse(
                IsRunning: true,
                Status: "queued",
                StartDate: startDate.Date,
                EndDate: endDate.Date,
                IncludeStatuses: includeStatuses,
                QueryStartDate: null,
                QueryEndDate: null,
                PageIndex: 0,
                PageNumber: 0,
                PageSize: pageSize,
                TotalCount: 0,
                TotalPage: 0,
                FetchedCount: 0,
                MatchedCount: 0,
                SkippedInvoiceDateOutOfRangeCount: 0,
                SkippedDuplicateDocumentCount: 0,
                InsertedCount: 0,
                UpdatedCount: 0,
                LastPageItemCount: 0,
                LastPageMatchedCount: 0,
                LastPageSkippedInvoiceDateOutOfRangeCount: 0,
                LastPageSkippedDuplicateDocumentCount: 0,
                LastPageInsertedCount: 0,
                LastPageUpdatedCount: 0,
                ProgressPercent: 0,
                StartedAtUtc: now,
                LastUpdatedAtUtc: now,
                FinishedAtUtc: null,
                ElapsedMs: 0,
                Message: "Senkronizasyon siraya alindi.");

            return latest;
        }
    }

    public void Start(
        DateTime startDate,
        DateTime endDate,
        bool includeStatuses,
        DateTime queryStartDate,
        DateTime queryEndDate,
        int pageSize)
    {
        var now = DateTime.UtcNow;

        lock (gate)
        {
            latest = new InvoiceViewingSynchronizationProgressResponse(
                IsRunning: true,
                Status: "running",
                StartDate: startDate.Date,
                EndDate: endDate.Date,
                IncludeStatuses: includeStatuses,
                QueryStartDate: queryStartDate.Date,
                QueryEndDate: queryEndDate,
                PageIndex: 0,
                PageNumber: 0,
                PageSize: pageSize,
                TotalCount: 0,
                TotalPage: 0,
                FetchedCount: 0,
                MatchedCount: 0,
                SkippedInvoiceDateOutOfRangeCount: 0,
                SkippedDuplicateDocumentCount: 0,
                InsertedCount: 0,
                UpdatedCount: 0,
                LastPageItemCount: 0,
                LastPageMatchedCount: 0,
                LastPageSkippedInvoiceDateOutOfRangeCount: 0,
                LastPageSkippedDuplicateDocumentCount: 0,
                LastPageInsertedCount: 0,
                LastPageUpdatedCount: 0,
                ProgressPercent: 0,
                StartedAtUtc: now,
                LastUpdatedAtUtc: now,
                FinishedAtUtc: null,
                ElapsedMs: 0,
                Message: "Senkronizasyon basladi.");
        }
    }

    public void ReportPage(
        int pageIndex,
        int pageNumber,
        int pageSize,
        int totalCount,
        int totalPage,
        int fetchedCount,
        int matchedCount,
        int skippedInvoiceDateOutOfRangeCount,
        int skippedDuplicateDocumentCount,
        int insertedCount,
        int updatedCount,
        int lastPageItemCount,
        int lastPageMatchedCount,
        int lastPageSkippedInvoiceDateOutOfRangeCount,
        int lastPageSkippedDuplicateDocumentCount,
        int lastPageInsertedCount,
        int lastPageUpdatedCount)
    {
        var now = DateTime.UtcNow;

        lock (gate)
        {
            latest = latest with
            {
                IsRunning = true,
                Status = "running",
                PageIndex = pageIndex,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPage = totalPage,
                FetchedCount = fetchedCount,
                MatchedCount = matchedCount,
                SkippedInvoiceDateOutOfRangeCount = skippedInvoiceDateOutOfRangeCount,
                SkippedDuplicateDocumentCount = skippedDuplicateDocumentCount,
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                LastPageItemCount = lastPageItemCount,
                LastPageMatchedCount = lastPageMatchedCount,
                LastPageSkippedInvoiceDateOutOfRangeCount = lastPageSkippedInvoiceDateOutOfRangeCount,
                LastPageSkippedDuplicateDocumentCount = lastPageSkippedDuplicateDocumentCount,
                LastPageInsertedCount = lastPageInsertedCount,
                LastPageUpdatedCount = lastPageUpdatedCount,
                ProgressPercent = CalculateProgressPercent(pageNumber, totalPage, fetchedCount, totalCount),
                LastUpdatedAtUtc = now,
                ElapsedMs = CalculateElapsedMs(latest.StartedAtUtc, now),
                Message = totalPage > 0
                    ? $"Sayfa {pageNumber}/{totalPage} islendi."
                    : $"Sayfa {pageNumber} islendi."
            };
        }
    }

    public void Complete(
        int sourceTotalCount,
        int fetchedCount,
        int matchedCount,
        int skippedInvoiceDateOutOfRangeCount,
        int skippedDuplicateDocumentCount,
        int insertedCount,
        int updatedCount)
    {
        var now = DateTime.UtcNow;

        lock (gate)
        {
            latest = latest with
            {
                IsRunning = false,
                Status = "completed",
                TotalCount = sourceTotalCount,
                FetchedCount = fetchedCount,
                MatchedCount = matchedCount,
                SkippedInvoiceDateOutOfRangeCount = skippedInvoiceDateOutOfRangeCount,
                SkippedDuplicateDocumentCount = skippedDuplicateDocumentCount,
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                ProgressPercent = 100m,
                LastUpdatedAtUtc = now,
                FinishedAtUtc = now,
                ElapsedMs = CalculateElapsedMs(latest.StartedAtUtc, now),
                Message = "Senkronizasyon tamamlandi."
            };
        }
    }

    public void Fail(string message)
    {
        var now = DateTime.UtcNow;

        lock (gate)
        {
            latest = latest with
            {
                IsRunning = false,
                Status = "failed",
                LastUpdatedAtUtc = now,
                FinishedAtUtc = now,
                ElapsedMs = CalculateElapsedMs(latest.StartedAtUtc, now),
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Senkronizasyon hata ile durdu."
                    : message
            };
        }
    }

    private static InvoiceViewingSynchronizationProgressResponse CreateIdleProgress() =>
        new(
            IsRunning: false,
            Status: "idle",
            StartDate: null,
            EndDate: null,
            IncludeStatuses: null,
            QueryStartDate: null,
            QueryEndDate: null,
            PageIndex: 0,
            PageNumber: 0,
            PageSize: 0,
            TotalCount: 0,
            TotalPage: 0,
            FetchedCount: 0,
            MatchedCount: 0,
            SkippedInvoiceDateOutOfRangeCount: 0,
            SkippedDuplicateDocumentCount: 0,
            InsertedCount: 0,
            UpdatedCount: 0,
            LastPageItemCount: 0,
            LastPageMatchedCount: 0,
            LastPageSkippedInvoiceDateOutOfRangeCount: 0,
            LastPageSkippedDuplicateDocumentCount: 0,
            LastPageInsertedCount: 0,
            LastPageUpdatedCount: 0,
            ProgressPercent: 0,
            StartedAtUtc: null,
            LastUpdatedAtUtc: null,
            FinishedAtUtc: null,
            ElapsedMs: 0,
            Message: "Henuz senkronizasyon baslamadi.");

    private static decimal CalculateProgressPercent(
        int pageNumber,
        int totalPage,
        int fetchedCount,
        int totalCount)
    {
        if (totalPage > 0)
        {
            return Math.Round(Math.Min(100m, pageNumber * 100m / totalPage), 2);
        }

        return totalCount > 0
            ? Math.Round(Math.Min(100m, fetchedCount * 100m / totalCount), 2)
            : 0m;
    }

    private static long CalculateElapsedMs(DateTime? startedAtUtc, DateTime nowUtc) =>
        startedAtUtc.HasValue
            ? Math.Max(0, (long)(nowUtc - startedAtUtc.Value).TotalMilliseconds)
            : 0;
}
