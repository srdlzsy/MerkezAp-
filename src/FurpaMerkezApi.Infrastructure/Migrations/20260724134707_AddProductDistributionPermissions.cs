using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDistributionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("3632a09f-90be-414c-a038-4528ea233919"), "operasyon-islemleri.urun-dagilimlari.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > UrunDagilimlari > Ekle yetkisi.", "UrunDagilimlari Ekle", null },
                    { new Guid("3e88cd72-ebcd-87ac-c369-bec3b9ec7d86"), "operasyon-islemleri.urun-dagilimlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > UrunDagilimlari > Detay yetkisi.", "UrunDagilimlari Detay", null },
                    { new Guid("db539656-bd1d-edbf-5cda-9fc07aa9f89d"), "operasyon-islemleri.urun-dagilimlari.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > UrunDagilimlari > Sil yetkisi.", "UrunDagilimlari Sil", null },
                    { new Guid("ef067b6e-081b-87c5-387a-33dadcbe8d29"), "operasyon-islemleri.urun-dagilimlari.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > UrunDagilimlari > Guncelle yetkisi.", "UrunDagilimlari Guncelle", null },
                    { new Guid("fe5ba913-b8d0-4eb2-8bfa-9da3c5d31c40"), "operasyon-islemleri.urun-dagilimlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > UrunDagilimlari > Listele yetkisi.", "UrunDagilimlari Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("3632a09f-90be-414c-a038-4528ea233919"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("3e88cd72-ebcd-87ac-c369-bec3b9ec7d86"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("db539656-bd1d-edbf-5cda-9fc07aa9f89d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ef067b6e-081b-87c5-387a-33dadcbe8d29"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("fe5ba913-b8d0-4eb2-8bfa-9da3c5d31c40"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3632a09f-90be-414c-a038-4528ea233919"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3e88cd72-ebcd-87ac-c369-bec3b9ec7d86"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("db539656-bd1d-edbf-5cda-9fc07aa9f89d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ef067b6e-081b-87c5-387a-33dadcbe8d29"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fe5ba913-b8d0-4eb2-8bfa-9da3c5d31c40"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3632a09f-90be-414c-a038-4528ea233919"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3e88cd72-ebcd-87ac-c369-bec3b9ec7d86"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("db539656-bd1d-edbf-5cda-9fc07aa9f89d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ef067b6e-081b-87c5-387a-33dadcbe8d29"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fe5ba913-b8d0-4eb2-8bfa-9da3c5d31c40"));
        }
    }
}
