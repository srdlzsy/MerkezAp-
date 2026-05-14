using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class MobileOfflineSyncRequestConfiguration : IEntityTypeConfiguration<MobileOfflineSyncRequest>
{
    public void Configure(EntityTypeBuilder<MobileOfflineSyncRequest> builder)
    {
        builder.ToTable("mobile_offline_sync_requests");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.OperationCode)
            .HasColumnName("operation_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(item => item.RequestedByUserId)
            .HasColumnName("requested_by_user_id")
            .IsRequired();

        builder.Property(item => item.WarehouseNo)
            .HasColumnName("warehouse_no")
            .IsRequired();

        builder.Property(item => item.ClientRequestId)
            .HasColumnName("client_request_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(item => item.RequestFingerprint)
            .HasColumnName("request_fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(item => item.RequestPayload)
            .HasColumnName("request_payload");

        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.ResponsePayload)
            .HasColumnName("response_payload");

        builder.Property(item => item.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.Property(item => item.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.HasIndex(
                item => new
                {
                    item.OperationCode,
                    item.RequestedByUserId,
                    item.ClientRequestId
                })
            .IsUnique()
            .HasDatabaseName("ux_mobile_offline_sync_requests_operation_user_request");
    }
}
