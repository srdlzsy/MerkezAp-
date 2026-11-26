using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class GreenGrocerOrderLineSnapshotConfiguration : IEntityTypeConfiguration<GreenGrocerOrderLineSnapshot>
{
    public void Configure(EntityTypeBuilder<GreenGrocerOrderLineSnapshot> builder)
    {
        builder.ToTable("green_grocer_order_line_snapshots");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id).HasColumnName("id");
        builder.Property(snapshot => snapshot.WarehouseOrderLineGuid).HasColumnName("warehouse_order_line_guid").IsRequired();
        builder.Property(snapshot => snapshot.DocumentSerie).HasColumnName("document_serie").HasMaxLength(20).IsRequired();
        builder.Property(snapshot => snapshot.DocumentOrderNo).HasColumnName("document_order_no").IsRequired();
        builder.Property(snapshot => snapshot.RowNo).HasColumnName("row_no").IsRequired();
        builder.Property(snapshot => snapshot.OrderDate).HasColumnName("order_date").IsRequired();
        builder.Property(snapshot => snapshot.SourceWarehouseNo).HasColumnName("source_warehouse_no").IsRequired();
        builder.Property(snapshot => snapshot.TargetWarehouseNo).HasColumnName("target_warehouse_no").IsRequired();
        builder.Property(snapshot => snapshot.StockCode).HasColumnName("stock_code").HasMaxLength(25).IsRequired();
        builder.Property(snapshot => snapshot.InputQuantity).HasColumnName("input_quantity").IsRequired();
        builder.Property(snapshot => snapshot.InputMode).HasColumnName("input_mode").HasMaxLength(40).IsRequired();
        builder.Property(snapshot => snapshot.ConversionMode).HasColumnName("conversion_mode").HasMaxLength(60).IsRequired();
        builder.Property(snapshot => snapshot.AverageKgPerCase).HasColumnName("average_kg_per_case");
        builder.Property(snapshot => snapshot.UnitsPerCase).HasColumnName("units_per_case");
        builder.Property(snapshot => snapshot.EstimatedQuantity).HasColumnName("estimated_quantity").IsRequired();
        builder.Property(snapshot => snapshot.MicroUnit).HasColumnName("micro_unit").HasMaxLength(20).IsRequired();
        builder.Property(snapshot => snapshot.AverageSource).HasColumnName("average_source").HasMaxLength(60).IsRequired();
        builder.Property(snapshot => snapshot.AverageRecordCount).HasColumnName("average_record_count");
        builder.Property(snapshot => snapshot.AverageCaseCount).HasColumnName("average_case_count");
        builder.Property(snapshot => snapshot.CoefficientOfVariation).HasColumnName("coefficient_of_variation");
        builder.Property(snapshot => snapshot.Confidence).HasColumnName("confidence").HasMaxLength(30).IsRequired();
        builder.Property(snapshot => snapshot.RequiresManualApproval).HasColumnName("requires_manual_approval").IsRequired();
        builder.Property(snapshot => snapshot.AllowOrderLinking).HasColumnName("allow_order_linking").IsRequired();
        builder.Property(snapshot => snapshot.OverDeliveryTolerancePercent).HasColumnName("over_delivery_tolerance_percent").IsRequired();
        builder.Property(snapshot => snapshot.SevereOverDeliveryMultiplier).HasColumnName("severe_over_delivery_multiplier").IsRequired();
        builder.Property(snapshot => snapshot.CalculationTier).HasColumnName("calculation_tier").HasMaxLength(10);
        builder.Property(snapshot => snapshot.AverageWindowDaysUsed).HasColumnName("average_window_days_used");
        builder.Property(snapshot => snapshot.LabelAgeDays).HasColumnName("label_age_days");
        builder.Property(snapshot => snapshot.PriorAverageKgPerCase).HasColumnName("prior_average_kg_per_case");
        builder.Property(snapshot => snapshot.DriftPercent).HasColumnName("drift_percent");
        builder.Property(snapshot => snapshot.ManualApprovalConfirmed).HasColumnName("manual_approval_confirmed").HasDefaultValue(false).IsRequired();
        builder.Property(snapshot => snapshot.ApprovedKgPerCase).HasColumnName("approved_kg_per_case");
        builder.Property(snapshot => snapshot.ActualShippedQuantity).HasColumnName("actual_shipped_quantity");
        builder.Property(snapshot => snapshot.ActualShippedCaseCount).HasColumnName("actual_shipped_case_count");
        builder.Property(snapshot => snapshot.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(snapshot => snapshot.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(snapshot => snapshot.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(snapshot => snapshot.UpdatedByUserId).HasColumnName("updated_by_user_id");
        builder.Property(snapshot => snapshot.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(snapshot => snapshot.WarehouseOrderLineGuid)
            .IsUnique()
            .HasDatabaseName("ux_green_grocer_order_snapshots_order_line_guid");

        builder.HasIndex(snapshot => new { snapshot.OrderDate, snapshot.TargetWarehouseNo, snapshot.StockCode })
            .HasDatabaseName("ix_green_grocer_order_snapshots_date_target_stock");

        builder.HasIndex(snapshot => new { snapshot.DocumentSerie, snapshot.DocumentOrderNo })
            .HasDatabaseName("ix_green_grocer_order_snapshots_document");
    }
}
