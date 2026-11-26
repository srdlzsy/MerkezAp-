using System.Security.Cryptography;
using System.Text;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B;
using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B.Models;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.Ayarlar;

public sealed class B2BUserManagementTests
{
    [Fact]
    public async Task CreateB2BUserAsync_CreatesHmacSha512HashAndLinksMikroAccounts()
    {
        await using var contexts = CreateContexts();
        var accountId = Guid.NewGuid();
        contexts.Mikro.CARI_HESAPLARs.Add(new CARI_HESAPLAR
        {
            cari_Guid = accountId,
            cari_kod = "32000100",
            cari_unvan1 = "ORNEK GIDA A.S.",
            cari_unvan2 = "BURSA",
            cari_iptal = false,
            cari_create_date = DateTime.UtcNow
        });
        await contexts.Mikro.SaveChangesAsync();

        var service = CreateService(contexts);
        var endDate = new DateTime(2027, 12, 31, 23, 59, 0);

        var created = await service.CreateB2BUserAsync(
            new CreateB2BUserRequest(
                "Ahmet Yilmaz",
                "ahmet@ornek.com",
                "Gizli123!",
                true,
                "1, 2, 2, 5 ",
                endDate,
                [
                    new B2BUserAccountInput(accountId, "01"),
                    new B2BUserAccountInput(accountId, "01")
                ]),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.UserId);
        Assert.Equal("Ahmet Yilmaz", created.UserFullName);
        Assert.Equal("ahmet@ornek.com", created.UserMail);
        Assert.Equal("1,2,5", created.Menus);
        Assert.Equal(1, created.AccountCount);
        Assert.Equal(["01"], created.Categories);

        var linkedAccount = Assert.Single(created.Accounts);
        Assert.Equal(accountId, linkedAccount.AccountId);
        Assert.Equal("01", linkedAccount.Category);
        Assert.Equal("32000100", linkedAccount.AccountCode);
        Assert.Equal("ORNEK GIDA A.S. BURSA", linkedAccount.AccountTitle);

        var persisted = await contexts.FurpaB2B.Users.SingleAsync(u => u.UserId == created.UserId);
        Assert.Equal(128, persisted.UserPasswordSalt.Length);
        Assert.Equal(64, persisted.UserPasswordHash.Length);
        Assert.True(VerifyPassword("Gizli123!", persisted.UserPasswordSalt, persisted.UserPasswordHash));
        Assert.False(VerifyPassword("YanlisSifre", persisted.UserPasswordSalt, persisted.UserPasswordHash));
    }

