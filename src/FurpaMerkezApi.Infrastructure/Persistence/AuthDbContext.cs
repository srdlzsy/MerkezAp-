using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<AppRole> Roles => Set<AppRole>();

    public DbSet<AppPermission> Permissions => Set<AppPermission>();

    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();

    public DbSet<AppRolePermission> RolePermissions => Set<AppRolePermission>();

    public DbSet<MobileOfflineSyncRequest> MobileOfflineSyncRequests => Set<MobileOfflineSyncRequest>();

    public DbSet<UyumsoftInboxInvoice> UyumsoftInboxInvoices => Set<UyumsoftInboxInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
