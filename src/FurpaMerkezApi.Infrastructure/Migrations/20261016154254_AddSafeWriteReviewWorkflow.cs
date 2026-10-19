using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSafeWriteReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "safe_write_review_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    safe_write_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reviewed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    previous_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    previous_error_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    result_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    result_error_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_safe_write_review_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_safe_write_review_audits_app_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_safe_write_review_audits_mobile_offline_sync_requests_safe_write_id",
                        column: x => x.safe_write_id,
                        principalTable: "mobile_offline_sync_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[] { new Guid("867eb4b4-d079-1d86-4d37-0ca5ff26855d"), "ayar-islemleri.veritabani-izleme.resolve-safe-write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > VeritabaniIzleme > Guvenli Yazmayi Sonuclandir yetkisi.", "VeritabaniIzleme Guvenli Yazmayi Sonuclandir", null });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[] { new Guid("867eb4b4-d079-1d86-4d37-0ca5ff26855d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_safe_write_review_audits_reviewed_by_user_id",
                table: "safe_write_review_audits",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_safe_write_review_audits_reviewed_at",
                table: "safe_write_review_audits",
                column: "reviewed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_safe_write_review_audits_write_reviewed",
                table: "safe_write_review_audits",
                columns: new[] { "safe_write_id", "reviewed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "safe_write_review_audits");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("867eb4b4-d079-1d86-4d37-0ca5ff26855d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("867eb4b4-d079-1d86-4d37-0ca5ff26855d"));
        }
    }
}
