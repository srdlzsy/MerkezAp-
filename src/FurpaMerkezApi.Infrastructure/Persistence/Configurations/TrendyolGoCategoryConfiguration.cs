using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TrendyolGoCategoryConfiguration : IEntityTypeConfiguration<TrendyolGoCategory>
{
    public void Configure(EntityTypeBuilder<TrendyolGoCategory> builder)
    {
        builder.ToTable("trendyol_go_categories");
        builder.HasKey(item => item.CategoryId);
        builder.Property(item => item.CategoryId).HasColumnName("category_id").ValueGeneratedNever();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(300);
        builder.Property(item => item.ParentCategoryId).HasColumnName("parent_category_id");
        builder.Property(item => item.Path).HasColumnName("path").HasMaxLength(1500);
        builder.Property(item => item.IsLeaf).HasColumnName("is_leaf");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.SyncedAtUtc).HasColumnName("synced_at_utc");
        builder.HasIndex(item => new { item.IsActive, item.IsLeaf, item.Name });
    }
}
