using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentFlowTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_flows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    flow_key = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    source_warehouse_no = table.Column<int>(type: "int", nullable: false),
                    target_warehouse_no = table.Column<int>(type: "int", nullable: true),
                    document_serie = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    document_order_no = table.Column<int>(type: "int", nullable: false),
                    document_no = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    external_document_no = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    external_uuid = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    current_step = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    last_error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    last_changed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_flows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_flow_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_flow_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    step = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    changed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_flow_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_flow_events_document_flows_document_flow_id",
                        column: x => x.document_flow_id,
                        principalTable: "document_flows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions]
                    WHERE [id] = '3934735c-d7c4-1d56-b46d-13b97d62975e'
                       OR [code] = N'operasyon-islemleri.belge-akis-takibi.list'
                )
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('3934735c-d7c4-1d56-b46d-13b97d62975e', N'operasyon-islemleri.belge-akis-takibi.list', '2026-04-14T00:00:00.0000000Z', N'OperasyonIslemleri > BelgeAkisTakibi > Listele yetkisi.', N'BelgeAkisTakibi Listele', NULL);
                END

                IF NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions]
                    WHERE [id] = '446beaaf-9a9f-b474-8e2d-741f8fddf46d'
                       OR [code] = N'operasyon-islemleri.belge-akis-takibi.detail'
                )
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('446beaaf-9a9f-b474-8e2d-741f8fddf46d', N'operasyon-islemleri.belge-akis-takibi.detail', '2026-04-14T00:00:00.0000000Z', N'OperasyonIslemleri > BelgeAkisTakibi > Detay yetkisi.', N'BelgeAkisTakibi Detay', NULL);
                END

                IF EXISTS (
                    SELECT 1
                    FROM [app_permissions]
                    WHERE [id] = '3934735c-d7c4-1d56-b46d-13b97d62975e'
                )
                AND EXISTS (
                    SELECT 1
                    FROM [app_roles]
                    WHERE [id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions]
                    WHERE [permission_id] = '3934735c-d7c4-1d56-b46d-13b97d62975e'
                      AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                )
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('3934735c-d7c4-1d56-b46d-13b97d62975e', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', '2026-04-14T00:00:00.0000000Z');
                END

                IF EXISTS (
                    SELECT 1
                    FROM [app_permissions]
                    WHERE [id] = '446beaaf-9a9f-b474-8e2d-741f8fddf46d'
                )
                AND EXISTS (
                    SELECT 1
                    FROM [app_roles]
                    WHERE [id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions]
                    WHERE [permission_id] = '446beaaf-9a9f-b474-8e2d-741f8fddf46d'
                      AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                )
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('446beaaf-9a9f-b474-8e2d-741f8fddf46d', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', '2026-04-14T00:00:00.0000000Z');
                END
                """);

            migrationBuilder.CreateIndex(
                name: "ix_document_flow_events_flow_occurred",
                table: "document_flow_events",
                columns: new[] { "document_flow_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_document_flows_source_warehouse_updated",
                table: "document_flows",
                columns: new[] { "source_warehouse_no", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_document_flows_status_updated",
                table: "document_flows",
                columns: new[] { "status", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_document_flows_target_warehouse_updated",
                table: "document_flows",
                columns: new[] { "target_warehouse_no", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_document_flows_flow_key",
                table: "document_flows",
                column: "flow_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_flow_events");

            migrationBuilder.DropTable(
                name: "document_flows");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3934735c-d7c4-1d56-b46d-13b97d62975e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("446beaaf-9a9f-b474-8e2d-741f8fddf46d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3934735c-d7c4-1d56-b46d-13b97d62975e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("446beaaf-9a9f-b474-8e2d-741f8fddf46d"));

        }
    }
}