    [Fact]
    public async Task CreateB2BUserAsync_ThrowsConflictWhenEmailAlreadyExists()
    {
        await using var contexts = CreateContexts();
        var service = CreateService(contexts);
        var endDate = new DateTime(2027, 12, 31);

        await service.CreateB2BUserAsync(
            new CreateB2BUserRequest("Ilk Kullanici", "test@furpa.com.tr", "Sifre123", true, "1", endDate),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateB2BUserAsync(
                new CreateB2BUserRequest("Ikinci Kullanici", "TEST@FURPA.COM.TR", "Sifre456", true, "1", endDate),
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateB2BUserAsync_RejectsMissingOrInactiveMikroAccount()
    {
        await using var contexts = CreateContexts();
        var inactiveAccountId = Guid.NewGuid();
        contexts.Mikro.CARI_HESAPLARs.Add(new CARI_HESAPLAR
        {
            cari_Guid = inactiveAccountId,
            cari_kod = "32000999",
            cari_unvan1 = "IPTAL CARI",
            cari_iptal = true,
            cari_create_date = DateTime.UtcNow
        });
        await contexts.Mikro.SaveChangesAsync();

        var service = CreateService(contexts);
        var request = new CreateB2BUserRequest(
            "Gecersiz Cari",
            "gecersiz@furpa.com.tr",
            "Parola123",
            true,
            "1",
            new DateTime(2027, 12, 31),
            [new B2BUserAccountInput(inactiveAccountId, "01")]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateB2BUserAsync(request, CancellationToken.None));

        Assert.Contains(inactiveAccountId.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await contexts.FurpaB2B.Users.ToArrayAsync());
        Assert.Empty(await contexts.FurpaB2B.UserAccounts.ToArrayAsync());
    }

    [Fact]
    public async Task UpdateB2BUserAsync_PreservesAccountsWhenNullAndSyncsWhenProvided()
    {
        await using var contexts = CreateContexts();
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        contexts.Mikro.CARI_HESAPLARs.AddRange(
            CreateMikroAccount(accountA, "32000101"),
            CreateMikroAccount(accountB, "32000102"));
        await contexts.Mikro.SaveChangesAsync();
        var service = CreateService(contexts);
        var endDate = new DateTime(2027, 12, 31);

        var created = await service.CreateB2BUserAsync(
            new CreateB2BUserRequest(
                "Mehmet Demir",
                "mehmet@furpa.com.tr",
                "Parola123",
                true,
                "1,2",
                endDate,
                [new B2BUserAccountInput(accountA, "01")]),
            CancellationToken.None);

        var afterNullAccounts = await service.UpdateB2BUserAsync(
            created.UserId,
            new UpdateB2BUserRequest("Mehmet Demir", "mehmet@furpa.com.tr", true, "1,2,3", endDate, Accounts: null),
            CancellationToken.None);

        Assert.Single(afterNullAccounts.Accounts);
        Assert.Equal(accountA, afterNullAccounts.Accounts.First().AccountId);

        var afterSync = await service.UpdateB2BUserAsync(
            created.UserId,
            new UpdateB2BUserRequest(
                "Mehmet Demir",
                "mehmet@furpa.com.tr",
                false,
                "1,3",
                endDate,
                [new B2BUserAccountInput(accountB, "02.01")]),
            CancellationToken.None);

        var syncedAccount = Assert.Single(afterSync.Accounts);
        Assert.Equal(accountB, syncedAccount.AccountId);
        Assert.Equal("02.01", syncedAccount.Category);
        Assert.False(afterSync.Status);
        Assert.Equal(1, afterSync.AccountCount);
    }

    [Fact]
    public async Task UpdateB2BUserAsync_PreservesLegacyAccountButRejectsNewMissingAccount()
    {
        await using var contexts = CreateContexts();
        var legacyAccountId = Guid.NewGuid();
        var missingAccountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var endDate = new DateTime(2027, 12, 31);

        contexts.FurpaB2B.Users.Add(new B2BUserEntity
        {
            UserId = userId,
            UserFullName = "Legacy Kullanici",
            UserMail = "legacy@furpa.com.tr",
            UserPasswordSalt = new byte[128],
            UserPasswordHash = new byte[64],
            Status = true,
            CreateDate = DateTime.UtcNow,
            UserEndDate = endDate
        });
        contexts.FurpaB2B.UserAccounts.Add(new B2BUserAccountEntity
        {
            UserId = userId,
            AccountId = legacyAccountId,
            Category = "01"
        });
        await contexts.FurpaB2B.SaveChangesAsync();

        var service = CreateService(contexts);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateB2BUserAsync(
                userId,
                new UpdateB2BUserRequest(
                    "Legacy Kullanici",
                    "legacy@furpa.com.tr",
                    true,
                    "1",
                    endDate,
                    [
                        new B2BUserAccountInput(legacyAccountId, "02"),
                        new B2BUserAccountInput(missingAccountId, "01")
                    ]),
                CancellationToken.None));

        Assert.Contains(missingAccountId.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
        var persistedAccount = await contexts.FurpaB2B.UserAccounts.SingleAsync();
        Assert.Equal(legacyAccountId, persistedAccount.AccountId);
        Assert.Equal("01", persistedAccount.Category);
    }

    [Fact]
    public async Task ResetB2BUserPasswordAsync_UpdatesSaltAndHash()
    {
        await using var contexts = CreateContexts();
        var service = CreateService(contexts);

        var created = await service.CreateB2BUserAsync(
            new CreateB2BUserRequest("Ayse Kaya", "ayse@furpa.com.tr", "EskiSifre1", true, null, new DateTime(2027, 6, 1)),
            CancellationToken.None);

        await service.ResetB2BUserPasswordAsync(
            created.UserId,
            new ResetB2BUserPasswordRequest("YeniSifre99!"),
            CancellationToken.None);

        var persisted = await contexts.FurpaB2B.Users.SingleAsync(u => u.UserId == created.UserId);
        Assert.False(VerifyPassword("EskiSifre1", persisted.UserPasswordSalt, persisted.UserPasswordHash));
        Assert.True(VerifyPassword("YeniSifre99!", persisted.UserPasswordSalt, persisted.UserPasswordHash));
    }

    [Fact]
    public async Task ListB2BMenusAsync_And_SearchB2BAccountsAsync_ReturnActiveRecords()
    {
        await using var contexts = CreateContexts();
        contexts.FurpaB2B.MenuGroups.AddRange(
            new B2BMenuGroupEntity { Id = 1, MenuGroupName = "Stok Islemleri", MenuGroupIcon = "cubes", Status = true },
            new B2BMenuGroupEntity { Id = 2, MenuGroupName = "Pasif Grup", MenuGroupIcon = "x", Status = false });
        contexts.FurpaB2B.Menus.AddRange(
            new B2BMenuEntity { Id = 1, MenuName = "Satin Alma", MenuRoute = "satinalma", MenuIcon = "", MenuGroupId = 1, Status = true },
            new B2BMenuEntity { Id = 2, MenuName = "Pasif Menu", MenuRoute = "pasif", MenuIcon = "", MenuGroupId = 1, Status = false },
            new B2BMenuEntity { Id = 3, MenuName = "Pasif Grup Menusu", MenuRoute = "pg", MenuIcon = "", MenuGroupId = 2, Status = true });
        await contexts.FurpaB2B.SaveChangesAsync();

        var activeAccountId = Guid.NewGuid();
        contexts.Mikro.CARI_HESAPLARs.AddRange(
            new CARI_HESAPLAR
            {
                cari_Guid = activeAccountId,
                cari_kod = "32009861",
                cari_unvan1 = "ETKIN UNLU MAMULLER",
                cari_vdaire_no = "3810001122",
                cari_iptal = false,
                cari_create_date = DateTime.UtcNow
            },
            new CARI_HESAPLAR
            {
                cari_Guid = Guid.NewGuid(),
                cari_kod = "32009999",
                cari_unvan1 = "IPTAL CARI",
                cari_iptal = true,
                cari_create_date = DateTime.UtcNow
            });
        await contexts.Mikro.SaveChangesAsync();

        var service = CreateService(contexts);

        var menus = await service.ListB2BMenusAsync(CancellationToken.None);
        var menu = Assert.Single(menus);
        Assert.Equal(1, menu.Id);
        Assert.Equal("Stok Islemleri", menu.MenuGroupName);

        var accounts = await service.SearchB2BAccountsAsync("ETKIN", 20, CancellationToken.None);
        var account = Assert.Single(accounts);
        Assert.Equal(activeAccountId, account.AccountId);
        Assert.Equal("32009861", account.AccountCode);
        Assert.Equal("ETKIN UNLU MAMULLER", account.AccountTitle);
    }

    private static bool VerifyPassword(string password, byte[] salt, byte[] expectedHash)
    {
        using var hmac = new HMACSHA512(salt);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(computed, expectedHash);
    }

    private static CARI_HESAPLAR CreateMikroAccount(Guid id, string code) => new()
    {
        cari_Guid = id,
        cari_kod = code,
        cari_unvan1 = $"CARI {code}",
        cari_iptal = false,
        cari_create_date = DateTime.UtcNow
    };

    private static AyarlarService CreateService(TestContexts contexts) =>
        new(contexts.Furpa, contexts.FurpaB2B, contexts.Mikro);

    private static TestContexts CreateContexts()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new TestContexts(
            new FurpaDbContext(new DbContextOptionsBuilder<FurpaDbContext>()
                .UseInMemoryDatabase($"b2b-furpa-{suffix}")
                .Options),
            new FurpaB2BDbContext(new DbContextOptionsBuilder<FurpaB2BDbContext>()
                .UseInMemoryDatabase($"b2b-db-{suffix}")
                .Options),
            new MikroDbContext(new DbContextOptionsBuilder<MikroDbContext>()
                .UseInMemoryDatabase($"b2b-mikro-{suffix}")
                .Options));
    }

    private sealed class TestContexts(
        FurpaDbContext furpa,
        FurpaB2BDbContext furpaB2B,
        MikroDbContext mikro) : IAsyncDisposable
    {
        public FurpaDbContext Furpa { get; } = furpa;
        public FurpaB2BDbContext FurpaB2B { get; } = furpaB2B;
        public MikroDbContext Mikro { get; } = mikro;

        public async ValueTask DisposeAsync()
        {
            await Furpa.DisposeAsync();
            await FurpaB2B.DisposeAsync();
            await Mikro.DisposeAsync();
        }
    }
}
