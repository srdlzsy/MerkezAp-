using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TrendyolGoProductCategoryOverrideConfiguration : IEntityTypeConfiguration<TrendyolGoProductCategoryOverride>
{
    public void Configure(EntityTypeBuilder<TrendyolGoProductCategoryOverride> builder)
    {
        builder.ToTable("trendyol_go_product_category_overrides");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.StockCode).HasColumnName("stock_code").HasMaxLength(100);
        builder.Property(item => item.CategoryId).HasColumnName("category_id");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(item => item.StockCode).IsUnique();
        builder.HasIndex(item => new { item.IsActive, item.CategoryId });
    }
}
