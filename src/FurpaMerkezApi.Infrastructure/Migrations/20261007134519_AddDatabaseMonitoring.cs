using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "database_monitoring_incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    session_id = table.Column<int>(type: "int", nullable: false),
                    blocking_session_id = table.Column<int>(type: "int", nullable: true),
                    database_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    login_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    host_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    program_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    wait_type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    elapsed_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    sql_text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    recommendation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    first_seen_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    occurrence_count = table.Column<int>(type: "int", nullable: false),
                    resolved_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_monitoring_incidents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "database_session_termination_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    session_id = table.Column<int>(type: "int", nullable: false),
                    login_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    host_process_id = table.Column<int>(type: "int", nullable: true),
                    login_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    host_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    program_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    database_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    sql_text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_succeeded = table.Column<bool>(type: "bit", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_session_termination_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_database_session_termination_audits_app_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("353f74f3-dea9-070f-072d-3040e8d597cf"), "ayar-islemleri.veritabani-izleme.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > VeritabaniIzleme > Yonet yetkisi.", "VeritabaniIzleme Yonet", null },
                    { new Guid("5119a720-a70b-bebc-ef60-fc08429d4bc9"), "ayar-islemleri.veritabani-izleme.terminate-session", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > VeritabaniIzleme > Oturum Sonlandir yetkisi.", "VeritabaniIzleme Oturum Sonlandir", null },
                    { new Guid("5e387039-cc94-53a7-2dfc-8f2d74073f4f"), "ayar-islemleri.veritabani-izleme.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > VeritabaniIzleme > Detay yetkisi.", "VeritabaniIzleme Detay", null },
                    { new Guid("a1824ee7-545c-3ef8-36e6-d340e314d9df"), "ayar-islemleri.veritabani-izleme.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > VeritabaniIzleme > Listele yetkisi.", "VeritabaniIzleme Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("353f74f3-dea9-070f-072d-3040e8d597cf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5119a720-a70b-bebc-ef60-fc08429d4bc9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5e387039-cc94-53a7-2dfc-8f2d74073f4f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1824ee7-545c-3ef8-36e6-d340e314d9df"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_database_monitoring_incidents_active_last_seen",
                table: "database_monitoring_incidents",
                columns: new[] { "resolved_at_utc", "last_seen_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_database_monitoring_incidents_fingerprint",
                table: "database_monitoring_incidents",
                column: "fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_session_termination_audits_requested_by_user_id",
                table: "database_session_termination_audits",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_database_session_termination_audits_requested_at",
                table: "database_session_termination_audits",
                column: "requested_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_database_session_termination_audits_session_requested",
                table: "database_session_termination_audits",
                columns: new[] { "session_id", "requested_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "database_monitoring_incidents");

            migrationBuilder.DropTable(
                name: "database_session_termination_audits");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("353f74f3-dea9-070f-072d-3040e8d597cf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5119a720-a70b-bebc-ef60-fc08429d4bc9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5e387039-cc94-53a7-2dfc-8f2d74073f4f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a1824ee7-545c-3ef8-36e6-d340e314d9df"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("353f74f3-dea9-070f-072d-3040e8d597cf"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5119a720-a70b-bebc-ef60-fc08429d4bc9"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5e387039-cc94-53a7-2dfc-8f2d74073f4f"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a1824ee7-545c-3ef8-36e6-d340e314d9df"));
        }
    }
}
