using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class AppUserClientRoleConfiguration : IEntityTypeConfiguration<AppUserClientRole>
{
    public void Configure(EntityTypeBuilder<AppUserClientRole> builder)
    {
        builder.ToTable("app_user_client_roles");

        builder.HasKey(userClientRole => new
        {
            userClientRole.UserId,
            userClientRole.ClientType,
            userClientRole.RoleId
        });

        builder.Property(userClientRole => userClientRole.UserId)
            .HasColumnName("user_id");

        builder.Property(userClientRole => userClientRole.ClientType)
            .HasColumnName("client_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(userClientRole => userClientRole.RoleId)
            .HasColumnName("role_id");

        builder.Property(userClientRole => userClientRole.AssignedAtUtc)
            .HasColumnName("assigned_at_utc")
            .IsRequired();

        builder.HasIndex(userClientRole => userClientRole.RoleId)
            .HasDatabaseName("ix_app_user_client_roles_role_id");

        builder.HasOne(userClientRole => userClientRole.User)
            .WithMany(user => user.ClientRoles)
            .HasForeignKey(userClientRole => userClientRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(userClientRole => userClientRole.Role)
            .WithMany(role => role.UserClientRoles)
            .HasForeignKey(userClientRole => userClientRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
