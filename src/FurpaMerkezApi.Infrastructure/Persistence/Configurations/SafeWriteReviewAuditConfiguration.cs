using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class SafeWriteReviewAuditConfiguration : IEntityTypeConfiguration<SafeWriteReviewAudit>
{
    public void Configure(EntityTypeBuilder<SafeWriteReviewAudit> builder)
    {
        builder.ToTable("safe_write_review_audits");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SafeWriteId).HasColumnName("safe_write_id").IsRequired();
        builder.Property(item => item.Action).HasColumnName("action").HasMaxLength(30).IsRequired();
        builder.Property(item => item.Outcome).HasColumnName("outcome").HasMaxLength(40).IsRequired();
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(item => item.ReviewedByUserId).HasColumnName("reviewed_by_user_id").IsRequired();
        builder.Property(item => item.ReviewedAtUtc).HasColumnName("reviewed_at_utc").IsRequired();
        builder.Property(item => item.PreviousStatus).HasColumnName("previous_status").HasMaxLength(20).IsRequired();
        builder.Property(item => item.PreviousErrorCode).HasColumnName("previous_error_code").HasMaxLength(100);
        builder.Property(item => item.ResultStatus).HasColumnName("result_status").HasMaxLength(20).IsRequired();
        builder.Property(item => item.ResultErrorCode).HasColumnName("result_error_code").HasMaxLength(100);
        builder.Property(item => item.Message).HasColumnName("message").HasMaxLength(1000);

        builder.HasOne<MobileOfflineSyncRequest>()
            .WithMany()
            .HasForeignKey(item => item.SafeWriteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.SafeWriteId, item.ReviewedAtUtc })
            .HasDatabaseName("ix_safe_write_review_audits_write_reviewed");
        builder.HasIndex(item => item.ReviewedAtUtc)
            .HasDatabaseName("ix_safe_write_review_audits_reviewed_at");
    }
}
