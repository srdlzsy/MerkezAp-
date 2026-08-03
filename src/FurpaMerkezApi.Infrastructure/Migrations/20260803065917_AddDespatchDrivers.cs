using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDespatchDrivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "despatch_drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    plate_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    tckn = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_despatch_drivers", x => x.id);
                    table.ForeignKey(
                        name: "FK_despatch_drivers_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_despatch_drivers_app_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("516956aa-1bbb-a453-95bb-d0e7a7d72fd1"), "ayar-islemleri.soforler.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Listele yetkisi.", "Soforler Listele", null },
                    { new Guid("7656d1d2-1faf-ac2d-bebf-8aa42e713fb6"), "ayar-islemleri.soforler.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Detay yetkisi.", "Soforler Detay", null },
                    { new Guid("99a13038-047d-8a50-e36d-df3264fa7d5b"), "ayar-islemleri.soforler.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Yonet yetkisi.", "Soforler Yonet", null },
                    { new Guid("a8db3e54-0a21-1767-babf-7fbf4c4865f5"), "ayar-islemleri.soforler.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Tum Depolar yetkisi.", "Soforler Tum Depolar", null },
                    { new Guid("c2b3b3f0-d35b-6789-5f05-bdbbef566c1e"), "ayar-islemleri.soforler.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Ekle yetkisi.", "Soforler Ekle", null },
                    { new Guid("dbc86f97-2bd3-c37d-6753-005783d26a58"), "ayar-islemleri.soforler.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Guncelle yetkisi.", "Soforler Guncelle", null },
                    { new Guid("e315f467-31d2-3372-2326-8004b66e604c"), "ayar-islemleri.soforler.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > Soforler > Sil yetkisi.", "Soforler Sil", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("516956aa-1bbb-a453-95bb-d0e7a7d72fd1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7656d1d2-1faf-ac2d-bebf-8aa42e713fb6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("99a13038-047d-8a50-e36d-df3264fa7d5b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a8db3e54-0a21-1767-babf-7fbf4c4865f5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c2b3b3f0-d35b-6789-5f05-bdbbef566c1e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("dbc86f97-2bd3-c37d-6753-005783d26a58"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e315f467-31d2-3372-2326-8004b66e604c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_despatch_drivers_created_by_user_id",
                table: "despatch_drivers",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_despatch_drivers_updated_by_user_id",
                table: "despatch_drivers",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_despatch_drivers_active_name",
                table: "despatch_drivers",
                columns: new[] { "is_active", "last_name", "first_name" });

            migrationBuilder.CreateIndex(
                name: "ix_despatch_drivers_active_plate",
                table: "despatch_drivers",
                columns: new[] { "is_active", "plate_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "despatch_drivers");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("516956aa-1bbb-a453-95bb-d0e7a7d72fd1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7656d1d2-1faf-ac2d-bebf-8aa42e713fb6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("99a13038-047d-8a50-e36d-df3264fa7d5b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a8db3e54-0a21-1767-babf-7fbf4c4865f5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c2b3b3f0-d35b-6789-5f05-bdbbef566c1e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("dbc86f97-2bd3-c37d-6753-005783d26a58"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e315f467-31d2-3372-2326-8004b66e604c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("516956aa-1bbb-a453-95bb-d0e7a7d72fd1"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7656d1d2-1faf-ac2d-bebf-8aa42e713fb6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("99a13038-047d-8a50-e36d-df3264fa7d5b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a8db3e54-0a21-1767-babf-7fbf4c4865f5"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c2b3b3f0-d35b-6789-5f05-bdbbef566c1e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("dbc86f97-2bd3-c37d-6753-005783d26a58"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e315f467-31d2-3372-2326-8004b66e604c"));
        }
    }
}
