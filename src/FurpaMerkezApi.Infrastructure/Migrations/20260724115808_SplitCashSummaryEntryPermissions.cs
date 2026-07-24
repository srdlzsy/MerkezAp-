using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    public partial class SplitCashSummaryEntryPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @seededAtUtc datetime2 = CAST('2026-04-14T00:00:00' AS datetime2);
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';

                DECLARE @oldCreatePermissionId uniqueidentifier = '2fd4f83b-41dd-9fbb-d1d2-e40bdaa9f041';
                DECLARE @oldUpdatePermissionId uniqueidentifier = '97b6ea99-c766-a946-5912-61af6ef2f0fe';
                DECLARE @newListPermissionId uniqueidentifier = '9a800872-1ab3-6e73-c3ae-dc42b48f1753';
                DECLARE @newCreatePermissionId uniqueidentifier = '46558377-9879-a2b9-fd7d-6dbe9f10849c';
                DECLARE @newUpdatePermissionId uniqueidentifier = '689d1e9e-dcd4-2ee9-9b51-e55371aa9c35';
                DECLARE @newDeletePermissionId uniqueidentifier = 'd55a0756-1a47-b26e-c666-1cbe97fb489b';

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = @newListPermissionId)
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (@newListPermissionId, N'kasa-islemleri.icmal-kaydi-girisi.list', @seededAtUtc, N'KasaIslemleri > IcmalKaydiGirisi > Listele yetkisi.', N'IcmalKaydiGirisi Listele', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = @newCreatePermissionId)
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (@newCreatePermissionId, N'kasa-islemleri.icmal-kaydi-girisi.create', @seededAtUtc, N'KasaIslemleri > IcmalKaydiGirisi > Ekle yetkisi.', N'IcmalKaydiGirisi Ekle', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = @newUpdatePermissionId)
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (@newUpdatePermissionId, N'kasa-islemleri.icmal-kaydi-girisi.update', @seededAtUtc, N'KasaIslemleri > IcmalKaydiGirisi > Guncelle yetkisi.', N'IcmalKaydiGirisi Guncelle', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = @newDeletePermissionId)
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (@newDeletePermissionId, N'kasa-islemleri.icmal-kaydi-girisi.delete', @seededAtUtc, N'KasaIslemleri > IcmalKaydiGirisi > Sil yetkisi.', N'IcmalKaydiGirisi Sil', NULL);
                END;

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @newListPermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] IN (@oldCreatePermissionId, @oldUpdatePermissionId)
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @newListPermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @newCreatePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @oldCreatePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @newCreatePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @newUpdatePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @oldUpdatePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @newUpdatePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @newDeletePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @oldUpdatePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @newDeletePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN (@oldCreatePermissionId, @oldUpdatePermissionId);

                DELETE FROM [app_permissions]
                WHERE [id] IN (@oldCreatePermissionId, @oldUpdatePermissionId);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @seededAtUtc datetime2 = CAST('2026-04-14T00:00:00' AS datetime2);
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';

                DECLARE @oldCreatePermissionId uniqueidentifier = '2fd4f83b-41dd-9fbb-d1d2-e40bdaa9f041';
                DECLARE @oldUpdatePermissionId uniqueidentifier = '97b6ea99-c766-a946-5912-61af6ef2f0fe';
                DECLARE @newListPermissionId uniqueidentifier = '9a800872-1ab3-6e73-c3ae-dc42b48f1753';
                DECLARE @newCreatePermissionId uniqueidentifier = '46558377-9879-a2b9-fd7d-6dbe9f10849c';
                DECLARE @newUpdatePermissionId uniqueidentifier = '689d1e9e-dcd4-2ee9-9b51-e55371aa9c35';
                DECLARE @newDeletePermissionId uniqueidentifier = 'd55a0756-1a47-b26e-c666-1cbe97fb489b';

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = @oldCreatePermissionId)
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (@oldCreatePermissionId, N'kasa-islemleri.kasa-sayimlari.create', @seededAtUtc, N'KasaIslemleri > KasaSayimlari > Ekle yetkisi.', N'KasaSayimlari Ekle', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = @oldUpdatePermissionId)
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES (@oldUpdatePermissionId, N'kasa-islemleri.kasa-sayimlari.update', @seededAtUtc, N'KasaIslemleri > KasaSayimlari > Guncelle yetkisi.', N'KasaSayimlari Guncelle', NULL);
                END;

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @oldCreatePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @newCreatePermissionId
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @oldCreatePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT @oldUpdatePermissionId, source.[role_id], MIN(source.[assigned_at_utc])
                FROM (
                    SELECT [role_id], [assigned_at_utc]
                    FROM [app_role_permissions]
                    WHERE [permission_id] IN (@newUpdatePermissionId, @newDeletePermissionId)
                    UNION ALL
                    SELECT @administratorRoleId, @seededAtUtc
                    WHERE EXISTS (SELECT 1 FROM [app_roles] WHERE [id] = @administratorRoleId)
                ) AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = @oldUpdatePermissionId
                      AND existing.[role_id] = source.[role_id])
                GROUP BY source.[role_id];

                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN (
                    @newListPermissionId,
                    @newCreatePermissionId,
                    @newUpdatePermissionId,
                    @newDeletePermissionId);

                DELETE FROM [app_permissions]
                WHERE [id] IN (
                    @newListPermissionId,
                    @newCreatePermissionId,
                    @newUpdatePermissionId,
                    @newDeletePermissionId);
                """);
        }
    }
}
