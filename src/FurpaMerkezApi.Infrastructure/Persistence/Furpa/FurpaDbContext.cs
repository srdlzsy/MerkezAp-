using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa;

public sealed class FurpaDbContext(DbContextOptions<FurpaDbContext> options) : DbContext(options)
{
    public DbSet<LabelDocumentEntity> LabelDocuments => Set<LabelDocumentEntity>();

    public DbSet<LabelDocumentDetailEntity> LabelDocumentDetails => Set<LabelDocumentDetailEntity>();

    public DbSet<TagViewRow> Tags => Set<TagViewRow>();

    public DbSet<CashierEntity> Cashiers => Set<CashierEntity>();

    public DbSet<CashRegistryDetailEntity> CashRegistryDetails => Set<CashRegistryDetailEntity>();

    public DbSet<BranchDetailEntity> BranchDetails => Set<BranchDetailEntity>();

    public DbSet<AuthorizationFileEntity> AuthorizationFiles => Set<AuthorizationFileEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LabelDocumentEntity>(entity =>
        {
            entity.ToTable("LabelDocuments");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Id).ValueGeneratedOnAdd();
            entity.Property(document => document.CreateDate).HasColumnType("datetime");

            entity.HasMany(document => document.Details)
                .WithOne(detail => detail.Document)
                .HasForeignKey(detail => detail.DocumentId);
        });

        modelBuilder.Entity<LabelDocumentDetailEntity>(entity =>
        {
            entity.ToTable("LabelDocumentDetails");
            entity.HasKey(detail => detail.DetailId);
            entity.Property(detail => detail.DetailId).ValueGeneratedOnAdd();
            entity.Property(detail => detail.ProductCode).HasMaxLength(25);
        });

        modelBuilder.Entity<TagViewRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("VwKunyeNet");
        });

        modelBuilder.Entity<CashierEntity>(entity =>
        {
            entity.ToTable("Cashiers");
            entity.HasKey(item => item.CashierId);
            entity.Property(item => item.CashierName).HasMaxLength(100);
            entity.Property(item => item.CashierPassword).HasMaxLength(100);
            entity.Property(item => item.CashierAuthorization).HasMaxLength(100);
            entity.Property(item => item.CreateDate).HasColumnType("datetime");
            entity.Property(item => item.UpdateDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<CashRegistryDetailEntity>(entity =>
        {
            entity.ToTable("CashRegistryDetails");
            entity.HasKey(item => item.DetailId);
        });

        modelBuilder.Entity<BranchDetailEntity>(entity =>
        {
            entity.ToTable("BranchDetails");
            entity.HasKey(item => item.BranchNo);
            entity.Property(item => item.BranchIpAddress).HasMaxLength(100);
            entity.Property(item => item.PosGenelFolderPath).HasMaxLength(255);
            entity.Property(item => item.PoskonFolderPath).HasMaxLength(255);
            entity.Property(item => item.BranchScalesFolderPath).HasMaxLength(255);
        });

        modelBuilder.Entity<AuthorizationFileEntity>(entity =>
        {
            entity.ToTable("AuthorizationFiles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100);
            entity.Property(item => item.UpdateDate).HasColumnType("datetime");
        });
    }
}
