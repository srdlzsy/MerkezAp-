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

    public DbSet<FeedbackItem> FeedbackItems => Set<FeedbackItem>();

    public DbSet<DocumentFlow> DocumentFlows => Set<DocumentFlow>();

    public DbSet<DocumentFlowEvent> DocumentFlowEvents => Set<DocumentFlowEvent>();

    public DbSet<StockAnomaly> StockAnomalies => Set<StockAnomaly>();

    public DbSet<StockAnomalyEvent> StockAnomalyEvents => Set<StockAnomalyEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuthDbContext).Assembly,
            type => type != typeof(Configurations.UyumsoftInboxInvoiceConfiguration));
        new Configurations.UyumsoftInboxInvoiceConfiguration(IsSqlServerProvider())
            .Configure(modelBuilder.Entity<UyumsoftInboxInvoice>());
        base.OnModelCreating(modelBuilder);
    }

    private bool IsSqlServerProvider() =>
        Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
}
