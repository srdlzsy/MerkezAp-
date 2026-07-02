using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMikroDocumentEditingDeletePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @AdminRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @PermissionId uniqueidentifier = 'd68e59be-25fa-4ca2-b223-bd5ee887d732';
                DECLARE @PermissionCode nvarchar(160) = N'duzeltme-islemleri.mikro-evrak-duzenleme.delete';
                DECLARE @CreatedAt datetime2 = '2026-04-14T00:00:00';

                IF NOT EXISTS (
                    SELECT 1
                    FROM app_permissions
                    WHERE code = @PermissionCode OR id = @PermissionId
                )
                BEGIN
                    INSERT INTO app_permissions (id, code, created_at_utc, description, name, updated_at_utc)
                    VALUES (
                        @PermissionId,
                        @PermissionCode,
                        @CreatedAt,
                        N'DuzeltmeIslemleri > MikroEvrakDuzenleme > Sil yetkisi.',
                        N'MikroEvrakDuzenleme Sil',
                        NULL
                    );
                END

                SELECT @PermissionId = id
                FROM app_permissions
                WHERE code = @PermissionCode;

                IF @PermissionId IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM app_roles
                    WHERE id = @AdminRoleId
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM app_role_permissions
                    WHERE permission_id = @PermissionId
                      AND role_id = @AdminRoleId
                )
                BEGIN
                    INSERT INTO app_role_permissions (permission_id, role_id, assigned_at_utc)
                    VALUES (@PermissionId, @AdminRoleId, @CreatedAt);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rolePermission
                FROM app_role_permissions AS rolePermission
                INNER JOIN app_permissions AS permission ON permission.id = rolePermission.permission_id
                WHERE permission.code = N'duzeltme-islemleri.mikro-evrak-duzenleme.delete';

                DELETE FROM app_permissions
                WHERE code = N'duzeltme-islemleri.mikro-evrak-duzenleme.delete';
                """);
        }
    }
}
