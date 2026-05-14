using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKunyeEtiketYazdirmaPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("91f4518e-007d-ca11-e8b4-03967a2b4624"), "stok-islemleri.kunye-etiket-yazdirma.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > KunyeEtiketYazdirma > Guncelle yetkisi.", "KunyeEtiketYazdirma Guncelle", null },
                    { new Guid("c80495b3-55cf-b620-df21-3b052d3bfdcb"), "stok-islemleri.kunye-etiket-yazdirma.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > KunyeEtiketYazdirma > Detay yetkisi.", "KunyeEtiketYazdirma Detay", null },
                    { new Guid("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"), "stok-islemleri.kunye-etiket-yazdirma.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > KunyeEtiketYazdirma > Listele yetkisi.", "KunyeEtiketYazdirma Listele", null },
                    { new Guid("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"), "stok-islemleri.kunye-etiket-yazdirma.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > KunyeEtiketYazdirma > Ekle yetkisi.", "KunyeEtiketYazdirma Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("91f4518e-007d-ca11-e8b4-03967a2b4624"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c80495b3-55cf-b620-df21-3b052d3bfdcb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("91f4518e-007d-ca11-e8b4-03967a2b4624"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c80495b3-55cf-b620-df21-3b052d3bfdcb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("91f4518e-007d-ca11-e8b4-03967a2b4624"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c80495b3-55cf-b620-df21-3b052d3bfdcb"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"));
        }
    }
}
