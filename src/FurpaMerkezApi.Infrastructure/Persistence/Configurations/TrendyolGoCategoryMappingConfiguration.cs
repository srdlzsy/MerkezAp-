using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TrendyolGoCategoryMappingConfiguration : IEntityTypeConfiguration<TrendyolGoCategoryMapping>
{
    public void Configure(EntityTypeBuilder<TrendyolGoCategoryMapping> builder)
    {
        builder.ToTable("trendyol_go_category_mappings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.MappingKey).HasColumnName("mapping_key").HasMaxLength(250);
        builder.Property(item => item.MikroCategoryCode).HasColumnName("mikro_category_code").HasMaxLength(100);
        builder.Property(item => item.MikroMainGroupCode).HasColumnName("mikro_main_group_code").HasMaxLength(100);
        builder.Property(item => item.CategoryId).HasColumnName("category_id");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
        builder.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(item => item.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasIndex(item => item.MappingKey).IsUnique();
        builder.HasIndex(item => new { item.IsActive, item.CategoryId });
    }
}
