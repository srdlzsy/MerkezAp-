using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("app_roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .HasColumnName("id");

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasColumnName("description")
            .HasMaxLength(250);

        builder.Property(role => role.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(role => role.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(role => role.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasIndex(role => role.Name)
            .IsUnique()
            .HasDatabaseName("ux_app_roles_name");

        builder.HasData(AuthSeedData.AdministratorRole);
    }
}
