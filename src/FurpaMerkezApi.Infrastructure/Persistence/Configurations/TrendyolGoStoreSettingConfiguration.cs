using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TrendyolGoStoreSettingConfiguration : IEntityTypeConfiguration<TrendyolGoStoreSetting>
{
    public void Configure(EntityTypeBuilder<TrendyolGoStoreSetting> builder)
    {
        builder.ToTable("trendyol_go_store_settings");
        builder.HasKey(item => item.StoreId);
        builder.Property(item => item.StoreId).HasColumnName("store_id").ValueGeneratedNever();
        builder.Property(item => item.WarehouseNo).HasColumnName("warehouse_no");
        builder.Property(item => item.StoreName).HasColumnName("store_name").HasMaxLength(300);
        builder.Property(item => item.StockReserveQuantity).HasColumnName("stock_reserve_quantity");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(item => item.WarehouseNo)
            .IsUnique()
            .HasFilter("[is_active] = 1");
        builder.HasIndex(item => item.IsActive);
    }
}
