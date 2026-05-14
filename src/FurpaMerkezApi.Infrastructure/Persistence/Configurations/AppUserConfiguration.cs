using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id");

        builder.Property(user => user.Username)
            .HasColumnName("username")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.NormalizedUsername)
            .HasColumnName("normalized_username")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.WarehouseNo)
            .HasColumnName("warehouse_no")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.WarehouseName)
            .HasColumnName("warehouse_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(user => user.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName("ux_app_users_normalized_username");

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ux_app_users_normalized_email");

        builder.HasData(AuthSeedData.AdministratorUser);
    }
}
