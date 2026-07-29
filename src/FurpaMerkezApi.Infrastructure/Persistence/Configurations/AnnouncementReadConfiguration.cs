using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.ToTable("announcement_reads");

        builder.HasKey(read => new { read.AnnouncementId, read.UserId });

        builder.Property(read => read.AnnouncementId)
            .HasColumnName("announcement_id");

        builder.Property(read => read.UserId)
            .HasColumnName("user_id");

        builder.Property(read => read.ReadAtUtc)
            .HasColumnName("read_at_utc")
            .IsRequired();

        builder.HasOne(read => read.User)
            .WithMany()
            .HasForeignKey(read => read.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(read => new { read.UserId, read.ReadAtUtc })
            .HasDatabaseName("ix_announcement_reads_user_read_at");
    }
}
