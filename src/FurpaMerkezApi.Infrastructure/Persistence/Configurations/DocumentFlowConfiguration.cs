using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class DocumentFlowConfiguration : IEntityTypeConfiguration<DocumentFlow>
{
    public void Configure(EntityTypeBuilder<DocumentFlow> builder)
    {
        builder.ToTable("document_flows");
        builder.HasKey(flow => flow.Id);

        builder.Property(flow => flow.Id).HasColumnName("id");
        builder.Property(flow => flow.FlowKey).HasColumnName("flow_key").HasMaxLength(180).IsRequired();
        builder.Property(flow => flow.DocumentType).HasColumnName("document_type").HasMaxLength(40).HasConversion<string>().IsRequired();
        builder.Property(flow => flow.SourceWarehouseNo).HasColumnName("source_warehouse_no").IsRequired();
        builder.Property(flow => flow.TargetWarehouseNo).HasColumnName("target_warehouse_no");
        builder.Property(flow => flow.DocumentSerie).HasColumnName("document_serie").HasMaxLength(20).IsRequired();
        builder.Property(flow => flow.DocumentOrderNo).HasColumnName("document_order_no").IsRequired();
        builder.Property(flow => flow.DocumentNo).HasColumnName("document_no").HasMaxLength(50);
        builder.Property(flow => flow.ExternalDocumentNo).HasColumnName("external_document_no").HasMaxLength(50);
        builder.Property(flow => flow.ExternalUuid).HasColumnName("external_uuid").HasMaxLength(50);
        builder.Property(flow => flow.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(flow => flow.CurrentStep).HasColumnName("current_step").HasMaxLength(40).HasConversion<string>().IsRequired();
        builder.Property(flow => flow.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(flow => flow.LastChangedByUserId).HasColumnName("last_changed_by_user_id");
        builder.Property(flow => flow.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(flow => flow.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasMany(flow => flow.Events)
            .WithOne()
            .HasForeignKey(flowEvent => flowEvent.DocumentFlowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(flow => flow.FlowKey).IsUnique().HasDatabaseName("ux_document_flows_flow_key");
        builder.HasIndex(flow => new { flow.SourceWarehouseNo, flow.UpdatedAtUtc })
            .HasDatabaseName("ix_document_flows_source_warehouse_updated");
        builder.HasIndex(flow => new { flow.TargetWarehouseNo, flow.UpdatedAtUtc })
            .HasDatabaseName("ix_document_flows_target_warehouse_updated");
        builder.HasIndex(flow => new { flow.Status, flow.UpdatedAtUtc })
            .HasDatabaseName("ix_document_flows_status_updated");
    }
}
