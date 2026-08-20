using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B;

public sealed class FurpaB2BDbContext(DbContextOptions<FurpaB2BDbContext> options) : DbContext(options)
{
    public DbSet<B2BBulletinEntity> Bulletins => Set<B2BBulletinEntity>();

    public DbSet<B2BUserEntity> Users => Set<B2BUserEntity>();

    public DbSet<B2BUserAccountEntity> UserAccounts => Set<B2BUserAccountEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<B2BBulletinEntity>(entity =>
        {
            entity.ToTable("Bultens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(item => item.BultenDefination).HasColumnType("nvarchar(max)");
            entity.Property(item => item.BultenLink).HasColumnType("nvarchar(max)");
            entity.Property(item => item.BultenCreateDate).HasColumnType("datetime2");
        });

        modelBuilder.Entity<B2BUserEntity>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserFullName).HasMaxLength(70);
            entity.Property(item => item.UserMail).HasMaxLength(150);
            entity.Property(item => item.UserPasswordSalt).HasColumnType("varbinary(max)");
            entity.Property(item => item.UserPasswordHash).HasColumnType("varbinary(max)");
            entity.Property(item => item.Menus).HasColumnType("nvarchar(max)");
            entity.Property(item => item.CreateDate).HasColumnType("datetime2");
            entity.Property(item => item.UserEndDate).HasColumnType("datetime2");
        });

        modelBuilder.Entity<B2BUserAccountEntity>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(item => item.Category).HasColumnType("nvarchar(max)");
        });
    }
}
