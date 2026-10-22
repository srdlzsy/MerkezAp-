using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AxataSynchronizationDocumentStateConfiguration
    : IEntityTypeConfiguration<AxataSynchronizationDocumentState>
{
    public void Configure(EntityTypeBuilder<AxataSynchronizationDocumentState> builder)
    {
        builder.ToTable("axata_synchronization_document_states");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TaskCode).HasColumnName("task_code").HasMaxLength(100).IsRequired();
        builder.Property(item => item.DocumentKey).HasColumnName("document_key").HasMaxLength(120).IsRequired();
        builder.Property(item => item.FailureCount).HasColumnName("failure_count").IsRequired();
        builder.Property(item => item.LastError).HasColumnName("last_error").HasMaxLength(2000).IsRequired();
        builder.Property(item => item.FirstFailedAtUtc).HasColumnName("first_failed_at_utc").IsRequired();
        builder.Property(item => item.LastFailedAtUtc).HasColumnName("last_failed_at_utc").IsRequired();
        builder.Property(item => item.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired();

        builder.HasIndex(item => new { item.TaskCode, item.DocumentKey })
            .IsUnique()
            .HasDatabaseName("ux_axata_sync_document_state_task_document");
        builder.HasIndex(item => new { item.TaskCode, item.NextAttemptAtUtc })
            .HasDatabaseName("ix_axata_sync_document_state_next_attempt");
    }
}
