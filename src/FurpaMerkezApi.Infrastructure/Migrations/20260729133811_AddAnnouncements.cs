using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    priority = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_by_username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_by_full_name = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    starts_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    published_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    archived_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                    table.ForeignKey(
                        name: "FK_announcements_app_users_archived_by_user_id",
                        column: x => x.archived_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_announcements_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "announcement_reads",
                columns: table => new
                {
                    announcement_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_reads", x => new { x.announcement_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_announcement_reads_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_announcement_reads_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "announcement_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    announcement_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    warehouse_no = table.Column<int>(type: "int", nullable: true),
                    warehouse_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    user_full_name = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_announcement_targets_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_announcement_targets_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("12200d80-8274-ef33-b164-70c368e92e59"), "ortak-islemler.duyurular.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Detay yetkisi.", "Duyurular Detay", null },
                    { new Guid("198188bd-8c24-cb09-2ff4-baeddcc9d751"), "ortak-islemler.duyurular.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Listele yetkisi.", "Duyurular Listele", null },
                    { new Guid("2e3219cf-67e2-d80e-99bd-5a89caec0175"), "ortak-islemler.duyurular.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Guncelle yetkisi.", "Duyurular Guncelle", null },
                    { new Guid("54394721-cd97-ce15-eef3-fbbee5ac5cbf"), "ortak-islemler.duyurular.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Tum Depolar yetkisi.", "Duyurular Tum Depolar", null },
                    { new Guid("8b932892-b9f9-9d15-f994-94a7ed19f4f9"), "ortak-islemler.duyurular.archive", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Arsivle yetkisi.", "Duyurular Arsivle", null },
                    { new Guid("d164fd9b-bca2-7fbc-f3e3-13c4c8fd6dc7"), "ortak-islemler.duyurular.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > Duyurular > Ekle yetkisi.", "Duyurular Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("12200d80-8274-ef33-b164-70c368e92e59"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("198188bd-8c24-cb09-2ff4-baeddcc9d751"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2e3219cf-67e2-d80e-99bd-5a89caec0175"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("54394721-cd97-ce15-eef3-fbbee5ac5cbf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8b932892-b9f9-9d15-f994-94a7ed19f4f9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d164fd9b-bca2-7fbc-f3e3-13c4c8fd6dc7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_reads_user_read_at",
                table: "announcement_reads",
                columns: new[] { "user_id", "read_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_announcement_id",
                table: "announcement_targets",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_type_warehouse",
                table: "announcement_targets",
                columns: new[] { "type", "warehouse_no" });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_user_id",
                table: "announcement_targets",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_archived_by_user_id",
                table: "announcements",
                column: "archived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_created_by_user_id",
                table: "announcements",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_status_published_at",
                table: "announcements",
                columns: new[] { "status", "published_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_reads");

            migrationBuilder.DropTable(
                name: "announcement_targets");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("12200d80-8274-ef33-b164-70c368e92e59"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("198188bd-8c24-cb09-2ff4-baeddcc9d751"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2e3219cf-67e2-d80e-99bd-5a89caec0175"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("54394721-cd97-ce15-eef3-fbbee5ac5cbf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8b932892-b9f9-9d15-f994-94a7ed19f4f9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d164fd9b-bca2-7fbc-f3e3-13c4c8fd6dc7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("12200d80-8274-ef33-b164-70c368e92e59"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("198188bd-8c24-cb09-2ff4-baeddcc9d751"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2e3219cf-67e2-d80e-99bd-5a89caec0175"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("54394721-cd97-ce15-eef3-fbbee5ac5cbf"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8b932892-b9f9-9d15-f994-94a7ed19f4f9"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d164fd9b-bca2-7fbc-f3e3-13c4c8fd6dc7"));
        }
    }
}
