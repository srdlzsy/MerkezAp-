using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OrtakIslemler.SikayetOneri;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.OrtakIslemler.SikayetOneri;

public sealed class SikayetOneriService(AuthDbContext dbContext, IClock clock) : ISikayetOneriService
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;

    public async Task<FeedbackItemDto> CreateAsync(
        CreateFeedbackItemRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var item = new FeedbackItem(
            Guid.NewGuid(),
            ParseType(request.Type),
            request.Title,
            request.Message,
            ParsePriority(request.Priority),
            request.UserId,
            request.Username,
            request.FullName,
            request.WarehouseNo,
            request.WarehouseName,
            now);

        dbContext.FeedbackItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    public async Task<IReadOnlyCollection<FeedbackItemDto>> GetMyItemsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);

        var items = await dbContext.FeedbackItems
            .AsNoTracking()
            .Where(item => item.CreatedByUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(DefaultTake)
            .ToArrayAsync(cancellationToken);

        return items.Select(ToDto).ToArray();
    }

    public async Task<FeedbackSummaryDto> GetMySummaryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);

        var query = dbContext.FeedbackItems
            .AsNoTracking()
            .Where(item => item.CreatedByUserId == userId);

        var openCount = await query.CountAsync(
            item => item.Status != FeedbackItemStatus.Resolved &&
                    item.Status != FeedbackItemStatus.Closed &&
                    item.Status != FeedbackItemStatus.Rejected,
            cancellationToken);

        var resolvedCount = await query.CountAsync(
            item => item.Status == FeedbackItemStatus.Resolved ||
                    item.Status == FeedbackItemStatus.Closed,
            cancellationToken);

        var latest = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new
            {
                item.Status,
                item.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new FeedbackSummaryDto(
            openCount,
            resolvedCount,
            latest is null ? null : StatusCode(latest.Status),
            latest?.CreatedAtUtc);
    }

    public async Task<IReadOnlyCollection<FeedbackItemDto>> ListForManagementAsync(
        FeedbackManagementListRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var query = ApplyManagementScope(
            dbContext.FeedbackItems.AsNoTracking(),
            request.CanViewAll,
            request.CurrentUserId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ParseStatus(request.Status);
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = ParseType(request.Type);
            query = query.Where(item => item.Type == type);
        }

        if (request.CanViewAll && request.WarehouseNo is { } warehouseNo)
        {
            if (warehouseNo <= 0)
            {
                throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
            }

            query = query.Where(item => item.WarehouseNo == warehouseNo);
        }

        if (request.StartDate is { } startDate)
        {
            query = query.Where(item => item.CreatedAtUtc >= startDate.Date);
        }

        if (request.EndDate is { } endDate)
        {
            query = query.Where(item => item.CreatedAtUtc < endDate.Date.AddDays(1));
        }

        if (request.StartDate.HasValue &&
            request.EndDate.HasValue &&
            request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            throw new ArgumentException("Start date can not be later than end date.", nameof(request.StartDate));
        }

        var items = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return items.Select(ToDto).ToArray();
    }

    public async Task<FeedbackItemDto> GetForManagementAsync(
        Guid feedbackId,
        FeedbackManagementScope scope,
        CancellationToken cancellationToken)
    {
        var item = await GetScopedItemAsync(feedbackId, scope.CanViewAll, scope.CurrentUserId, cancellationToken);
        return ToDto(item);
    }

    public async Task<FeedbackItemDto> MarkAsReadAsync(
        Guid feedbackId,
        FeedbackManagementActionContext context,
        CancellationToken cancellationToken)
    {
        ValidateUserId(context.UserId);

        var item = await GetScopedItemAsync(feedbackId, context.CanViewAll, context.UserId, cancellationToken);
        item.MarkAsRead(context.UserId, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    public async Task<FeedbackItemDto> ChangeStatusAsync(
        Guid feedbackId,
        ChangeFeedbackStatusRequest request,
        FeedbackManagementActionContext context,
        CancellationToken cancellationToken)
    {
        ValidateUserId(context.UserId);

        var item = await GetScopedItemAsync(feedbackId, context.CanViewAll, context.UserId, cancellationToken);
        item.ChangeStatus(ParseStatus(request.Status), request.AdminNote, context.UserId, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(item);
    }

    private async Task<FeedbackItem> GetScopedItemAsync(
        Guid feedbackId,
        bool canViewAll,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (feedbackId == Guid.Empty)
        {
            throw new ArgumentException("Feedback id is required.", nameof(feedbackId));
        }

        var item = await ApplyManagementScope(
                dbContext.FeedbackItems,
                canViewAll,
                currentUserId)
            .FirstOrDefaultAsync(currentItem => currentItem.Id == feedbackId, cancellationToken);

        return item ?? throw new KeyNotFoundException("Feedback item was not found.");
    }

    private static IQueryable<FeedbackItem> ApplyManagementScope(
        IQueryable<FeedbackItem> query,
        bool canViewAll,
        Guid currentUserId)
    {
        if (canViewAll)
        {
            return query;
        }

        if (currentUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User information was not found on the current user.");
        }

        return query.Where(item => item.CreatedByUserId == currentUserId);
    }

    private static int NormalizeTake(int? take)
    {
        if (take is null)
        {
            return DefaultTake;
        }

        if (take.Value <= 0)
        {
            throw new ArgumentException("Take must be greater than zero.", nameof(take));
        }

        return Math.Min(take.Value, MaxTake);
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }

    private static FeedbackItemType ParseType(string value) =>
        NormalizeKey(value) switch
        {
            "complaint" or "sikayet" => FeedbackItemType.Complaint,
            "suggestion" or "oneri" => FeedbackItemType.Suggestion,
            _ => throw new ArgumentException("Type must be one of: Complaint, Suggestion.")
        };

    private static FeedbackItemPriority ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FeedbackItemPriority.Normal;
        }

        return NormalizeKey(value) switch
        {
            "low" or "dusuk" => FeedbackItemPriority.Low,
            "normal" => FeedbackItemPriority.Normal,
            "high" or "yuksek" => FeedbackItemPriority.High,
            _ => throw new ArgumentException("Priority must be one of: Low, Normal, High.")
        };
    }

    private static FeedbackItemStatus ParseStatus(string value) =>
        NormalizeKey(value) switch
        {
            "new" or "yeni" => FeedbackItemStatus.New,
            "read" or "okundu" => FeedbackItemStatus.Read,
            "inprogress" or "islemde" => FeedbackItemStatus.InProgress,
            "resolved" or "cozuldu" => FeedbackItemStatus.Resolved,
            "closed" or "kapali" => FeedbackItemStatus.Closed,
            "rejected" or "reddedildi" => FeedbackItemStatus.Rejected,
            _ => throw new ArgumentException("Status must be one of: New, Read, InProgress, Resolved, Closed, Rejected.")
        };

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static FeedbackItemDto ToDto(FeedbackItem item) =>
        new(
            item.Id,
            TypeCode(item.Type),
            TypeName(item.Type),
            item.Title,
            item.Message,
            StatusCode(item.Status),
            StatusName(item.Status),
            PriorityCode(item.Priority),
            PriorityName(item.Priority),
            item.CreatedByUserId,
            item.CreatedByUsername,
            item.CreatedByFullName,
            item.WarehouseNo,
            item.WarehouseName,
            item.AdminNote,
            item.ReadAtUtc,
            item.ReadByUserId,
            item.StatusChangedAtUtc,
            item.StatusChangedByUserId,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.ClosedAtUtc);

    private static string TypeCode(FeedbackItemType type) =>
        type switch
        {
            FeedbackItemType.Complaint => "Complaint",
            FeedbackItemType.Suggestion => "Suggestion",
            _ => type.ToString()
        };

    private static string TypeName(FeedbackItemType type) =>
        type switch
        {
            FeedbackItemType.Complaint => "Sikayet",
            FeedbackItemType.Suggestion => "Oneri",
            _ => type.ToString()
        };

    private static string StatusCode(FeedbackItemStatus status) =>
        status switch
        {
            FeedbackItemStatus.New => "New",
            FeedbackItemStatus.Read => "Read",
            FeedbackItemStatus.InProgress => "InProgress",
            FeedbackItemStatus.Resolved => "Resolved",
            FeedbackItemStatus.Closed => "Closed",
            FeedbackItemStatus.Rejected => "Rejected",
            _ => status.ToString()
        };

    private static string StatusName(FeedbackItemStatus status) =>
        status switch
        {
            FeedbackItemStatus.New => "Yeni",
            FeedbackItemStatus.Read => "Okundu",
            FeedbackItemStatus.InProgress => "Islemde",
            FeedbackItemStatus.Resolved => "Cozuldu",
            FeedbackItemStatus.Closed => "Kapali",
            FeedbackItemStatus.Rejected => "Reddedildi",
            _ => status.ToString()
        };

    private static string PriorityCode(FeedbackItemPriority priority) =>
        priority switch
        {
            FeedbackItemPriority.Low => "Low",
            FeedbackItemPriority.Normal => "Normal",
            FeedbackItemPriority.High => "High",
            _ => priority.ToString()
        };

    private static string PriorityName(FeedbackItemPriority priority) =>
        priority switch
        {
            FeedbackItemPriority.Low => "Dusuk",
            FeedbackItemPriority.Normal => "Normal",
            FeedbackItemPriority.High => "Yuksek",
            _ => priority.ToString()
        };
}
