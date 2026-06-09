using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feedback_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    priority = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_by_username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    created_by_full_name = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    warehouse_no = table.Column<int>(type: "int", nullable: false),
                    warehouse_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    admin_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    read_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    read_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status_changed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status_changed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    closed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_feedback_items_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feedback_items_app_users_read_by_user_id",
                        column: x => x.read_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feedback_items_app_users_status_changed_by_user_id",
                        column: x => x.status_changed_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0ad4c479-c5f6-dab6-3a38-8187ffbdbd42"), "ortak-islemler.sikayet-oneri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > SikayetOneri > Detay yetkisi.", "SikayetOneri Detay", null },
                    { new Guid("5bfdb1d9-67f5-8b79-7578-37e30bfb6b0c"), "ortak-islemler.sikayet-oneri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > SikayetOneri > Listele yetkisi.", "SikayetOneri Listele", null },
                    { new Guid("915f0314-6cf6-d27f-8615-d17af10f8cb4"), "ortak-islemler.sikayet-oneri.list-all", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > SikayetOneri > Tumunu Listele yetkisi.", "SikayetOneri Tumunu Listele", null },
                    { new Guid("f6b34fc4-0830-b068-1c5d-c74f0db6e4af"), "ortak-islemler.sikayet-oneri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OrtakIslemler > SikayetOneri > Guncelle yetkisi.", "SikayetOneri Guncelle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0ad4c479-c5f6-dab6-3a38-8187ffbdbd42"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5bfdb1d9-67f5-8b79-7578-37e30bfb6b0c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("915f0314-6cf6-d27f-8615-d17af10f8cb4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f6b34fc4-0830-b068-1c5d-c74f0db6e4af"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_feedback_items_read_by_user_id",
                table: "feedback_items",
                column: "read_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_items_status_changed_by_user_id",
                table: "feedback_items",
                column: "status_changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_items_created_at_utc",
                table: "feedback_items",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_items_created_by_user_id",
                table: "feedback_items",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_feedback_items_warehouse_status_created",
                table: "feedback_items",
                columns: new[] { "warehouse_no", "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedback_items");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0ad4c479-c5f6-dab6-3a38-8187ffbdbd42"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5bfdb1d9-67f5-8b79-7578-37e30bfb6b0c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("915f0314-6cf6-d27f-8615-d17af10f8cb4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f6b34fc4-0830-b068-1c5d-c74f0db6e4af"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0ad4c479-c5f6-dab6-3a38-8187ffbdbd42"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5bfdb1d9-67f5-8b79-7578-37e30bfb6b0c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("915f0314-6cf6-d27f-8615-d17af10f8cb4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f6b34fc4-0830-b068-1c5d-c74f0db6e4af"));
        }
    }
}
