using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class FeedbackItemConfiguration : IEntityTypeConfiguration<FeedbackItem>
{
    public void Configure(EntityTypeBuilder<FeedbackItem> builder)
    {
        builder.ToTable("feedback_items");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.Type)
            .HasColumnName("type")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(item => item.Title)
            .HasColumnName("title")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(item => item.Message)
            .HasColumnName("message")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(item => item.Priority)
            .HasColumnName("priority")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(item => item.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(item => item.CreatedByUsername)
            .HasColumnName("created_by_username")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(item => item.CreatedByFullName)
            .HasColumnName("created_by_full_name")
            .HasMaxLength(201)
            .IsRequired();

        builder.Property(item => item.WarehouseNo)
            .HasColumnName("warehouse_no")
            .IsRequired();

        builder.Property(item => item.WarehouseName)
            .HasColumnName("warehouse_name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.AdminNote)
            .HasColumnName("admin_note")
            .HasMaxLength(1000);

        builder.Property(item => item.ReadAtUtc)
            .HasColumnName("read_at_utc");

        builder.Property(item => item.ReadByUserId)
            .HasColumnName("read_by_user_id");

        builder.Property(item => item.StatusChangedAtUtc)
            .HasColumnName("status_changed_at_utc");

        builder.Property(item => item.StatusChangedByUserId)
            .HasColumnName("status_changed_by_user_id");

        builder.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.Property(item => item.ClosedAtUtc)
            .HasColumnName("closed_at_utc");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.ReadByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.StatusChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.CreatedAtUtc)
            .HasDatabaseName("ix_feedback_items_created_at_utc");

        builder.HasIndex(item => new { item.WarehouseNo, item.Status, item.CreatedAtUtc })
            .HasDatabaseName("ix_feedback_items_warehouse_status_created");

        builder.HasIndex(item => item.CreatedByUserId)
            .HasDatabaseName("ix_feedback_items_created_by_user_id");
    }
}
