using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TrendyolGoBranchPosPriceSyncTaskConfiguration : IEntityTypeConfiguration<TrendyolGoBranchPosPriceSyncTask>
{
    public void Configure(EntityTypeBuilder<TrendyolGoBranchPosPriceSyncTask> builder)
    {
        builder.ToTable("trendyol_go_branch_pos_price_sync_tasks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StoreId).HasColumnName("store_id");
        builder.Property(item => item.WarehouseNo).HasColumnName("warehouse_no");
        builder.Property(item => item.PayloadJson).HasColumnName("payload_json").HasColumnType("nvarchar(max)");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(item => item.AttemptCount).HasColumnName("attempt_count");
        builder.Property(item => item.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(item => item.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.HasIndex(item => new { item.Status, item.NextAttemptAtUtc });
    }
}
