using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class DespatchDriverConfiguration : IEntityTypeConfiguration<DespatchDriver>
{
    public void Configure(EntityTypeBuilder<DespatchDriver> builder)
    {
        builder.ToTable("despatch_drivers");

        builder.HasKey(driver => driver.Id);

        builder.Property(driver => driver.Id).HasColumnName("id");
        builder.Property(driver => driver.FirstName).HasColumnName("first_name").HasMaxLength(60).IsRequired();
        builder.Property(driver => driver.LastName).HasColumnName("last_name").HasMaxLength(60).IsRequired();
        builder.Property(driver => driver.PlateNumber).HasColumnName("plate_number").HasMaxLength(20).IsRequired();
        builder.Property(driver => driver.Tckn).HasColumnName("tckn").HasMaxLength(11).IsRequired();
        builder.Property(driver => driver.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(driver => driver.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(driver => driver.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(driver => driver.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(driver => driver.UpdatedByUserId).HasColumnName("updated_by_user_id");
        builder.Property(driver => driver.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(driver => driver.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(driver => driver.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(driver => new { driver.IsActive, driver.LastName, driver.FirstName })
            .HasDatabaseName("ix_despatch_drivers_active_name");

        builder.HasIndex(driver => new { driver.IsActive, driver.PlateNumber })
            .HasDatabaseName("ix_despatch_drivers_active_plate");
    }
}
