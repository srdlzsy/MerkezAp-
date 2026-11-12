using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TrendyolGoRuntimeSettingConfiguration : IEntityTypeConfiguration<TrendyolGoRuntimeSetting>
{
    public void Configure(EntityTypeBuilder<TrendyolGoRuntimeSetting> builder)
    {
        builder.ToTable("trendyol_go_runtime_settings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.PriceListNo).HasColumnName("price_list_no");
        builder.Property(item => item.PaymentPlanNo).HasColumnName("payment_plan_no");
        builder.Property(item => item.StockReserveQuantity).HasColumnName("stock_reserve_quantity");
        builder.Property(item => item.BranchPosPriceSyncEnabled).HasColumnName("branch_pos_price_sync_enabled");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
