using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260828104000_AddBanknoteTrackMutationPermissions")]
    public partial class AddBanknoteTrackMutationPermissions : Migration
    {
        private const string AdministratorRoleId = "2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a";
        private const string UpdatePermissionId = "84d7e17e-c4ee-4fb0-8321-088b6ce96854";
        private const string DeletePermissionId = "29976880-6fbd-449f-b3b1-5c2d22950b4c";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'kasa-islemleri.banknot-takipleri.update')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [name], [description], [created_at_utc], [updated_at_utc])
                    VALUES ('{UpdatePermissionId}', N'kasa-islemleri.banknot-takipleri.update', N'BanknotTakipleri Guncelle', N'KasaIslemleri > BanknotTakipleri > Guncelle yetkisi.', '2026-08-28T00:00:00.0000000Z', NULL)
                END

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'kasa-islemleri.banknot-takipleri.delete')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [name], [description], [created_at_utc], [updated_at_utc])
                    VALUES ('{DeletePermissionId}', N'kasa-islemleri.banknot-takipleri.delete', N'BanknotTakipleri Sil', N'KasaIslemleri > BanknotTakipleri > Sil yetkisi.', '2026-08-28T00:00:00.0000000Z', NULL)
                END

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '{UpdatePermissionId}' AND [role_id] = '{AdministratorRoleId}')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('{UpdatePermissionId}', '{AdministratorRoleId}', '2026-08-28T00:00:00.0000000Z')
                END

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '{DeletePermissionId}' AND [role_id] = '{AdministratorRoleId}')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('{DeletePermissionId}', '{AdministratorRoleId}', '2026-08-28T00:00:00.0000000Z')
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN ('{UpdatePermissionId}', '{DeletePermissionId}');

                DELETE FROM [app_permissions]
                WHERE [id] IN ('{UpdatePermissionId}', '{DeletePermissionId}')
                   OR [code] IN (N'kasa-islemleri.banknot-takipleri.update', N'kasa-islemleri.banknot-takipleri.delete');
                """);
        }
    }
}
