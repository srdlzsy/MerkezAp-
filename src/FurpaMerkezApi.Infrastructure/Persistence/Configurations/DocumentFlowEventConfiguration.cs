using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class DocumentFlowEventConfiguration : IEntityTypeConfiguration<DocumentFlowEvent>
{
    public void Configure(EntityTypeBuilder<DocumentFlowEvent> builder)
    {
        builder.ToTable("document_flow_events");
        builder.HasKey(flowEvent => flowEvent.Id);

        builder.Property(flowEvent => flowEvent.Id).HasColumnName("id");
        builder.Property(flowEvent => flowEvent.DocumentFlowId).HasColumnName("document_flow_id").IsRequired();
        builder.Property(flowEvent => flowEvent.Step).HasColumnName("step").HasMaxLength(40).HasConversion<string>().IsRequired();
        builder.Property(flowEvent => flowEvent.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(flowEvent => flowEvent.Message).HasColumnName("message").HasMaxLength(500).IsRequired();
        builder.Property(flowEvent => flowEvent.Error).HasColumnName("error").HasMaxLength(2000);
        builder.Property(flowEvent => flowEvent.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(flowEvent => flowEvent.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();

        builder.HasIndex(flowEvent => new { flowEvent.DocumentFlowId, flowEvent.OccurredAtUtc })
            .HasDatabaseName("ix_document_flow_events_flow_occurred");
    }
}
