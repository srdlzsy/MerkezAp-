using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class TerminalInstallationConfiguration : IEntityTypeConfiguration<TerminalInstallation>
{
    public void Configure(EntityTypeBuilder<TerminalInstallation> builder)
    {
        builder.ToTable("terminal_installations");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.DeviceId).HasColumnName("device_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.AppVersion).HasColumnName("app_version").HasMaxLength(40).IsRequired();
        builder.Property(item => item.BuildNumber).HasColumnName("build_number").IsRequired();
        builder.Property(item => item.WarehouseNo).HasColumnName("warehouse_no").IsRequired();
        builder.Property(item => item.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(item => item.Manufacturer).HasColumnName("manufacturer").HasMaxLength(100);
        builder.Property(item => item.DeviceModel).HasColumnName("device_model").HasMaxLength(150);
        builder.Property(item => item.AndroidVersion).HasColumnName("android_version").HasMaxLength(40);
        builder.Property(item => item.AndroidSdk).HasColumnName("android_sdk");
        builder.Property(item => item.SupportedAbis).HasColumnName("supported_abis").HasMaxLength(500).IsRequired();
        builder.Property(item => item.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").IsRequired();
        builder.Property(item => item.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(item => item.LastIpAddress).HasColumnName("last_ip_address").HasMaxLength(64);
        builder.Property(item => item.PreviousWarehouseNo).HasColumnName("previous_warehouse_no");
        builder.Property(item => item.WarehouseChangedAtUtc).HasColumnName("warehouse_changed_at_utc");
        builder.Property(item => item.WarehouseChangeCount).HasColumnName("warehouse_change_count").IsRequired();
        builder.Property(item => item.VersionChangedAtUtc).HasColumnName("version_changed_at_utc");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.DeviceId).IsUnique().HasDatabaseName("ux_terminal_installations_device_id");
        builder.HasIndex(item => new { item.WarehouseNo, item.LastSeenAtUtc })
            .HasDatabaseName("ix_terminal_installations_warehouse_last_seen");
        builder.HasIndex(item => new { item.BuildNumber, item.LastSeenAtUtc })
            .HasDatabaseName("ix_terminal_installations_build_last_seen");
    }
}
