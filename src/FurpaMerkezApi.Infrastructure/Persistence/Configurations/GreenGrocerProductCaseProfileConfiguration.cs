using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class GreenGrocerProductCaseProfileConfiguration : IEntityTypeConfiguration<GreenGrocerProductCaseProfile>
{
    public void Configure(EntityTypeBuilder<GreenGrocerProductCaseProfile> builder)
    {
        builder.ToTable("green_grocer_product_case_profiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id).HasColumnName("id");
        builder.Property(profile => profile.StockCode).HasColumnName("stock_code").HasMaxLength(25).IsRequired();
        builder.Property(profile => profile.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(profile => profile.InputMode).HasColumnName("input_mode").HasMaxLength(40).IsRequired();
        builder.Property(profile => profile.ConversionMode).HasColumnName("conversion_mode").HasMaxLength(60).IsRequired();
        builder.Property(profile => profile.ManualKgPerCase).HasColumnName("manual_kg_per_case");
        builder.Property(profile => profile.ManualUnitsPerCase).HasColumnName("manual_units_per_case");
        builder.Property(profile => profile.MinExpectedKgPerCase).HasColumnName("min_expected_kg_per_case");
        builder.Property(profile => profile.MaxExpectedKgPerCase).HasColumnName("max_expected_kg_per_case");
        builder.Property(profile => profile.AverageWindowDays).HasColumnName("average_window_days").IsRequired();
        builder.Property(profile => profile.MinAverageRecordCount).HasColumnName("min_average_record_count").IsRequired();
        builder.Property(profile => profile.MinAverageCaseCount).HasColumnName("min_average_case_count").IsRequired();
        builder.Property(profile => profile.MaxCoefficientOfVariation).HasColumnName("max_coefficient_of_variation").IsRequired();
        builder.Property(profile => profile.RequiresManualApproval).HasColumnName("requires_manual_approval").IsRequired();
        builder.Property(profile => profile.AllowOrderLinking).HasColumnName("allow_order_linking").IsRequired();
        builder.Property(profile => profile.OverDeliveryTolerancePercent)
            .HasColumnName("over_delivery_tolerance_percent")
            .IsRequired();
        builder.Property(profile => profile.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(profile => profile.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(profile => profile.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(profile => profile.UpdatedByUserId).HasColumnName("updated_by_user_id");
        builder.Property(profile => profile.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(profile => profile.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(profile => profile.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(profile => profile.StockCode)
            .IsUnique()
            .HasDatabaseName("ux_green_grocer_product_case_profiles_stock_code");

        builder.HasIndex(profile => new { profile.IsActive, profile.StockCode })
            .HasDatabaseName("ix_green_grocer_product_case_profiles_active_stock_code");
    }
}
