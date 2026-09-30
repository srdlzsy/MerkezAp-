using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class EDespatchSubmissionConfiguration : IEntityTypeConfiguration<EDespatchSubmission>
{
    public void Configure(EntityTypeBuilder<EDespatchSubmission> builder)
    {
        builder.ToTable("edespatch_submissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DocumentKey).HasColumnName("document_key").HasMaxLength(180);
        builder.Property(x => x.DocumentNo).HasColumnName("document_no").HasMaxLength(50);
        builder.Property(x => x.Uuid).HasColumnName("uuid").HasMaxLength(50);
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.HasIndex(x => x.DocumentKey).IsUnique();
        builder.HasIndex(x => x.DocumentNo).IsUnique();
        builder.HasIndex(x => x.Uuid).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
    }
}
