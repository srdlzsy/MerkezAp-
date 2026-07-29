using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementTargetConfiguration : IEntityTypeConfiguration<AnnouncementTarget>
{
    public void Configure(EntityTypeBuilder<AnnouncementTarget> builder)
    {
        builder.ToTable("announcement_targets");

        builder.HasKey(target => target.Id);

        builder.Property(target => target.Id)
            .HasColumnName("id");

        builder.Property(target => target.AnnouncementId)
            .HasColumnName("announcement_id")
            .IsRequired();

        builder.Property(target => target.Type)
            .HasColumnName("type")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(target => target.WarehouseNo)
            .HasColumnName("warehouse_no");

        builder.Property(target => target.WarehouseName)
            .HasColumnName("warehouse_name")
            .HasMaxLength(150);

        builder.Property(target => target.UserId)
            .HasColumnName("user_id");

        builder.Property(target => target.Username)
            .HasColumnName("username")
            .HasMaxLength(50);

        builder.Property(target => target.UserFullName)
            .HasColumnName("user_full_name")
            .HasMaxLength(201);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(target => target.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(target => new { target.Type, target.WarehouseNo })
            .HasDatabaseName("ix_announcement_targets_type_warehouse");

        builder.HasIndex(target => target.UserId)
            .HasDatabaseName("ix_announcement_targets_user_id");

        builder.HasIndex(target => target.AnnouncementId)
            .HasDatabaseName("ix_announcement_targets_announcement_id");
    }
}
