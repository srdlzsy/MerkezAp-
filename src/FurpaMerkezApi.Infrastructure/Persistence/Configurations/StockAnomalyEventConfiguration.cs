using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class StockAnomalyEventConfiguration : IEntityTypeConfiguration<StockAnomalyEvent>
{
    public void Configure(EntityTypeBuilder<StockAnomalyEvent> builder)
    {
        builder.ToTable("stock_anomaly_events");
        builder.HasKey(anomalyEvent => anomalyEvent.Id);

        builder.Property(anomalyEvent => anomalyEvent.Id).HasColumnName("id");
        builder.Property(anomalyEvent => anomalyEvent.StockAnomalyId).HasColumnName("stock_anomaly_id").IsRequired();
        builder.Property(anomalyEvent => anomalyEvent.EventType).HasColumnName("event_type").HasMaxLength(40).HasConversion<string>().IsRequired();
        builder.Property(anomalyEvent => anomalyEvent.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(anomalyEvent => anomalyEvent.Message).HasColumnName("message").HasMaxLength(500).IsRequired();
        builder.Property(anomalyEvent => anomalyEvent.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(anomalyEvent => anomalyEvent.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();

        builder.HasIndex(anomalyEvent => new { anomalyEvent.StockAnomalyId, anomalyEvent.OccurredAtUtc })
            .HasDatabaseName("ix_stock_anomaly_events_anomaly_occurred");
    }
}
