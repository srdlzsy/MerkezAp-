using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class MikroApiWriteAuditConfiguration : IEntityTypeConfiguration<MikroApiWriteAudit>
{
    public void Configure(EntityTypeBuilder<MikroApiWriteAudit> builder)
    {
        builder.ToTable("mikro_api_write_audits");
        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id).HasColumnName("id");
        builder.Property(audit => audit.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(audit => audit.DocumentFlowId).HasColumnName("document_flow_id");
        builder.Property(audit => audit.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.Endpoint).HasColumnName("endpoint").HasMaxLength(500).IsRequired();
        builder.Property(audit => audit.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
        builder.Property(audit => audit.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(audit => audit.HttpStatusCode).HasColumnName("http_status_code");
        builder.Property(audit => audit.MikroStatusCode).HasColumnName("mikro_status_code");
        builder.Property(audit => audit.Response).HasColumnName("response").HasMaxLength(8000);
        builder.Property(audit => audit.Error).HasColumnName("error").HasMaxLength(2000);
        builder.Property(audit => audit.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(audit => audit.ElapsedMilliseconds).HasColumnName("elapsed_milliseconds");
        builder.Property(audit => audit.RecoveredDocumentNo).HasColumnName("recovered_document_no").HasMaxLength(100);
        builder.Property(audit => audit.RecoveredGuid).HasColumnName("recovered_guid");
        builder.Property(audit => audit.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(audit => audit.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(audit => audit.RecoveredAtUtc).HasColumnName("recovered_at_utc");

        builder.HasOne<DocumentFlow>()
            .WithMany()
            .HasForeignKey(audit => audit.DocumentFlowId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(audit => audit.RequestId).IsUnique().HasDatabaseName("ux_mikro_api_write_audits_request_id");
        builder.HasIndex(audit => new { audit.Status, audit.CreatedAtUtc })
            .HasDatabaseName("ix_mikro_api_write_audits_status_created");
        builder.HasIndex(audit => new { audit.DocumentFlowId, audit.CreatedAtUtc })
            .HasDatabaseName("ix_mikro_api_write_audits_flow_created");
        builder.HasIndex(audit => audit.CorrelationId)
            .HasDatabaseName("ix_mikro_api_write_audits_correlation_id");
    }
}
