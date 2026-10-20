using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa;

public sealed class FurpaDbContext(DbContextOptions<FurpaDbContext> options) : DbContext(options)
{
    public DbSet<LabelDocumentEntity> LabelDocuments => Set<LabelDocumentEntity>();

    public DbSet<LabelDocumentDetailEntity> LabelDocumentDetails => Set<LabelDocumentDetailEntity>();

    public DbSet<TagViewRow> Tags => Set<TagViewRow>();

    public DbSet<DeviceDetailEntity> DeviceDetails => Set<DeviceDetailEntity>();

    public DbSet<DeviceTypeEntity> DeviceTypes => Set<DeviceTypeEntity>();

    public DbSet<CashierEntity> Cashiers => Set<CashierEntity>();

    public DbSet<CashierDefinitionEntity> CashierDefinitions => Set<CashierDefinitionEntity>();

    public DbSet<CashRegistryDetailEntity> CashRegistryDetails => Set<CashRegistryDetailEntity>();

    public DbSet<CashRegisterDetailEntity> CashRegisterDetails => Set<CashRegisterDetailEntity>();

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

        modelBuilder.Entity<DeviceDetailEntity>(entity =>
        {
            entity.ToTable("DeviceDetails");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.IpAddress).HasMaxLength(100);
            entity.Property(item => item.Description).HasMaxLength(255);
            entity.HasIndex(item => new { item.BranchNo, item.DeviceTypeId, item.IpAddress });
        });

        modelBuilder.Entity<DeviceTypeEntity>(entity =>
        {
            entity.ToTable("DeviceTypes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.DeviceName).HasMaxLength(100);
        });

        modelBuilder.Entity<CashierEntity>(entity =>
        {
            entity.ToTable("Cashiers");
            entity.HasKey(item => item.CashierId);
            entity.Property(item => item.CashierName).HasMaxLength(100);
            entity.Property(item => item.CashierPassword).HasMaxLength(6);
            entity.Property(item => item.CashierAuthorization).HasMaxLength(1);
            entity.Property(item => item.CreateDate).HasColumnType("datetime");
            entity.Property(item => item.UpdateDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<CashierDefinitionEntity>(entity =>
        {
            entity.ToTable("KASIYER_TANIMLARI");
            entity.HasKey(item => item.CashierId);
            entity.Property(item => item.CashierId).HasColumnName("KasiyerId").ValueGeneratedNever();
            entity.Property(item => item.CreatedByUser).HasColumnName("OlusturanKullanici").HasMaxLength(100);
            entity.Property(item => item.CreatedAt).HasColumnName("OlusturmaTarihi").HasColumnType("datetime");
            entity.Property(item => item.UpdatedByUser).HasColumnName("GuncelleyenKullanici").HasMaxLength(100);
            entity.Property(item => item.UpdatedAt).HasColumnName("GuncellemeTarihi").HasColumnType("datetime");
            entity.Property(item => item.CashierCode).HasColumnName("KasiyerKodu").HasMaxLength(100);
            entity.Property(item => item.FirstName).HasColumnName("KasiyerAdi").HasMaxLength(200);
            entity.Property(item => item.LastName).HasColumnName("KasiyerSoyadi").HasMaxLength(200);
            entity.Property(item => item.Password).HasColumnName("KasiyerSifre").HasMaxLength(200);
            entity.Property(item => item.Authorization).HasColumnName("KasiyerYetki").HasMaxLength(200);
            entity.Property(item => item.IsActive).HasColumnName("KasiyerDurumu");
            entity.Property(item => item.Address).HasColumnName("Adres").HasMaxLength(500);
            entity.Property(item => item.Phone).HasColumnName("Telefon").HasMaxLength(100);
            entity.Property(item => item.PasswordKey).HasColumnName("sifre_anahtari").HasMaxLength(100);
        });

        modelBuilder.Entity<CashRegistryDetailEntity>(entity =>
        {
            entity.ToTable("CashRegistryDetails");
            entity.HasKey(item => item.DetailId);
        });

        modelBuilder.Entity<CashRegisterDetailEntity>(entity =>
        {
            entity.ToTable("CashRegisterDetails");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.CashRegisterNo).HasMaxLength(40);
            entity.Property(item => item.Bank).HasMaxLength(100);
            entity.Property(item => item.TerminalId).HasMaxLength(40);
            entity.Property(item => item.MerchantNo).HasMaxLength(40);
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
            entity.ToTable("AuthorizationFile");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100);
            entity.Property(item => item.UpdateDate).HasColumnType("datetime");
        });
    }
}
