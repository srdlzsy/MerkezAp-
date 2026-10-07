using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class DatabaseMonitoringIncidentConfiguration : IEntityTypeConfiguration<DatabaseMonitoringIncident>
{
    public void Configure(EntityTypeBuilder<DatabaseMonitoringIncident> builder)
    {
        builder.ToTable("database_monitoring_incidents");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(item => item.Type).HasColumnName("type").HasMaxLength(40).IsRequired();
        builder.Property(item => item.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        builder.Property(item => item.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(item => item.BlockingSessionId).HasColumnName("blocking_session_id");
        builder.Property(item => item.DatabaseName).HasColumnName("database_name").HasMaxLength(128);
        builder.Property(item => item.LoginName).HasColumnName("login_name").HasMaxLength(128);
        builder.Property(item => item.HostName).HasColumnName("host_name").HasMaxLength(128);
        builder.Property(item => item.ProgramName).HasColumnName("program_name").HasMaxLength(256);
        builder.Property(item => item.WaitType).HasColumnName("wait_type").HasMaxLength(120);
        builder.Property(item => item.ElapsedMilliseconds).HasColumnName("elapsed_milliseconds").IsRequired();
        builder.Property(item => item.SqlText).HasColumnName("sql_text").HasMaxLength(4000);
        builder.Property(item => item.Recommendation).HasColumnName("recommendation").HasMaxLength(1000).IsRequired();
        builder.Property(item => item.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").IsRequired();
        builder.Property(item => item.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(item => item.OccurrenceCount).HasColumnName("occurrence_count").IsRequired();
        builder.Property(item => item.ResolvedAtUtc).HasColumnName("resolved_at_utc");

        builder.HasIndex(item => item.Fingerprint)
            .IsUnique()
            .HasDatabaseName("ux_database_monitoring_incidents_fingerprint");
        builder.HasIndex(item => new { item.ResolvedAtUtc, item.LastSeenAtUtc })
            .HasDatabaseName("ix_database_monitoring_incidents_active_last_seen");
    }
}
