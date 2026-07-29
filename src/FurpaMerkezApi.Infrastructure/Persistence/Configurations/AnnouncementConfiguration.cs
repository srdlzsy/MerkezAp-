using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements");

        builder.HasKey(announcement => announcement.Id);

        builder.Property(announcement => announcement.Id)
            .HasColumnName("id");

        builder.Property(announcement => announcement.Title)
            .HasColumnName("title")
            .HasMaxLength(140)
            .IsRequired();

        builder.Property(announcement => announcement.Message)
            .HasColumnName("message")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(announcement => announcement.Priority)
            .HasColumnName("priority")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(announcement => announcement.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(announcement => announcement.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(announcement => announcement.CreatedByUsername)
            .HasColumnName("created_by_username")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(announcement => announcement.CreatedByFullName)
            .HasColumnName("created_by_full_name")
            .HasMaxLength(201)
            .IsRequired();

        builder.Property(announcement => announcement.StartsAtUtc)
            .HasColumnName("starts_at_utc");

        builder.Property(announcement => announcement.ExpiresAtUtc)
            .HasColumnName("expires_at_utc");

        builder.Property(announcement => announcement.PublishedAtUtc)
            .HasColumnName("published_at_utc")
            .IsRequired();

        builder.Property(announcement => announcement.ArchivedAtUtc)
            .HasColumnName("archived_at_utc");

        builder.Property(announcement => announcement.ArchivedByUserId)
            .HasColumnName("archived_by_user_id");

        builder.Property(announcement => announcement.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(announcement => announcement.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(announcement => announcement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(announcement => announcement.ArchivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(announcement => announcement.Targets)
            .WithOne(target => target.Announcement)
            .HasForeignKey(target => target.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(announcement => announcement.Reads)
            .WithOne(read => read.Announcement)
            .HasForeignKey(read => read.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(announcement => new { announcement.Status, announcement.PublishedAtUtc })
            .HasDatabaseName("ix_announcements_status_published_at");

        builder.HasIndex(announcement => announcement.CreatedByUserId)
            .HasDatabaseName("ix_announcements_created_by_user_id");
    }
}
