using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class StockAnomalyConfiguration : IEntityTypeConfiguration<StockAnomaly>
{
    public void Configure(EntityTypeBuilder<StockAnomaly> builder)
    {
        builder.ToTable("stock_anomalies");
        builder.HasKey(anomaly => anomaly.Id);

        builder.Property(anomaly => anomaly.Id).HasColumnName("id");
        builder.Property(anomaly => anomaly.SourceKey).HasColumnName("source_key").HasMaxLength(220).IsRequired();
        builder.Property(anomaly => anomaly.Type).HasColumnName("type").HasMaxLength(60).HasConversion<string>().IsRequired();
        builder.Property(anomaly => anomaly.Severity).HasColumnName("severity").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(anomaly => anomaly.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(anomaly => anomaly.WarehouseNo).HasColumnName("warehouse_no").IsRequired();
        builder.Property(anomaly => anomaly.RelatedWarehouseNo).HasColumnName("related_warehouse_no");
        builder.Property(anomaly => anomaly.WarehouseName).HasColumnName("warehouse_name").HasMaxLength(120);
        builder.Property(anomaly => anomaly.RelatedWarehouseName).HasColumnName("related_warehouse_name").HasMaxLength(120);
        builder.Property(anomaly => anomaly.ProductCode).HasColumnName("product_code").HasMaxLength(50);
        builder.Property(anomaly => anomaly.ProductName).HasColumnName("product_name").HasMaxLength(200);
        builder.Property(anomaly => anomaly.DocumentSerie).HasColumnName("document_serie").HasMaxLength(20);
        builder.Property(anomaly => anomaly.DocumentOrderNo).HasColumnName("document_order_no");
        builder.Property(anomaly => anomaly.DocumentNo).HasColumnName("document_no").HasMaxLength(50);
        builder.Property(anomaly => anomaly.MovementGuid).HasColumnName("movement_guid");
        builder.Property(anomaly => anomaly.Quantity).HasColumnName("quantity");
        builder.Property(anomaly => anomaly.ExpectedQuantity).HasColumnName("expected_quantity");
        builder.Property(anomaly => anomaly.ActualQuantity).HasColumnName("actual_quantity");
        builder.Property(anomaly => anomaly.AverageQuantity).HasColumnName("average_quantity");
        builder.Property(anomaly => anomaly.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(anomaly => anomaly.Message).HasColumnName("message").HasMaxLength(500).IsRequired();
        builder.Property(anomaly => anomaly.Evidence).HasColumnName("evidence").HasMaxLength(4000);
        builder.Property(anomaly => anomaly.LastChangedByUserId).HasColumnName("last_changed_by_user_id");
        builder.Property(anomaly => anomaly.FirstDetectedAtUtc).HasColumnName("first_detected_at_utc").IsRequired();
        builder.Property(anomaly => anomaly.LastDetectedAtUtc).HasColumnName("last_detected_at_utc").IsRequired();
        builder.Property(anomaly => anomaly.ResolvedAtUtc).HasColumnName("resolved_at_utc");

        builder.HasMany(anomaly => anomaly.Events)
            .WithOne()
            .HasForeignKey(anomalyEvent => anomalyEvent.StockAnomalyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(anomaly => anomaly.SourceKey).IsUnique().HasDatabaseName("ux_stock_anomalies_source_key");
        builder.HasIndex(anomaly => new { anomaly.WarehouseNo, anomaly.Status, anomaly.LastDetectedAtUtc })
            .HasDatabaseName("ix_stock_anomalies_warehouse_status_last_detected");
        builder.HasIndex(anomaly => new { anomaly.Type, anomaly.Status, anomaly.LastDetectedAtUtc })
            .HasDatabaseName("ix_stock_anomalies_type_status_last_detected");
    }
}
