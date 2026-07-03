using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPerformanceCardPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @AdminRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @CreatedAt datetime2 = '2026-04-14T00:00:00';

                DECLARE @Permissions TABLE (
                    [id] uniqueidentifier NOT NULL,
                    [code] nvarchar(160) NOT NULL,
                    [description] nvarchar(250) NOT NULL,
                    [name] nvarchar(100) NOT NULL
                );

                INSERT INTO @Permissions ([id], [code], [description], [name])
                VALUES
                    ('f52c6768-1a77-7968-93dd-b5927c4aa55a', N'rapor-islemleri.tedarikci-performans-karnesi.list', N'RaporIslemleri > TedarikciPerformansKarnesi > Listele yetkisi.', N'TedarikciPerformansKarnesi Listele'),
                    ('9fafd716-622d-4a0a-7d8a-7965b85d758f', N'rapor-islemleri.tedarikci-performans-karnesi.detail', N'RaporIslemleri > TedarikciPerformansKarnesi > Detay yetkisi.', N'TedarikciPerformansKarnesi Detay');

                INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                SELECT pending.[id], pending.[code], @CreatedAt, pending.[description], pending.[name], NULL
                FROM @Permissions AS pending
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions] AS existing
                    WHERE existing.[code] = pending.[code]
                       OR existing.[id] = pending.[id]
                );

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT permission.[id], @AdminRoleId, @CreatedAt
                FROM [app_permissions] AS permission
                INNER JOIN @Permissions AS pending ON pending.[code] = permission.[code]
                WHERE EXISTS (
                    SELECT 1
                    FROM [app_roles]
                    WHERE [id] = @AdminRoleId
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS rolePermission
                    WHERE rolePermission.[permission_id] = permission.[id]
                      AND rolePermission.[role_id] = @AdminRoleId
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                INNER JOIN [app_permissions] AS permission ON permission.[id] = rolePermission.[permission_id]
                WHERE permission.[code] IN (
                    N'rapor-islemleri.tedarikci-performans-karnesi.list',
                    N'rapor-islemleri.tedarikci-performans-karnesi.detail'
                );

                DELETE FROM [app_permissions]
                WHERE [code] IN (
                    N'rapor-islemleri.tedarikci-performans-karnesi.list',
                    N'rapor-islemleri.tedarikci-performans-karnesi.detail'
                );
                """);
        }
    }
}
