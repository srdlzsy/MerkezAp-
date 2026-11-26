using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class GreenGrocerShipmentLineRecordConfiguration
    : IEntityTypeConfiguration<GreenGrocerShipmentLineRecord>
{
    public void Configure(EntityTypeBuilder<GreenGrocerShipmentLineRecord> builder)
    {
        builder.ToTable("green_grocer_shipment_line_records");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.ClientRequestId).HasColumnName("client_request_id").IsRequired();
        builder.Property(record => record.OrderLineSnapshotId).HasColumnName("order_line_snapshot_id").IsRequired();
        builder.Property(record => record.WarehouseOrderLineGuid).HasColumnName("warehouse_order_line_guid").IsRequired();
        builder.Property(record => record.DocumentSerie).HasColumnName("document_serie").HasMaxLength(20).IsRequired();
        builder.Property(record => record.DocumentOrderNo).HasColumnName("document_order_no").IsRequired();
        builder.Property(record => record.ActualQuantity).HasColumnName("actual_quantity").IsRequired();
        builder.Property(record => record.ActualCaseCount).HasColumnName("actual_case_count");
        builder.Property(record => record.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<GreenGrocerOrderLineSnapshot>()
            .WithMany()
            .HasForeignKey(record => record.OrderLineSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => new { record.ClientRequestId, record.WarehouseOrderLineGuid })
            .IsUnique()
            .HasDatabaseName("ux_green_grocer_shipment_records_request_line");

        builder.HasIndex(record => record.OrderLineSnapshotId)
            .HasDatabaseName("ix_green_grocer_shipment_records_snapshot");
    }
}
