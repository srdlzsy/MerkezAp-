using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveCashSummaryEditDeletePermissionsToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @seededAtUtc datetime2 = CAST('2026-04-14T00:00:00' AS datetime2);
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';

                DECLARE @sourceUpdatePermissionId uniqueidentifier =
                    (SELECT TOP 1 [id] FROM [app_permissions] WHERE [code] = N'kasa-islemleri.icmal-kaydi-girisi.update');
                DECLARE @sourceDeletePermissionId uniqueidentifier =
                    (SELECT TOP 1 [id] FROM [app_permissions] WHERE [code] = N'kasa-islemleri.icmal-kaydi-girisi.delete');
                DECLARE @targetUpdatePermissionId uniqueidentifier = '97b6ea99-c766-a946-5912-61af6ef2f0fe';
                DECLARE @targetDeletePermissionId uniqueidentifier = 'baabb904-6089-0901-234b-11c14089c6dd';

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'kasa-islemleri.kasa-sayimlari.update')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (
                        @targetUpdatePermissionId,
                        N'kasa-islemleri.kasa-sayimlari.update',
                        @seededAtUtc,
                        N'KasaIslemleri > KasaSayimlari > Guncelle yetkisi.',
                        N'KasaSayimlari Guncelle',
                        NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'kasa-islemleri.kasa-sayimlari.delete')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (
                        @targetDeletePermissionId,
                        N'kasa-islemleri.kasa-sayimlari.delete',
                        @seededAtUtc,
                        N'KasaIslemleri > KasaSayimlari > Sil yetkisi.',
                        N'KasaSayimlari Sil',
                        NULL);
                END;

                SELECT @targetUpdatePermissionId = [id]
                FROM [app_permissions]
                WHERE [code] = N'kasa-islemleri.kasa-sayimlari.update';

                SELECT @targetDeletePermissionId = [id]
                FROM [app_permissions]
                WHERE [code] = N'kasa-islemleri.kasa-sayimlari.delete';

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @targetUpdatePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @sourceUpdatePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @targetUpdatePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @targetDeletePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @sourceDeletePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @targetDeletePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN (@sourceUpdatePermissionId, @sourceDeletePermissionId);

                DELETE FROM [app_permissions]
                WHERE [id] IN (@sourceUpdatePermissionId, @sourceDeletePermissionId);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @seededAtUtc datetime2 = CAST('2026-04-14T00:00:00' AS datetime2);
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';

                DECLARE @sourceUpdatePermissionId uniqueidentifier =
                    (SELECT TOP 1 [id] FROM [app_permissions] WHERE [code] = N'kasa-islemleri.kasa-sayimlari.update');
                DECLARE @sourceDeletePermissionId uniqueidentifier =
                    (SELECT TOP 1 [id] FROM [app_permissions] WHERE [code] = N'kasa-islemleri.kasa-sayimlari.delete');
                DECLARE @targetUpdatePermissionId uniqueidentifier = '689d1e9e-dcd4-2ee9-9b51-e55371aa9c35';
                DECLARE @targetDeletePermissionId uniqueidentifier = 'd55a0756-1a47-b26e-c666-1cbe97fb489b';

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'kasa-islemleri.icmal-kaydi-girisi.update')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (
                        @targetUpdatePermissionId,
                        N'kasa-islemleri.icmal-kaydi-girisi.update',
                        @seededAtUtc,
                        N'KasaIslemleri > IcmalKaydiGirisi > Guncelle yetkisi.',
                        N'IcmalKaydiGirisi Guncelle',
                        NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'kasa-islemleri.icmal-kaydi-girisi.delete')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (
                        @targetDeletePermissionId,
                        N'kasa-islemleri.icmal-kaydi-girisi.delete',
                        @seededAtUtc,
                        N'KasaIslemleri > IcmalKaydiGirisi > Sil yetkisi.',
                        N'IcmalKaydiGirisi Sil',
                        NULL);
                END;

                SELECT @targetUpdatePermissionId = [id]
                FROM [app_permissions]
                WHERE [code] = N'kasa-islemleri.icmal-kaydi-girisi.update';

                SELECT @targetDeletePermissionId = [id]
                FROM [app_permissions]
                WHERE [code] = N'kasa-islemleri.icmal-kaydi-girisi.delete';

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @targetUpdatePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @sourceUpdatePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @targetUpdatePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @targetDeletePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @sourceDeletePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @targetDeletePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN (@sourceUpdatePermissionId, @sourceDeletePermissionId);

                DELETE FROM [app_permissions]
                WHERE [id] IN (@sourceUpdatePermissionId, @sourceDeletePermissionId);
                """);
        }
    }
}
