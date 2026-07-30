using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OrtakIslemler.Duyurular;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.OrtakIslemler.Duyurular;

public sealed class DuyurularService(AuthDbContext dbContext, IClock clock) : IDuyurularService
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const int DefaultSearchTake = 25;
    private const int MaxSearchTake = 100;

    public async Task<IReadOnlyCollection<AnnouncementDto>> GetInboxAsync(
        AnnouncementInboxRequest request,
        CancellationToken cancellationToken)
    {
        ValidateUserId(request.UserId);
        ValidateUserWarehouseNo(request.WarehouseNo);

        var take = NormalizeTake(request.Take);
        var query = CreateVisibleAnnouncementQuery(request.UserId, request.WarehouseNo, clock.UtcNow)
            .AsNoTracking();

        if (!request.IncludeRead)
        {
            query = query.Where(announcement =>
                !announcement.Reads.Any(read => read.UserId == request.UserId));
        }

        var announcements = await query
            .Include(announcement => announcement.Targets)
            .Include(announcement => announcement.Reads.Where(read => read.UserId == request.UserId))
            .OrderByDescending(announcement => announcement.Priority)
            .ThenByDescending(announcement => announcement.PublishedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return announcements
            .Select(announcement => ToDto(announcement, request.UserId))
            .ToArray();
    }

    public async Task<AnnouncementSummaryDto> GetSummaryAsync(
        Guid userId,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        ValidateUserId(userId);
        ValidateUserWarehouseNo(warehouseNo);

        var query = CreateVisibleAnnouncementQuery(userId, warehouseNo, clock.UtcNow)
            .AsNoTracking();

        var activeCount = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(
            announcement => !announcement.Reads.Any(read => read.UserId == userId),
            cancellationToken);

        var latest = await query
            .OrderByDescending(announcement => announcement.PublishedAtUtc)
            .Select(announcement => new
            {
                announcement.Id,
                announcement.PublishedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new AnnouncementSummaryDto(
            activeCount,
            unreadCount,
            latest?.Id,
            latest?.PublishedAtUtc);
    }

    public async Task<AnnouncementDto> MarkAsReadAsync(
        Guid announcementId,
        Guid userId,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        ValidateAnnouncementId(announcementId);
        ValidateUserId(userId);
        ValidateUserWarehouseNo(warehouseNo);

        var announcement = await CreateVisibleAnnouncementQuery(userId, warehouseNo, clock.UtcNow)
            .Include(currentAnnouncement => currentAnnouncement.Targets)
            .Include(currentAnnouncement => currentAnnouncement.Reads.Where(read => read.UserId == userId))
            .FirstOrDefaultAsync(currentAnnouncement => currentAnnouncement.Id == announcementId, cancellationToken);

        if (announcement is null)
        {
            throw new KeyNotFoundException("Announcement was not found.");
        }

        if (announcement.Reads.All(read => read.UserId != userId))
        {
            announcement.Reads.Add(new AnnouncementRead(announcement.Id, userId, clock.UtcNow));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(announcement, userId);
    }

    public async Task<IReadOnlyCollection<AnnouncementDto>> ListForManagementAsync(
        AnnouncementManagementListRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(request.Actor);
        ValidateDateFilter(request.StartDate, request.EndDate);

        var take = NormalizeTake(request.Take);
        var query = ApplyManagementScope(
            dbContext.Announcements.AsNoTracking(),
            request.Actor);

        if (!request.IncludeArchived)
        {
            query = query.Where(announcement => announcement.Status != AnnouncementStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ParseStatus(request.Status);
            query = query.Where(announcement => announcement.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetType))
        {
            var targetType = ParseTargetType(request.TargetType);
            query = query.Where(announcement => announcement.Targets.Any(target => target.Type == targetType));
        }

        if (request.TargetWarehouseNo is { } targetWarehouseNo)
        {
            ValidateTargetWarehouseNo(targetWarehouseNo);

            if (!request.Actor.CanTargetAllWarehouses && targetWarehouseNo != request.Actor.WarehouseNo)
            {
                throw new ArgumentException(
                    "Current user is not allowed to filter another warehouse.",
                    nameof(request.TargetWarehouseNo));
            }

            query = query.Where(announcement =>
                announcement.Targets.Any(target => target.WarehouseNo == targetWarehouseNo));
        }

        if (request.TargetUserId is { } targetUserId)
        {
            ValidateUserId(targetUserId);
            query = query.Where(announcement =>
                announcement.Targets.Any(target => target.UserId == targetUserId));
        }

        if (request.StartDate is { } startDate)
        {
            query = query.Where(announcement => announcement.CreatedAtUtc >= startDate.Date);
        }

        if (request.EndDate is { } endDate)
        {
            query = query.Where(announcement => announcement.CreatedAtUtc < endDate.Date.AddDays(1));
        }

        var announcements = await query
            .Include(announcement => announcement.Targets)
            .OrderByDescending(announcement => announcement.PublishedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        var readSummaries = await BuildReadSummaryByAnnouncementIdAsync(
            announcements,
            request.Actor,
            cancellationToken);

        return announcements
            .Select(announcement => ToDto(
                announcement,
                null,
                GetReadSummary(readSummaries, announcement.Id)))
            .ToArray();
    }

    public async Task<AnnouncementDto> GetForManagementAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var announcement = await GetScopedAnnouncementAsync(
            announcementId,
            actor,
            asTracking: false,
            includeReads: false,
            cancellationToken);

        return await ToManagementDtoAsync(
            announcement,
            actor,
            includeReceipts: true,
            cancellationToken);
    }

    public async Task<AnnouncementReadReceiptListDto> GetReadReceiptsAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var announcement = await GetScopedAnnouncementAsync(
            announcementId,
            actor,
            asTracking: false,
            includeReads: false,
            cancellationToken);

        var readSummaries = await BuildReadSummaryByAnnouncementIdAsync(
            [announcement],
            actor,
            cancellationToken);

        var readers = await ListReadReceiptsAsync(announcement.Id, actor, cancellationToken);

        return new AnnouncementReadReceiptListDto(
            announcement.Id,
            GetReadSummary(readSummaries, announcement.Id) ?? CreateEmptyReadSummary(),
            readers);
    }

    public async Task<IReadOnlyCollection<AnnouncementTargetUserDto>> SearchTargetUsersAsync(
        AnnouncementTargetUserSearchRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(request.Actor);

        if (request.WarehouseNo is { } warehouseNo)
        {
            ValidateTargetWarehouseNo(warehouseNo);

            if (!request.Actor.CanTargetAllWarehouses && warehouseNo != request.Actor.WarehouseNo)
            {
                throw new ArgumentException(
                    "Current user is not allowed to search another warehouse.",
                    nameof(request.WarehouseNo));
            }
        }

        var take = NormalizeSearchTake(request.Take);
        var searchTerm = NormalizeSearchTerm(request.SearchTerm);

        var query = ApplyTargetUserSearchScope(
            dbContext.Users.AsNoTracking().Where(user => user.IsActive),
            request.Actor,
            request.WarehouseNo);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(user =>
                user.Username.ToLower().Contains(searchTerm) ||
                user.Email.ToLower().Contains(searchTerm) ||
                user.FirstName.ToLower().Contains(searchTerm) ||
                user.LastName.ToLower().Contains(searchTerm) ||
                (user.FirstName + " " + user.LastName).ToLower().Contains(searchTerm) ||
                user.WarehouseNo.ToLower().Contains(searchTerm) ||
                user.WarehouseName.ToLower().Contains(searchTerm));
        }

        var users = await query
            .OrderBy(user => user.WarehouseNo)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .ThenBy(user => user.Username)
            .Take(take)
            .Select(user => new
            {
                user.Id,
                user.Username,
                user.Email,
                user.FirstName,
                user.LastName,
                user.WarehouseNo,
                user.WarehouseName
            })
            .ToArrayAsync(cancellationToken);

        return users
            .Select(user => ToTargetUserDto(
                user.Id,
                user.Username,
                user.Email,
                user.FirstName,
                user.LastName,
                user.WarehouseNo,
                user.WarehouseName))
            .ToArray();
    }

    public async Task<AnnouncementDto> CreateAsync(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(request.Actor);

        var now = clock.UtcNow;
        var announcementId = Guid.NewGuid();
        var announcement = new Announcement(
            announcementId,
            request.Title,
            request.Message,
            ParsePriority(request.Priority),
            request.Actor.UserId,
            request.Actor.Username,
            request.Actor.FullName,
            request.StartsAtUtc,
            request.ExpiresAtUtc,
            now);

        var targets = await BuildTargetsAsync(
            announcementId,
            ParseTargetType(request.TargetType),
            request.TargetWarehouseNos,
            request.TargetUserIds,
            request.Actor,
            cancellationToken);

        announcement.Targets.AddRange(targets);
        dbContext.Announcements.Add(announcement);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToManagementDtoAsync(
            announcement,
            request.Actor,
            includeReceipts: false,
            cancellationToken);
    }

    public async Task<AnnouncementDto> UpdateAsync(
        Guid announcementId,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        ValidateActor(request.Actor);

        var announcement = await GetScopedAnnouncementAsync(
            announcementId,
            request.Actor,
            asTracking: true,
            includeReads: true,
            cancellationToken);

        EnsureCanModify(announcement, request.Actor);

        announcement.Update(
            request.Title,
            request.Message,
            ParsePriority(request.Priority),
            request.StartsAtUtc,
            request.ExpiresAtUtc,
            clock.UtcNow);

        var targets = await BuildTargetsAsync(
            announcement.Id,
            ParseTargetType(request.TargetType),
            request.TargetWarehouseNos,
            request.TargetUserIds,
            request.Actor,
            cancellationToken);

        dbContext.AnnouncementTargets.RemoveRange(announcement.Targets);
        dbContext.AnnouncementReads.RemoveRange(announcement.Reads);
        announcement.Targets.Clear();
        announcement.Reads.Clear();
        announcement.Targets.AddRange(targets);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToManagementDtoAsync(
            announcement,
            request.Actor,
            includeReceipts: false,
            cancellationToken);
    }

    public async Task<AnnouncementDto> ArchiveAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        ValidateActor(actor);

        var announcement = await GetScopedAnnouncementAsync(
            announcementId,
            actor,
            asTracking: true,
            includeReads: false,
            cancellationToken);

        EnsureCanModify(announcement, actor);
        announcement.Archive(actor.UserId, clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ToManagementDtoAsync(
            announcement,
            actor,
            includeReceipts: false,
            cancellationToken);
    }

    private IQueryable<Announcement> CreateVisibleAnnouncementQuery(
        Guid userId,
        int warehouseNo,
        DateTime now)
    {
        var normalizedNow = DateTime.SpecifyKind(now, DateTimeKind.Utc);

        return dbContext.Announcements
            .Where(announcement => announcement.Status == AnnouncementStatus.Published)
            .Where(announcement => announcement.StartsAtUtc == null || announcement.StartsAtUtc <= normalizedNow)
            .Where(announcement => announcement.ExpiresAtUtc == null || announcement.ExpiresAtUtc > normalizedNow)
            .Where(announcement => announcement.Targets.Any(target =>
                target.Type == AnnouncementTargetType.AllWarehouses ||
                (target.Type == AnnouncementTargetType.Warehouse && target.WarehouseNo == warehouseNo) ||
                (target.Type == AnnouncementTargetType.User && target.UserId == userId)));
    }

    private async Task<Announcement> GetScopedAnnouncementAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        bool asTracking,
        bool includeReads,
        CancellationToken cancellationToken)
    {
        ValidateAnnouncementId(announcementId);
        ValidateActor(actor);

        IQueryable<Announcement> query = ApplyManagementScope(dbContext.Announcements, actor)
            .Include(announcement => announcement.Targets);

        if (includeReads)
        {
            query = query
                .Include(announcement => announcement.Reads)
                .ThenInclude(read => read.User);
        }

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        var announcement = await query
            .FirstOrDefaultAsync(currentAnnouncement => currentAnnouncement.Id == announcementId, cancellationToken);

        return announcement ?? throw new KeyNotFoundException("Announcement was not found.");
    }

    private static IQueryable<Announcement> ApplyManagementScope(
        IQueryable<Announcement> query,
        AnnouncementActorContext actor)
    {
        if (actor.CanTargetAllWarehouses)
        {
            return query;
        }

        return query.Where(announcement =>
            announcement.CreatedByUserId == actor.UserId ||
            announcement.Targets.Any(target =>
                target.Type == AnnouncementTargetType.AllWarehouses ||
                target.WarehouseNo == actor.WarehouseNo ||
                target.UserId == actor.UserId));
    }

    private async Task<AnnouncementDto> ToManagementDtoAsync(
        Announcement announcement,
        AnnouncementActorContext actor,
        bool includeReceipts,
        CancellationToken cancellationToken)
    {
        var readSummaries = await BuildReadSummaryByAnnouncementIdAsync(
            [announcement],
            actor,
            cancellationToken);

        var readReceipts = includeReceipts
            ? await ListReadReceiptsAsync(announcement.Id, actor, cancellationToken)
            : Array.Empty<AnnouncementReadReceiptDto>();

        return ToDto(
            announcement,
            null,
            GetReadSummary(readSummaries, announcement.Id),
            readReceipts);
    }

    private async Task<IReadOnlyDictionary<Guid, AnnouncementReadSummaryDto>> BuildReadSummaryByAnnouncementIdAsync(
        IReadOnlyCollection<Announcement> announcements,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var announcementArray = announcements.ToArray();

        if (announcementArray.Length == 0)
        {
            return new Dictionary<Guid, AnnouncementReadSummaryDto>();
        }

        var announcementIds = announcementArray
            .Select(announcement => announcement.Id)
            .ToArray();

        var readStats = await CreateScopedReadQuery(announcementIds, actor)
            .GroupBy(read => read.AnnouncementId)
            .Select(group => new
            {
                AnnouncementId = group.Key,
                ReadCount = group.Count(),
                LastReadAtUtc = group.Max(read => (DateTime?)read.ReadAtUtc)
            })
            .ToArrayAsync(cancellationToken);

        var readStatsByAnnouncementId = readStats.ToDictionary(
            readStat => readStat.AnnouncementId,
            readStat => readStat);

        var targetUserCounts = await ResolveTargetUserCountsByAnnouncementIdAsync(
            announcementArray,
            actor,
            cancellationToken);

        return announcementArray.ToDictionary(
            announcement => announcement.Id,
            announcement =>
            {
                readStatsByAnnouncementId.TryGetValue(announcement.Id, out var readStat);
                targetUserCounts.TryGetValue(announcement.Id, out var targetUserCount);

                var readCount = readStat?.ReadCount ?? 0;
                var unreadCount = targetUserCount.HasValue
                    ? (int?)Math.Max(targetUserCount.Value - readCount, 0)
                    : null;

                return new AnnouncementReadSummaryDto(
                    readCount,
                    targetUserCount,
                    unreadCount,
                    readStat?.LastReadAtUtc);
            });
    }

    private async Task<IReadOnlyDictionary<Guid, int?>> ResolveTargetUserCountsByAnnouncementIdAsync(
        IReadOnlyCollection<Announcement> announcements,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var announcementArray = announcements.ToArray();
        var result = new Dictionary<Guid, int?>();

        if (announcementArray.Length == 0)
        {
            return result;
        }

        var hasAllWarehousesTarget = announcementArray.Any(announcement =>
            announcement.Targets.Any(target => target.Type == AnnouncementTargetType.AllWarehouses));

        var targetedWarehouseNos = announcementArray
            .SelectMany(announcement => announcement.Targets)
            .Where(target => target.Type == AnnouncementTargetType.Warehouse && target.WarehouseNo.HasValue)
            .Select(target => target.WarehouseNo!.Value)
            .Distinct()
            .ToArray();

        var scopedAllWarehousesUserCount = hasAllWarehousesTarget
            ? await CountActiveUsersInScopeAsync(actor, cancellationToken)
            : 0;

        var userCountByWarehouseNo = await CountActiveUsersByWarehouseNoAsync(
            targetedWarehouseNos,
            actor,
            cancellationToken);

        foreach (var announcement in announcementArray)
        {
            if (announcement.Targets.Any(target => target.Type == AnnouncementTargetType.AllWarehouses))
            {
                result[announcement.Id] = scopedAllWarehousesUserCount;
                continue;
            }

            var warehouseTargets = announcement.Targets
                .Where(target => target.Type == AnnouncementTargetType.Warehouse && target.WarehouseNo.HasValue)
                .Select(target => target.WarehouseNo!.Value)
                .Distinct()
                .ToArray();

            if (warehouseTargets.Length > 0)
            {
                var visibleWarehouseTargets = actor.CanTargetAllWarehouses
                    ? warehouseTargets
                    : warehouseTargets.Where(warehouseNo => warehouseNo == actor.WarehouseNo).ToArray();

                result[announcement.Id] = visibleWarehouseTargets.Sum(warehouseNo =>
                    userCountByWarehouseNo.TryGetValue(warehouseNo, out var userCount) ? userCount : 0);

                continue;
            }

            result[announcement.Id] = announcement.Targets
                .Where(target => target.Type == AnnouncementTargetType.User && target.UserId.HasValue)
                .Where(target => IsTargetVisibleToActor(target, actor))
                .Select(target => target.UserId!.Value)
                .Distinct()
                .Count();
        }

        return result;
    }

    private async Task<int> CountActiveUsersInScopeAsync(
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive);

        if (!actor.CanTargetAllWarehouses)
        {
            var actorWarehouseNoText = actor.WarehouseNo.ToString();
            query = query.Where(user => user.WarehouseNo == actorWarehouseNoText);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<int, int>> CountActiveUsersByWarehouseNoAsync(
        IReadOnlyCollection<int> warehouseNos,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var scopedWarehouseNos = actor.CanTargetAllWarehouses
            ? warehouseNos.Distinct().ToArray()
            : warehouseNos.Where(warehouseNo => warehouseNo == actor.WarehouseNo).Distinct().ToArray();

        if (scopedWarehouseNos.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        var scopedWarehouseNoTexts = scopedWarehouseNos
            .Select(warehouseNo => warehouseNo.ToString())
            .ToArray();

        var counts = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && scopedWarehouseNoTexts.Contains(user.WarehouseNo))
            .GroupBy(user => user.WarehouseNo)
            .Select(group => new
            {
                WarehouseNo = group.Key,
                Count = group.Count()
            })
            .ToArrayAsync(cancellationToken);

        return counts
            .Select(count => new
            {
                WarehouseNo = TryParseWarehouseNo(count.WarehouseNo),
                count.Count
            })
            .Where(count => count.WarehouseNo.HasValue)
            .ToDictionary(
                count => count.WarehouseNo!.Value,
                count => count.Count);
    }

    private async Task<IReadOnlyCollection<AnnouncementReadReceiptDto>> ListReadReceiptsAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var readers = await CreateScopedReadQuery([announcementId], actor)
            .OrderByDescending(read => read.ReadAtUtc)
            .ThenBy(read => read.User.Username)
            .Select(read => new
            {
                read.UserId,
                read.User.Username,
                read.User.FirstName,
                read.User.LastName,
                read.User.Email,
                read.User.WarehouseNo,
                read.User.WarehouseName,
                read.ReadAtUtc
            })
            .ToArrayAsync(cancellationToken);

        return readers
            .Select(reader =>
            {
                var fullName = JoinFullName(reader.FirstName, reader.LastName);

                return new AnnouncementReadReceiptDto(
                    reader.UserId,
                    reader.Username,
                    string.IsNullOrWhiteSpace(fullName) ? reader.Username : fullName,
                    reader.Email,
                    TryParseWarehouseNo(reader.WarehouseNo),
                    reader.WarehouseName,
                    reader.ReadAtUtc);
            })
            .ToArray();
    }

    private IQueryable<AnnouncementRead> CreateScopedReadQuery(
        IReadOnlyCollection<Guid> announcementIds,
        AnnouncementActorContext actor)
    {
        var query = dbContext.AnnouncementReads
            .AsNoTracking()
            .Where(read => announcementIds.Contains(read.AnnouncementId));

        if (actor.CanTargetAllWarehouses)
        {
            return query;
        }

        var actorWarehouseNoText = actor.WarehouseNo.ToString();

        return query.Where(read =>
            read.UserId == actor.UserId ||
            read.User.WarehouseNo == actorWarehouseNoText);
    }

    private static IQueryable<AppUser> ApplyTargetUserSearchScope(
        IQueryable<AppUser> query,
        AnnouncementActorContext actor,
        int? requestedWarehouseNo)
    {
        if (actor.CanTargetAllWarehouses)
        {
            if (requestedWarehouseNo is { } warehouseNo)
            {
                var warehouseNoText = warehouseNo.ToString();
                return query.Where(user => user.WarehouseNo == warehouseNoText);
            }

            return query;
        }

        var actorWarehouseNoText = actor.WarehouseNo.ToString();
        return query.Where(user => user.WarehouseNo == actorWarehouseNoText);
    }

    private static bool IsTargetVisibleToActor(AnnouncementTarget target, AnnouncementActorContext actor) =>
        actor.CanTargetAllWarehouses ||
        target.UserId == actor.UserId ||
        target.WarehouseNo == actor.WarehouseNo;

    private async Task<IReadOnlyCollection<AnnouncementTarget>> BuildTargetsAsync(
        Guid announcementId,
        AnnouncementTargetType targetType,
        IReadOnlyCollection<int>? targetWarehouseNos,
        IReadOnlyCollection<Guid>? targetUserIds,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            AnnouncementTargetType.AllWarehouses => BuildAllWarehousesTarget(announcementId, actor),
            AnnouncementTargetType.Warehouse => await BuildWarehouseTargetsAsync(
                announcementId,
                targetWarehouseNos,
                actor,
                cancellationToken),
            AnnouncementTargetType.User => await BuildUserTargetsAsync(
                announcementId,
                targetUserIds,
                actor,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Target type is invalid.")
        };
    }

    private static IReadOnlyCollection<AnnouncementTarget> BuildAllWarehousesTarget(
        Guid announcementId,
        AnnouncementActorContext actor)
    {
        if (!actor.CanTargetAllWarehouses)
        {
            throw new ArgumentException("All warehouses target requires all warehouses permission.");
        }

        return
        [
            new AnnouncementTarget(
                Guid.NewGuid(),
                announcementId,
                AnnouncementTargetType.AllWarehouses,
                null,
                null,
                null,
                null,
                null)
        ];
    }

    private async Task<IReadOnlyCollection<AnnouncementTarget>> BuildWarehouseTargetsAsync(
        Guid announcementId,
        IReadOnlyCollection<int>? targetWarehouseNos,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var warehouseNos = (targetWarehouseNos ?? [])
            .Where(warehouseNo => warehouseNo > 0)
            .Distinct()
            .ToArray();

        if (warehouseNos.Length == 0)
        {
            throw new ArgumentException("At least one target warehouse is required.", nameof(targetWarehouseNos));
        }

        if (!actor.CanTargetAllWarehouses && warehouseNos.Any(warehouseNo => warehouseNo != actor.WarehouseNo))
        {
            throw new ArgumentException("Current user can only target own warehouse.", nameof(targetWarehouseNos));
        }

        var warehouseNameByNo = await ResolveWarehouseNamesAsync(warehouseNos, actor, cancellationToken);

        return warehouseNos
            .Select(warehouseNo => new AnnouncementTarget(
                Guid.NewGuid(),
                announcementId,
                AnnouncementTargetType.Warehouse,
                warehouseNo,
                warehouseNameByNo.GetValueOrDefault(warehouseNo),
                null,
                null,
                null))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<AnnouncementTarget>> BuildUserTargetsAsync(
        Guid announcementId,
        IReadOnlyCollection<Guid>? targetUserIds,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var userIds = (targetUserIds ?? [])
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            throw new ArgumentException("At least one target user is required.", nameof(targetUserIds));
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.IsActive)
            .Select(user => new
            {
                user.Id,
                user.Username,
                user.FirstName,
                user.LastName,
                user.WarehouseNo,
                user.WarehouseName
            })
            .ToArrayAsync(cancellationToken);

        if (users.Length != userIds.Length)
        {
            throw new ArgumentException("One or more target users are invalid or inactive.", nameof(targetUserIds));
        }

        var targets = new List<AnnouncementTarget>(users.Length);

        foreach (var user in users)
        {
            var warehouseNo = ParseWarehouseNo(user.WarehouseNo);

            if (!actor.CanTargetAllWarehouses && warehouseNo != actor.WarehouseNo)
            {
                throw new ArgumentException("Current user can only target users in own warehouse.", nameof(targetUserIds));
            }

            targets.Add(new AnnouncementTarget(
                Guid.NewGuid(),
                announcementId,
                AnnouncementTargetType.User,
                warehouseNo,
                user.WarehouseName,
                user.Id,
                user.Username,
                JoinFullName(user.FirstName, user.LastName)));
        }

        return targets;
    }

    private async Task<IReadOnlyDictionary<int, string>> ResolveWarehouseNamesAsync(
        IReadOnlyCollection<int> warehouseNos,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken)
    {
        var warehouseNoTexts = warehouseNos
            .Select(warehouseNo => warehouseNo.ToString())
            .ToArray();

        var userWarehouses = await dbContext.Users
            .AsNoTracking()
            .Where(user => warehouseNoTexts.Contains(user.WarehouseNo))
            .Select(user => new
            {
                user.WarehouseNo,
                user.WarehouseName
            })
            .ToArrayAsync(cancellationToken);

        var names = userWarehouses
            .Select(userWarehouse => new
            {
                WarehouseNo = ParseWarehouseNo(userWarehouse.WarehouseNo),
                userWarehouse.WarehouseName
            })
            .GroupBy(userWarehouse => userWarehouse.WarehouseNo)
            .ToDictionary(
                group => group.Key,
                group => group.First().WarehouseName);

        if (warehouseNos.Contains(actor.WarehouseNo))
        {
            names[actor.WarehouseNo] = actor.WarehouseName;
        }

        return names;
    }

    private static void EnsureCanModify(Announcement announcement, AnnouncementActorContext actor)
    {
        if (actor.CanTargetAllWarehouses || announcement.CreatedByUserId == actor.UserId)
        {
            return;
        }

        throw new ArgumentException("Current user can only modify own announcements.");
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

    private static int NormalizeSearchTake(int? take)
    {
        if (take is null)
        {
            return DefaultSearchTake;
        }

        if (take.Value <= 0)
        {
            throw new ArgumentException("Take must be greater than zero.", nameof(take));
        }

        return Math.Min(take.Value, MaxSearchTake);
    }

    private static string NormalizeSearchTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();

        if (normalized.Length > 100)
        {
            throw new ArgumentException("Search term can not exceed 100 characters.", nameof(value));
        }

        return normalized.ToLowerInvariant();
    }

    private static AnnouncementPriority ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AnnouncementPriority.Normal;
        }

        return NormalizeKey(value) switch
        {
            "normal" => AnnouncementPriority.Normal,
            "important" or "onemli" => AnnouncementPriority.Important,
            "urgent" or "acil" => AnnouncementPriority.Urgent,
            _ => throw new ArgumentException("Priority must be one of: Normal, Important, Urgent.")
        };
    }

    private static AnnouncementStatus ParseStatus(string value) =>
        NormalizeKey(value) switch
        {
            "published" or "yayinda" => AnnouncementStatus.Published,
            "archived" or "arsivde" => AnnouncementStatus.Archived,
            _ => throw new ArgumentException("Status must be one of: Published, Archived.")
        };

    private static AnnouncementTargetType ParseTargetType(string value) =>
        NormalizeKey(value) switch
        {
            "allwarehouses" or "tumdepolar" => AnnouncementTargetType.AllWarehouses,
            "warehouse" or "depo" => AnnouncementTargetType.Warehouse,
            "user" or "kullanici" => AnnouncementTargetType.User,
            _ => throw new ArgumentException("Target type must be one of: AllWarehouses, Warehouse, User.")
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
            .ToLowerInvariant()
            .Replace("İ", "i", StringComparison.Ordinal)
            .Replace("ı", "i", StringComparison.Ordinal)
            .Replace("ğ", "g", StringComparison.Ordinal)
            .Replace("ü", "u", StringComparison.Ordinal)
            .Replace("ş", "s", StringComparison.Ordinal)
            .Replace("ö", "o", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal);
    }

    private static void ValidateActor(AnnouncementActorContext actor)
    {
        ValidateUserId(actor.UserId);
        ValidateUserWarehouseNo(actor.WarehouseNo);

        if (string.IsNullOrWhiteSpace(actor.Username))
        {
            throw new ArgumentException("Username is required.", nameof(actor));
        }

        if (string.IsNullOrWhiteSpace(actor.FullName))
        {
            throw new ArgumentException("Full name is required.", nameof(actor));
        }

        if (string.IsNullOrWhiteSpace(actor.WarehouseName))
        {
            throw new ArgumentException("Warehouse name is required.", nameof(actor));
        }
    }

    private static void ValidateAnnouncementId(Guid announcementId)
    {
        if (announcementId == Guid.Empty)
        {
            throw new ArgumentException("Announcement id is required.", nameof(announcementId));
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }

    private static void ValidateUserWarehouseNo(int warehouseNo)
    {
        if (warehouseNo < 0)
        {
            throw new ArgumentException("User warehouse no can not be negative.", nameof(warehouseNo));
        }
    }

    private static void ValidateTargetWarehouseNo(int warehouseNo)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Target warehouse no must be greater than zero.", nameof(warehouseNo));
        }
    }

    private static void ValidateDateFilter(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue && startDate.Value.Date > endDate.Value.Date)
        {
            throw new ArgumentException("Start date can not be later than end date.", nameof(startDate));
        }
    }

    private static int ParseWarehouseNo(string value)
    {
        if (int.TryParse(value, out var warehouseNo) && warehouseNo >= 0)
        {
            return warehouseNo;
        }

        throw new InvalidOperationException("User warehouse information is invalid.");
    }

    private static int? TryParseWarehouseNo(string? value)
    {
        if (int.TryParse(value, out var warehouseNo) && warehouseNo >= 0)
        {
            return warehouseNo;
        }

        return null;
    }

    private static string JoinFullName(string firstName, string lastName)
    {
        var fullName = string.Join(" ", new[] { firstName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()));

        return string.IsNullOrWhiteSpace(fullName) ? string.Empty : fullName;
    }

    private static AnnouncementReadSummaryDto? GetReadSummary(
        IReadOnlyDictionary<Guid, AnnouncementReadSummaryDto> readSummaries,
        Guid announcementId) =>
        readSummaries.TryGetValue(announcementId, out var readSummary)
            ? readSummary
            : null;

    private static AnnouncementReadSummaryDto CreateEmptyReadSummary() =>
        new(0, null, null, null);

    private static AnnouncementDto ToDto(
        Announcement announcement,
        Guid? currentUserId,
        AnnouncementReadSummaryDto? readSummary = null,
        IReadOnlyCollection<AnnouncementReadReceiptDto>? readReceipts = null)
    {
        var readAtUtc = currentUserId.HasValue
            ? announcement.Reads.FirstOrDefault(read => read.UserId == currentUserId.Value)?.ReadAtUtc
            : null;

        return new AnnouncementDto(
            announcement.Id,
            announcement.Title,
            announcement.Message,
            PriorityCode(announcement.Priority),
            PriorityName(announcement.Priority),
            StatusCode(announcement.Status),
            StatusName(announcement.Status),
            announcement.CreatedByUserId,
            announcement.CreatedByUsername,
            announcement.CreatedByFullName,
            announcement.StartsAtUtc,
            announcement.ExpiresAtUtc,
            announcement.PublishedAtUtc,
            announcement.ArchivedAtUtc,
            announcement.ArchivedByUserId,
            announcement.CreatedAtUtc,
            announcement.UpdatedAtUtc,
            readAtUtc,
            announcement.Targets
                .OrderBy(target => target.Type)
                .ThenBy(target => target.WarehouseNo)
                .ThenBy(target => target.Username)
                .Select(ToTargetDto)
                .ToArray(),
            readSummary,
            readReceipts ?? Array.Empty<AnnouncementReadReceiptDto>());
    }

    private static AnnouncementTargetDto ToTargetDto(AnnouncementTarget target) =>
        new(
            target.Id,
            TargetTypeCode(target.Type),
            TargetTypeName(target.Type),
            target.WarehouseNo,
            target.WarehouseName,
            target.UserId,
            target.Username,
            target.UserFullName);

    private static AnnouncementTargetUserDto ToTargetUserDto(
        Guid id,
        string username,
        string email,
        string firstName,
        string lastName,
        string warehouseNo,
        string warehouseName)
    {
        var fullName = JoinFullName(firstName, lastName);
        var displayFullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName;
        var parsedWarehouseNo = TryParseWarehouseNo(warehouseNo);
        var warehouseLabel = parsedWarehouseNo.HasValue
            ? $"{parsedWarehouseNo.Value} - {warehouseName}"
            : warehouseName;

        return new AnnouncementTargetUserDto(
            id,
            username,
            displayFullName,
            email,
            parsedWarehouseNo,
            warehouseName,
            $"{displayFullName} ({username}) / {warehouseLabel}");
    }

    private static string PriorityCode(AnnouncementPriority priority) =>
        priority switch
        {
            AnnouncementPriority.Normal => "Normal",
            AnnouncementPriority.Important => "Important",
            AnnouncementPriority.Urgent => "Urgent",
            _ => priority.ToString()
        };

    private static string PriorityName(AnnouncementPriority priority) =>
        priority switch
        {
            AnnouncementPriority.Normal => "Normal",
            AnnouncementPriority.Important => "Onemli",
            AnnouncementPriority.Urgent => "Acil",
            _ => priority.ToString()
        };

    private static string StatusCode(AnnouncementStatus status) =>
        status switch
        {
            AnnouncementStatus.Published => "Published",
            AnnouncementStatus.Archived => "Archived",
            _ => status.ToString()
        };

    private static string StatusName(AnnouncementStatus status) =>
        status switch
        {
            AnnouncementStatus.Published => "Yayinda",
            AnnouncementStatus.Archived => "Arsivde",
            _ => status.ToString()
        };

    private static string TargetTypeCode(AnnouncementTargetType targetType) =>
        targetType switch
        {
            AnnouncementTargetType.AllWarehouses => "AllWarehouses",
            AnnouncementTargetType.Warehouse => "Warehouse",
            AnnouncementTargetType.User => "User",
            _ => targetType.ToString()
        };

    private static string TargetTypeName(AnnouncementTargetType targetType) =>
        targetType switch
        {
            AnnouncementTargetType.AllWarehouses => "Tum Depolar",
            AnnouncementTargetType.Warehouse => "Depo",
            AnnouncementTargetType.User => "Kullanici",
            _ => targetType.ToString()
        };
}
