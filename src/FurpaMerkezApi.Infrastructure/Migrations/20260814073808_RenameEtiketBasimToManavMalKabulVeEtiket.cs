using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameEtiketBasimToManavMalKabulVeEtiket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("119f2412-b309-b962-fbec-6733704c5818"), "kasa-islemleri.manav-mal-kabul-etiket.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Sayfa yetkisi.", "ManavMalKabulVeEtiket Sayfa", null },
                    { new Guid("5052a0f3-5e9f-6468-b436-e3fad9f8b06f"), "kasa-islemleri.manav-mal-kabul-etiket.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Guncelle yetkisi.", "ManavMalKabulVeEtiket Guncelle", null },
                    { new Guid("58f6c8c3-18a6-32c3-a6fb-025bfd6c644f"), "kasa-islemleri.manav-mal-kabul-etiket.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Listele yetkisi.", "ManavMalKabulVeEtiket Listele", null },
                    { new Guid("693497e4-c2de-5c39-729a-365840f8cbaf"), "kasa-islemleri.manav-mal-kabul-etiket.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Detay yetkisi.", "ManavMalKabulVeEtiket Detay", null },
                    { new Guid("6c613186-0e32-1802-8c1e-9f31c17f5d3c"), "kasa-islemleri.manav-mal-kabul-etiket.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Sil yetkisi.", "ManavMalKabulVeEtiket Sil", null },
                    { new Guid("c90ff086-cd2b-b88d-52d5-8aa2d6a48b51"), "kasa-islemleri.manav-mal-kabul-etiket.transfer", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Mal Kabul yetkisi.", "ManavMalKabulVeEtiket Mal Kabul", null },
                    { new Guid("e3a0c0d9-1b00-8dce-a5d0-6260628d941d"), "kasa-islemleri.manav-mal-kabul-etiket.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Ekle yetkisi.", "ManavMalKabulVeEtiket Ekle", null },
                    { new Guid("e8f9c1bd-302f-7474-f8ec-7a6cf1f77b08"), "kasa-islemleri.manav-mal-kabul-etiket.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > ManavMalKabulVeEtiket > Tum Depolar yetkisi.", "ManavMalKabulVeEtiket Tum Depolar", null }
                });

            MoveRoleAssignments(migrationBuilder, "1c60e631-936d-21e7-00da-c78c3a78f92a", "119f2412-b309-b962-fbec-6733704c5818");
            MoveRoleAssignments(migrationBuilder, "8f2efb39-38c7-10d2-5574-427d56a53d7d", "58f6c8c3-18a6-32c3-a6fb-025bfd6c644f");
            MoveRoleAssignments(migrationBuilder, "5c555522-27d5-ab96-b9f0-4c933b86c12e", "693497e4-c2de-5c39-729a-365840f8cbaf");
            MoveRoleAssignments(migrationBuilder, "e06804dc-485d-0d77-bbab-41e3ff9d02ba", "e3a0c0d9-1b00-8dce-a5d0-6260628d941d");
            MoveRoleAssignments(migrationBuilder, "b2e146a3-7b64-c1ae-9a75-d0faac25258e", "5052a0f3-5e9f-6468-b436-e3fad9f8b06f");
            MoveRoleAssignments(migrationBuilder, "e7ad6a0c-f4ef-620f-0972-862663f2ab09", "6c613186-0e32-1802-8c1e-9f31c17f5d3c");
            MoveRoleAssignments(migrationBuilder, "b0e99a09-1761-8c46-13cb-5f6f37a75374", "c90ff086-cd2b-b88d-52d5-8aa2d6a48b51");
            MoveRoleAssignments(migrationBuilder, "ce902d4b-1a05-304e-3cef-6b3e864039a6", "e8f9c1bd-302f-7474-f8ec-7a6cf1f77b08");

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValues: new object[]
                {
                    new Guid("1c60e631-936d-21e7-00da-c78c3a78f92a"),
                    new Guid("5c555522-27d5-ab96-b9f0-4c933b86c12e"),
                    new Guid("8f2efb39-38c7-10d2-5574-427d56a53d7d"),
                    new Guid("b0e99a09-1761-8c46-13cb-5f6f37a75374"),
                    new Guid("b2e146a3-7b64-c1ae-9a75-d0faac25258e"),
                    new Guid("ce902d4b-1a05-304e-3cef-6b3e864039a6"),
                    new Guid("e06804dc-485d-0d77-bbab-41e3ff9d02ba"),
                    new Guid("e7ad6a0c-f4ef-620f-0972-862663f2ab09")
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("1c60e631-936d-21e7-00da-c78c3a78f92a"), "kasa-islemleri.etiket-basim.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Sayfa yetkisi.", "EtiketBasim Sayfa", null },
                    { new Guid("5c555522-27d5-ab96-b9f0-4c933b86c12e"), "kasa-islemleri.etiket-basim.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Detay yetkisi.", "EtiketBasim Detay", null },
                    { new Guid("8f2efb39-38c7-10d2-5574-427d56a53d7d"), "kasa-islemleri.etiket-basim.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Listele yetkisi.", "EtiketBasim Listele", null },
                    { new Guid("b0e99a09-1761-8c46-13cb-5f6f37a75374"), "kasa-islemleri.etiket-basim.transfer", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Aktar yetkisi.", "EtiketBasim Aktar", null },
                    { new Guid("b2e146a3-7b64-c1ae-9a75-d0faac25258e"), "kasa-islemleri.etiket-basim.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Guncelle yetkisi.", "EtiketBasim Guncelle", null },
                    { new Guid("ce902d4b-1a05-304e-3cef-6b3e864039a6"), "kasa-islemleri.etiket-basim.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Tum Depolar yetkisi.", "EtiketBasim Tum Depolar", null },
                    { new Guid("e06804dc-485d-0d77-bbab-41e3ff9d02ba"), "kasa-islemleri.etiket-basim.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Ekle yetkisi.", "EtiketBasim Ekle", null },
                    { new Guid("e7ad6a0c-f4ef-620f-0972-862663f2ab09"), "kasa-islemleri.etiket-basim.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Sil yetkisi.", "EtiketBasim Sil", null }
                });

            MoveRoleAssignments(migrationBuilder, "119f2412-b309-b962-fbec-6733704c5818", "1c60e631-936d-21e7-00da-c78c3a78f92a");
            MoveRoleAssignments(migrationBuilder, "58f6c8c3-18a6-32c3-a6fb-025bfd6c644f", "8f2efb39-38c7-10d2-5574-427d56a53d7d");
            MoveRoleAssignments(migrationBuilder, "693497e4-c2de-5c39-729a-365840f8cbaf", "5c555522-27d5-ab96-b9f0-4c933b86c12e");
            MoveRoleAssignments(migrationBuilder, "e3a0c0d9-1b00-8dce-a5d0-6260628d941d", "e06804dc-485d-0d77-bbab-41e3ff9d02ba");
            MoveRoleAssignments(migrationBuilder, "5052a0f3-5e9f-6468-b436-e3fad9f8b06f", "b2e146a3-7b64-c1ae-9a75-d0faac25258e");
            MoveRoleAssignments(migrationBuilder, "6c613186-0e32-1802-8c1e-9f31c17f5d3c", "e7ad6a0c-f4ef-620f-0972-862663f2ab09");
            MoveRoleAssignments(migrationBuilder, "c90ff086-cd2b-b88d-52d5-8aa2d6a48b51", "b0e99a09-1761-8c46-13cb-5f6f37a75374");
            MoveRoleAssignments(migrationBuilder, "e8f9c1bd-302f-7474-f8ec-7a6cf1f77b08", "ce902d4b-1a05-304e-3cef-6b3e864039a6");

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValues: new object[]
                {
                    new Guid("119f2412-b309-b962-fbec-6733704c5818"),
                    new Guid("5052a0f3-5e9f-6468-b436-e3fad9f8b06f"),
                    new Guid("58f6c8c3-18a6-32c3-a6fb-025bfd6c644f"),
                    new Guid("693497e4-c2de-5c39-729a-365840f8cbaf"),
                    new Guid("6c613186-0e32-1802-8c1e-9f31c17f5d3c"),
                    new Guid("c90ff086-cd2b-b88d-52d5-8aa2d6a48b51"),
                    new Guid("e3a0c0d9-1b00-8dce-a5d0-6260628d941d"),
                    new Guid("e8f9c1bd-302f-7474-f8ec-7a6cf1f77b08")
                });
        }

        private static void MoveRoleAssignments(MigrationBuilder migrationBuilder, string fromPermissionId, string toPermissionId)
        {
            migrationBuilder.Sql($"""
                INSERT INTO [app_role_permissions] ([role_id], [permission_id], [assigned_at_utc])
                SELECT source.[role_id], CAST('{toPermissionId}' AS uniqueidentifier), MIN(source.[assigned_at_utc])
                FROM [app_role_permissions] AS source
                WHERE source.[permission_id] = CAST('{fromPermissionId}' AS uniqueidentifier)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [app_role_permissions] AS existing
                      WHERE existing.[role_id] = source.[role_id]
                        AND existing.[permission_id] = CAST('{toPermissionId}' AS uniqueidentifier))
                GROUP BY source.[role_id];

                DELETE FROM [app_role_permissions]
                WHERE [permission_id] = CAST('{fromPermissionId}' AS uniqueidentifier);
                """);
        }
    }
}
