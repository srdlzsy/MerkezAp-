using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddYeniKasaYonetimiSectionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("23d464a2-ddd6-2db4-4f31-171daec15739"), "ayar-islemleri.yeni-kasa-yonetimi.musteri-ekrani", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Musteri Ekrani yetkisi.", "YeniKasaYonetimi Musteri Ekrani", null },
                    { new Guid("32146628-0841-e939-0a3e-6db48ce0727e"), "ayar-islemleri.yeni-kasa-yonetimi.hizli-tuslar", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Hizli Tus Gruplari ve Urunler yetkisi.", "YeniKasaYonetimi Hizli Tus Gruplari ve Urunler", null },
                    { new Guid("497308f8-8e83-e638-ce6d-35b1add6e2cd"), "ayar-islemleri.yeni-kasa-yonetimi.odeme-yontemleri", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Odeme Yontemleri yetkisi.", "YeniKasaYonetimi Odeme Yontemleri", null },
                    { new Guid("89120a3d-3cb2-d5f9-000d-dfcbd36ea53a"), "ayar-islemleri.yeni-kasa-yonetimi.genel-ayarlar", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Genel Ayarlar ve Fis Limitleri yetkisi.", "YeniKasaYonetimi Genel Ayarlar ve Fis Limitleri", null },
                    { new Guid("920d328a-1c39-4ef1-2deb-b300b59dd6bc"), "ayar-islemleri.yeni-kasa-yonetimi.yetki-matrisi", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Kasa Yetki Matrisi yetkisi.", "YeniKasaYonetimi Kasa Yetki Matrisi", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("23d464a2-ddd6-2db4-4f31-171daec15739"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("32146628-0841-e939-0a3e-6db48ce0727e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("497308f8-8e83-e638-ce6d-35b1add6e2cd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("89120a3d-3cb2-d5f9-000d-dfcbd36ea53a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("920d328a-1c39-4ef1-2deb-b300b59dd6bc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            // Preserve current access on rollout. Roles that can already open/list this page
            // initially receive every section; administrators can narrow them afterwards.
            migrationBuilder.Sql(
                """
                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT section_permission.[id], existing_role.[role_id], SYSUTCDATETIME()
                FROM (
                    SELECT DISTINCT role_permission.[role_id]
                    FROM [app_role_permissions] AS role_permission
                    INNER JOIN [app_permissions] AS permission
                        ON permission.[id] = role_permission.[permission_id]
                    WHERE permission.[code] IN (
                        'ayar-islemleri.yeni-kasa-yonetimi.page',
                        'ayar-islemleri.yeni-kasa-yonetimi.list')
                ) AS existing_role
                CROSS JOIN [app_permissions] AS section_permission
                WHERE section_permission.[code] IN (
                    'ayar-islemleri.yeni-kasa-yonetimi.genel-ayarlar',
                    'ayar-islemleri.yeni-kasa-yonetimi.odeme-yontemleri',
                    'ayar-islemleri.yeni-kasa-yonetimi.yetki-matrisi',
                    'ayar-islemleri.yeni-kasa-yonetimi.musteri-ekrani',
                    'ayar-islemleri.yeni-kasa-yonetimi.hizli-tuslar')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [app_role_permissions] AS assigned
                      WHERE assigned.[permission_id] = section_permission.[id]
                        AND assigned.[role_id] = existing_role.[role_id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN (
                    '23d464a2-ddd6-2db4-4f31-171daec15739',
                    '32146628-0841-e939-0a3e-6db48ce0727e',
                    '497308f8-8e83-e638-ce6d-35b1add6e2cd',
                    '89120a3d-3cb2-d5f9-000d-dfcbd36ea53a',
                    '920d328a-1c39-4ef1-2deb-b300b59dd6bc');
                """);

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("23d464a2-ddd6-2db4-4f31-171daec15739"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("32146628-0841-e939-0a3e-6db48ce0727e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("497308f8-8e83-e638-ce6d-35b1add6e2cd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("89120a3d-3cb2-d5f9-000d-dfcbd36ea53a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("920d328a-1c39-4ef1-2deb-b300b59dd6bc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("23d464a2-ddd6-2db4-4f31-171daec15739"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("32146628-0841-e939-0a3e-6db48ce0727e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("497308f8-8e83-e638-ce6d-35b1add6e2cd"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("89120a3d-3cb2-d5f9-000d-dfcbd36ea53a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("920d328a-1c39-4ef1-2deb-b300b59dd6bc"));
        }
    }
}
