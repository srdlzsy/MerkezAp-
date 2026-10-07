using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class DatabaseSessionTerminationAuditConfiguration : IEntityTypeConfiguration<DatabaseSessionTerminationAudit>
{
    public void Configure(EntityTypeBuilder<DatabaseSessionTerminationAudit> builder)
    {
        builder.ToTable("database_session_termination_audits");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(item => item.LoginTime).HasColumnName("login_time").IsRequired();
        builder.Property(item => item.HostProcessId).HasColumnName("host_process_id");
        builder.Property(item => item.LoginName).HasColumnName("login_name").HasMaxLength(128);
        builder.Property(item => item.HostName).HasColumnName("host_name").HasMaxLength(128);
        builder.Property(item => item.ProgramName).HasColumnName("program_name").HasMaxLength(256);
        builder.Property(item => item.DatabaseName).HasColumnName("database_name").HasMaxLength(128);
        builder.Property(item => item.SqlText).HasColumnName("sql_text").HasMaxLength(4000);
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(item => item.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(item => item.IsSucceeded).HasColumnName("is_succeeded").IsRequired();
        builder.Property(item => item.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(item => item.Error).HasColumnName("error").HasMaxLength(2000);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(item => item.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.RequestedAtUtc)
            .HasDatabaseName("ix_database_session_termination_audits_requested_at");
        builder.HasIndex(item => new { item.SessionId, item.RequestedAtUtc })
            .HasDatabaseName("ix_database_session_termination_audits_session_requested");
    }
}
