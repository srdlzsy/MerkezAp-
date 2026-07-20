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
                true,
                "queued",
                startDate.Date,
                endDate.Date,
                includeStatuses,
                null,
                null,
                0,
                0,
                pageSize,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                now,
                now,
                null,
                0,
                "Senkronizasyon siraya alindi.");

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
                true,
                "running",
                startDate.Date,
                endDate.Date,
                includeStatuses,
                queryStartDate.Date,
                queryEndDate,
                0,
                0,
                pageSize,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                now,
                now,
                null,
                0,
                "Senkronizasyon basladi.");
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
        int insertedCount,
        int updatedCount,
        int lastPageItemCount,
        int lastPageMatchedCount,
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
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                LastPageItemCount = lastPageItemCount,
                LastPageMatchedCount = lastPageMatchedCount,
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
            false,
            "idle",
            null,
            null,
            null,
            null,
            null,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            null,
            0,
            "Henuz senkronizasyon baslamadi.");

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
