using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260424095000_AddLabelDocumentPermissions")]
    public partial class AddLabelDocumentPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                columnTypes: new[] { "uuid", "character varying(100)", "timestamp with time zone", "character varying(250)", "character varying(100)", "timestamp with time zone" },
                values: new object[,]
                {
                    { new Guid("625b8bb0-4009-8ebe-8071-300c19ec095e"), "stok-islemleri.etiket-belgeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > EtiketBelgeleri > Ekle yetkisi.", "EtiketBelgeleri Ekle", null },
                    { new Guid("fbc155f3-f37d-a25e-3a0b-74628238f508"), "stok-islemleri.etiket-belgeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > EtiketBelgeleri > Detay yetkisi.", "EtiketBelgeleri Detay", null },
                    { new Guid("07f77149-c49d-0e3b-f04a-9a24698d1a05"), "stok-islemleri.etiket-belgeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > EtiketBelgeleri > Listele yetkisi.", "EtiketBelgeleri Listele", null },
                    { new Guid("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"), "stok-islemleri.etiket-belgeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > EtiketBelgeleri > Guncelle yetkisi.", "EtiketBelgeleri Guncelle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                columnTypes: new[] { "uuid", "uuid", "timestamp with time zone" },
                values: new object[,]
                {
                    { new Guid("625b8bb0-4009-8ebe-8071-300c19ec095e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("fbc155f3-f37d-a25e-3a0b-74628238f508"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("07f77149-c49d-0e3b-f04a-9a24698d1a05"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("625b8bb0-4009-8ebe-8071-300c19ec095e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fbc155f3-f37d-a25e-3a0b-74628238f508"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("07f77149-c49d-0e3b-f04a-9a24698d1a05"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("625b8bb0-4009-8ebe-8071-300c19ec095e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fbc155f3-f37d-a25e-3a0b-74628238f508"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("07f77149-c49d-0e3b-f04a-9a24698d1a05"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"));
        }
    }
}
