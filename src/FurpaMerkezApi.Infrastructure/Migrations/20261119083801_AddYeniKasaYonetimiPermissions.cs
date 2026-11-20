using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddYeniKasaYonetimiPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("1c65e8a0-fd01-b756-f489-6bc184e650b7"), "ayar-islemleri.yeni-kasa-yonetimi.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Detay yetkisi.", "YeniKasaYonetimi Detay", null },
                    { new Guid("5378bac0-8719-da89-46bd-96c34599a94d"), "ayar-islemleri.yeni-kasa-yonetimi.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Sayfa yetkisi.", "YeniKasaYonetimi Sayfa", null },
                    { new Guid("5d9e7a55-bb31-ff8c-93a4-6a5a4d04d65d"), "ayar-islemleri.yeni-kasa-yonetimi.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Guncelle yetkisi.", "YeniKasaYonetimi Guncelle", null },
                    { new Guid("7ea8e301-4169-c029-f541-e6bc7bf04333"), "ayar-islemleri.yeni-kasa-yonetimi.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Sil yetkisi.", "YeniKasaYonetimi Sil", null },
                    { new Guid("b5564ae1-075b-e07b-292f-b3126d91cfcf"), "ayar-islemleri.yeni-kasa-yonetimi.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Ekle yetkisi.", "YeniKasaYonetimi Ekle", null },
                    { new Guid("ce6f5dee-1d7b-a550-b588-7b27cdaee122"), "ayar-islemleri.yeni-kasa-yonetimi.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Tum Depolar yetkisi.", "YeniKasaYonetimi Tum Depolar", null },
                    { new Guid("f1fe45a7-3246-faf9-8517-0a069764a8a1"), "ayar-islemleri.yeni-kasa-yonetimi.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > YeniKasaYonetimi > Listele yetkisi.", "YeniKasaYonetimi Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("1c65e8a0-fd01-b756-f489-6bc184e650b7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5378bac0-8719-da89-46bd-96c34599a94d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5d9e7a55-bb31-ff8c-93a4-6a5a4d04d65d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7ea8e301-4169-c029-f541-e6bc7bf04333"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b5564ae1-075b-e07b-292f-b3126d91cfcf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ce6f5dee-1d7b-a550-b588-7b27cdaee122"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f1fe45a7-3246-faf9-8517-0a069764a8a1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1c65e8a0-fd01-b756-f489-6bc184e650b7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5378bac0-8719-da89-46bd-96c34599a94d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5d9e7a55-bb31-ff8c-93a4-6a5a4d04d65d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7ea8e301-4169-c029-f541-e6bc7bf04333"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b5564ae1-075b-e07b-292f-b3126d91cfcf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ce6f5dee-1d7b-a550-b588-7b27cdaee122"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f1fe45a7-3246-faf9-8517-0a069764a8a1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1c65e8a0-fd01-b756-f489-6bc184e650b7"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5378bac0-8719-da89-46bd-96c34599a94d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5d9e7a55-bb31-ff8c-93a4-6a5a4d04d65d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7ea8e301-4169-c029-f541-e6bc7bf04333"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b5564ae1-075b-e07b-292f-b3126d91cfcf"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ce6f5dee-1d7b-a550-b588-7b27cdaee122"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f1fe45a7-3246-faf9-8517-0a069764a8a1"));
        }
    }
}
