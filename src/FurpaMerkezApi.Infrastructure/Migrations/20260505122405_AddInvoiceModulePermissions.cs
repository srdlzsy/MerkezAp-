using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceModulePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("ec6256b1-82a1-3d0e-b592-fe8a83472d85"), "fatura-islemleri.fatura-goruntuleme.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGoruntuleme > Listele yetkisi.", "FaturaGoruntuleme Listele", null },
                    { new Guid("d6104765-ba28-c852-f07d-907c227b0f0c"), "fatura-islemleri.fatura-goruntuleme.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGoruntuleme > Detay yetkisi.", "FaturaGoruntuleme Detay", null },
                    { new Guid("7428ce6c-f000-a466-4c1e-8f6659ea3014"), "fatura-islemleri.fatura-gonderimi.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGonderimi > Listele yetkisi.", "FaturaGonderimi Listele", null },
                    { new Guid("b1efeaa5-e984-38ec-7bb0-cbacbe7b63dd"), "fatura-islemleri.fatura-gonderimi.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGonderimi > Detay yetkisi.", "FaturaGonderimi Detay", null },
                    { new Guid("b7d484d7-6c4d-306b-9700-407c2b8dcc19"), "fatura-islemleri.fatura-gonderimi.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "FaturaIslemleri > FaturaGonderimi > Ekle yetkisi.", "FaturaGonderimi Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("ec6256b1-82a1-3d0e-b592-fe8a83472d85"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d6104765-ba28-c852-f07d-907c227b0f0c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7428ce6c-f000-a466-4c1e-8f6659ea3014"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b1efeaa5-e984-38ec-7bb0-cbacbe7b63dd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b7d484d7-6c4d-306b-9700-407c2b8dcc19"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ec6256b1-82a1-3d0e-b592-fe8a83472d85"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d6104765-ba28-c852-f07d-907c227b0f0c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7428ce6c-f000-a466-4c1e-8f6659ea3014"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b1efeaa5-e984-38ec-7bb0-cbacbe7b63dd"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b7d484d7-6c4d-306b-9700-407c2b8dcc19"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ec6256b1-82a1-3d0e-b592-fe8a83472d85"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d6104765-ba28-c852-f07d-907c227b0f0c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7428ce6c-f000-a466-4c1e-8f6659ea3014"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b1efeaa5-e984-38ec-7bb0-cbacbe7b63dd"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b7d484d7-6c4d-306b-9700-407c2b8dcc19"));
        }
    }
}
