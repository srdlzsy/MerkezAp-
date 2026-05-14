using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReceiptPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("1ad0ecc0-0104-8092-d75c-21f0d6cbea67"), "stok-islemleri.masraf-fisleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > MasrafFisleri > Guncelle yetkisi.", "MasrafFisleri Guncelle", null },
                    { new Guid("4eefb0f4-5dfa-d110-c4c9-377ec2b9e557"), "stok-islemleri.zayiat-fisleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > ZayiatFisleri > Listele yetkisi.", "ZayiatFisleri Listele", null },
                    { new Guid("551dc4a3-1671-7737-8441-5db4007a9456"), "stok-islemleri.zayiat-fisleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > ZayiatFisleri > Guncelle yetkisi.", "ZayiatFisleri Guncelle", null },
                    { new Guid("69eeeee9-957b-506c-3f91-26ab2902491f"), "stok-islemleri.zayiat-fisleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > ZayiatFisleri > Detay yetkisi.", "ZayiatFisleri Detay", null },
                    { new Guid("96e8b4d5-f916-b795-949d-f29ec158a525"), "stok-islemleri.masraf-fisleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > MasrafFisleri > Ekle yetkisi.", "MasrafFisleri Ekle", null },
                    { new Guid("9baca579-5aad-8b56-b936-f3a40a4dea01"), "stok-islemleri.masraf-fisleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > MasrafFisleri > Detay yetkisi.", "MasrafFisleri Detay", null },
                    { new Guid("caedac4a-c1d2-eb55-2bf2-856a0e150a82"), "stok-islemleri.masraf-fisleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > MasrafFisleri > Listele yetkisi.", "MasrafFisleri Listele", null },
                    { new Guid("d7a0a0af-df8a-3644-7ab0-f48132de0b30"), "stok-islemleri.zayiat-fisleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > ZayiatFisleri > Ekle yetkisi.", "ZayiatFisleri Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("1ad0ecc0-0104-8092-d75c-21f0d6cbea67"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4eefb0f4-5dfa-d110-c4c9-377ec2b9e557"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("551dc4a3-1671-7737-8441-5db4007a9456"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("69eeeee9-957b-506c-3f91-26ab2902491f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("96e8b4d5-f916-b795-949d-f29ec158a525"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9baca579-5aad-8b56-b936-f3a40a4dea01"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("caedac4a-c1d2-eb55-2bf2-856a0e150a82"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d7a0a0af-df8a-3644-7ab0-f48132de0b30"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1ad0ecc0-0104-8092-d75c-21f0d6cbea67"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4eefb0f4-5dfa-d110-c4c9-377ec2b9e557"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("551dc4a3-1671-7737-8441-5db4007a9456"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("69eeeee9-957b-506c-3f91-26ab2902491f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("96e8b4d5-f916-b795-949d-f29ec158a525"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9baca579-5aad-8b56-b936-f3a40a4dea01"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("caedac4a-c1d2-eb55-2bf2-856a0e150a82"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d7a0a0af-df8a-3644-7ab0-f48132de0b30"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1ad0ecc0-0104-8092-d75c-21f0d6cbea67"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4eefb0f4-5dfa-d110-c4c9-377ec2b9e557"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("551dc4a3-1671-7737-8441-5db4007a9456"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("69eeeee9-957b-506c-3f91-26ab2902491f"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("96e8b4d5-f916-b795-949d-f29ec158a525"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9baca579-5aad-8b56-b936-f3a40a4dea01"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("caedac4a-c1d2-eb55-2bf2-856a0e150a82"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d7a0a0af-df8a-3644-7ab0-f48132de0b30"));
        }
    }
}
