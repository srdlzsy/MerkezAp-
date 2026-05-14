using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitMalKabulMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"));

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("065e9112-4ec2-7b76-013c-753cf9674582"), "mal-kabul-islemleri.depo-mal-kabulleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > DepoMalKabulleri > Ekle yetkisi.", "DepoMalKabulleri Ekle", null },
                    { new Guid("1a676d0e-9d7c-08fb-bb4d-88edb66fb439"), "mal-kabul-islemleri.depo-mal-kabulleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > DepoMalKabulleri > Listele yetkisi.", "DepoMalKabulleri Listele", null },
                    { new Guid("5a7e4dfa-6dec-6339-324f-f3d684ef6b36"), "mal-kabul-islemleri.firma-mal-kabulleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > FirmaMalKabulleri > Ekle yetkisi.", "FirmaMalKabulleri Ekle", null },
                    { new Guid("8c18bd9c-86d2-c41f-eaf1-e4eebcc93bea"), "mal-kabul-islemleri.firma-mal-kabulleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > FirmaMalKabulleri > Guncelle yetkisi.", "FirmaMalKabulleri Guncelle", null },
                    { new Guid("9c0889d4-600f-089e-cecc-5fa3ccbb8c57"), "mal-kabul-islemleri.firma-mal-kabulleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > FirmaMalKabulleri > Listele yetkisi.", "FirmaMalKabulleri Listele", null },
                    { new Guid("cdea9ac6-a530-3804-d369-99214578b66a"), "mal-kabul-islemleri.depo-mal-kabulleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > DepoMalKabulleri > Detay yetkisi.", "DepoMalKabulleri Detay", null },
                    { new Guid("e167dd2b-c9dc-0e2b-d342-ec511c9bd7e8"), "mal-kabul-islemleri.depo-mal-kabulleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > DepoMalKabulleri > Guncelle yetkisi.", "DepoMalKabulleri Guncelle", null },
                    { new Guid("f9274556-d428-460f-c001-fc5408aa816d"), "mal-kabul-islemleri.firma-mal-kabulleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > FirmaMalKabulleri > Detay yetkisi.", "FirmaMalKabulleri Detay", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("065e9112-4ec2-7b76-013c-753cf9674582"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("1a676d0e-9d7c-08fb-bb4d-88edb66fb439"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5a7e4dfa-6dec-6339-324f-f3d684ef6b36"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8c18bd9c-86d2-c41f-eaf1-e4eebcc93bea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9c0889d4-600f-089e-cecc-5fa3ccbb8c57"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("cdea9ac6-a530-3804-d369-99214578b66a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e167dd2b-c9dc-0e2b-d342-ec511c9bd7e8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f9274556-d428-460f-c001-fc5408aa816d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("065e9112-4ec2-7b76-013c-753cf9674582"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1a676d0e-9d7c-08fb-bb4d-88edb66fb439"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5a7e4dfa-6dec-6339-324f-f3d684ef6b36"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8c18bd9c-86d2-c41f-eaf1-e4eebcc93bea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9c0889d4-600f-089e-cecc-5fa3ccbb8c57"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("cdea9ac6-a530-3804-d369-99214578b66a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e167dd2b-c9dc-0e2b-d342-ec511c9bd7e8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f9274556-d428-460f-c001-fc5408aa816d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("065e9112-4ec2-7b76-013c-753cf9674582"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1a676d0e-9d7c-08fb-bb4d-88edb66fb439"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5a7e4dfa-6dec-6339-324f-f3d684ef6b36"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c18bd9c-86d2-c41f-eaf1-e4eebcc93bea"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9c0889d4-600f-089e-cecc-5fa3ccbb8c57"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("cdea9ac6-a530-3804-d369-99214578b66a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e167dd2b-c9dc-0e2b-d342-ec511c9bd7e8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f9274556-d428-460f-c001-fc5408aa816d"));

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"), "mal-kabul-islemleri.mal-kabuller.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Ekle yetkisi.", "MalKabuller Ekle", null },
                    { new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"), "mal-kabul-islemleri.mal-kabuller.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Listele yetkisi.", "MalKabuller Listele", null },
                    { new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"), "mal-kabul-islemleri.mal-kabuller.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Guncelle yetkisi.", "MalKabuller Guncelle", null },
                    { new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"), "mal-kabul-islemleri.mal-kabuller.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Detay yetkisi.", "MalKabuller Detay", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
