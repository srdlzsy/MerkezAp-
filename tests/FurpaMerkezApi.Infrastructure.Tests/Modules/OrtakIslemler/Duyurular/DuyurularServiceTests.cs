using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OrtakIslemler.Duyurular;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.OrtakIslemler.Duyurular;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OrtakIslemler.Duyurular;

public sealed class DuyurularServiceTests
{
    [Fact]
    public async Task GetForManagementAsync_ReturnsReadSummaryAndReceipts()
    {
        await using var dbContext = CreateAuthDbContext();
        var now = new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc);
        var managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var firstReaderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var secondReaderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var otherWarehouseUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        dbContext.Users.AddRange(
            CreateUser(managerId, "manager", "Mert", "Yonetici", 101),
            CreateUser(firstReaderId, "alice", "Alice", "Okur", 101),
            CreateUser(secondReaderId, "burak", "Burak", "Okur", 101),
            CreateUser(otherWarehouseUserId, "other", "Diger", "Sube", 102));
        await dbContext.SaveChangesAsync();

        var service = new DuyurularService(dbContext, new FixedClock(now));
        var actor = CreateActor(managerId, warehouseNo: 101, canTargetAllWarehouses: true);

        var created = await service.CreateAsync(
            new CreateAnnouncementRequest(
                "Aksam sayim duyurusu",
                "Saat 21:00'de sayim baslayacak.",
                "Onemli",
                "Depo",
                [101],
                null,
                null,
                null,
                actor),
            CancellationToken.None);

        await service.MarkAsReadAsync(created.Id, firstReaderId, 101, CancellationToken.None);
        await service.MarkAsReadAsync(created.Id, secondReaderId, 101, CancellationToken.None);

        var detail = await service.GetForManagementAsync(created.Id, actor, CancellationToken.None);
        var receipts = await service.GetReadReceiptsAsync(created.Id, actor, CancellationToken.None);

        Assert.NotNull(detail.ReadSummary);
        Assert.Equal(2, detail.ReadSummary.ReadCount);
        Assert.Equal(3, detail.ReadSummary.TargetUserCount);
        Assert.Equal(1, detail.ReadSummary.UnreadCount);
        Assert.Equal(2, detail.ReadReceipts.Count);
        Assert.Contains(detail.ReadReceipts, receipt => receipt.UserId == firstReaderId && receipt.Username == "alice");

        Assert.Equal(detail.ReadSummary, receipts.Summary);
        Assert.Equal(2, receipts.Readers.Count);
    }

    [Fact]
    public async Task SearchTargetUsersAsync_FiltersOwnWarehouseWhenActorCanNotTargetAllWarehouses()
    {
        await using var dbContext = CreateAuthDbContext();
        var now = new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc);
        var actorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ownWarehouseUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var otherWarehouseUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        dbContext.Users.AddRange(
            CreateUser(actorUserId, "manager", "Mert", "Yonetici", 101),
            CreateUser(ownWarehouseUserId, "serdal101", "Serdal", "Sube", 101),
            CreateUser(otherWarehouseUserId, "serdal102", "Serdal", "Diger", 102));
        await dbContext.SaveChangesAsync();

        var service = new DuyurularService(dbContext, new FixedClock(now));
        var actor = CreateActor(actorUserId, warehouseNo: 101, canTargetAllWarehouses: false);

        var result = await service.SearchTargetUsersAsync(
            new AnnouncementTargetUserSearchRequest("serdal", null, null, actor),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(ownWarehouseUserId, result.Single().Id);
        Assert.Equal(101, result.Single().WarehouseNo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SearchTargetUsersAsync(
                new AnnouncementTargetUserSearchRequest("serdal", 102, null, actor),
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_AcceptsTurkishAnnouncementOptions()
    {
        await using var dbContext = CreateAuthDbContext();
        var now = new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc);
        var managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        dbContext.Users.Add(CreateUser(managerId, "manager", "Mert", "Yonetici", 101));
        await dbContext.SaveChangesAsync();

        var service = new DuyurularService(dbContext, new FixedClock(now));
        var actor = CreateActor(managerId, warehouseNo: 101, canTargetAllWarehouses: false);

        var created = await service.CreateAsync(
            new CreateAnnouncementRequest(
                "Kasa duyurusu",
                "Kapanis kontrolu yapilacak.",
                "\u00d6nemli",
                "Kullan\u0131c\u0131",
                null,
                [managerId],
                null,
                null,
                actor),
            CancellationToken.None);

        Assert.Equal("Important", created.Priority);
        Assert.Single(created.Targets);
        Assert.Equal("User", created.Targets.Single().Type);
    }

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"duyurular-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private static AppUser CreateUser(
        Guid id,
        string username,
        string firstName,
        string lastName,
        int warehouseNo,
        bool isActive = true) =>
        new(
            id,
            username,
            $"{username}@example.local",
            firstName,
            lastName,
            warehouseNo.ToString(),
            $"Depo {warehouseNo}",
            "hash",
            isActive,
            new DateTime(2026, 7, 30, 7, 0, 0, DateTimeKind.Utc));

    private static AnnouncementActorContext CreateActor(
        Guid userId,
        int warehouseNo,
        bool canTargetAllWarehouses) =>
        new(
            userId,
            "manager",
            "Mert Yonetici",
            warehouseNo,
            $"Depo {warehouseNo}",
            canTargetAllWarehouses);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
