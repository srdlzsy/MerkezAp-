using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    public partial class AddMikroDocumentEditingPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @seededAt datetime2 = CAST('2026-04-14T00:00:00' AS datetime2);
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @permissions TABLE
                (
                    [id] uniqueidentifier NOT NULL,
                    [code] nvarchar(100) NOT NULL,
                    [description] nvarchar(250) NOT NULL,
                    [name] nvarchar(100) NOT NULL
                );

                INSERT INTO @permissions ([id], [code], [description], [name])
                VALUES
                    ('e5e3e8c2-9405-68ee-5ef6-2cb1fd77617f', N'duzeltme-islemleri.mikro-evrak-duzenleme.list', N'DuzeltmeIslemleri > MikroEvrakDuzenleme > Listele yetkisi.', N'MikroEvrakDuzenleme Listele'),
                    ('2f7527cd-497f-48b9-9188-399d18b42614', N'duzeltme-islemleri.mikro-evrak-duzenleme.detail', N'DuzeltmeIslemleri > MikroEvrakDuzenleme > Detay yetkisi.', N'MikroEvrakDuzenleme Detay'),
                    ('9d824ac7-7354-7184-c824-efd0122e5491', N'duzeltme-islemleri.mikro-evrak-duzenleme.update', N'DuzeltmeIslemleri > MikroEvrakDuzenleme > Guncelle yetkisi.', N'MikroEvrakDuzenleme Guncelle');

                INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                SELECT permission.[id], permission.[code], @seededAt, permission.[description], permission.[name], NULL
                FROM @permissions AS permission
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions] AS existingPermission
                    WHERE existingPermission.[code] = permission.[code]);

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT existingPermission.[id], @administratorRoleId, @seededAt
                FROM @permissions AS permission
                INNER JOIN [app_permissions] AS existingPermission
                    ON existingPermission.[code] = permission.[code]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existingRolePermission
                    WHERE existingRolePermission.[permission_id] = existingPermission.[id]
                      AND existingRolePermission.[role_id] = @administratorRoleId);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @permissions TABLE ([code] nvarchar(100) NOT NULL);

                INSERT INTO @permissions ([code])
                VALUES
                    (N'duzeltme-islemleri.mikro-evrak-duzenleme.list'),
                    (N'duzeltme-islemleri.mikro-evrak-duzenleme.detail'),
                    (N'duzeltme-islemleri.mikro-evrak-duzenleme.update');

                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                INNER JOIN [app_permissions] AS permission
                    ON permission.[id] = rolePermission.[permission_id]
                INNER JOIN @permissions AS permissionToDelete
                    ON permissionToDelete.[code] = permission.[code];

                DELETE permission
                FROM [app_permissions] AS permission
                INNER JOIN @permissions AS permissionToDelete
                    ON permissionToDelete.[code] = permission.[code];
                """);
        }
    }
}
