using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAnomalyCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_anomalies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_key = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    warehouse_no = table.Column<int>(type: "int", nullable: false),
                    related_warehouse_no = table.Column<int>(type: "int", nullable: true),
                    warehouse_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    related_warehouse_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    product_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    product_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    document_serie = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    document_order_no = table.Column<int>(type: "int", nullable: true),
                    document_no = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    movement_guid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    quantity = table.Column<double>(type: "float", nullable: true),
                    expected_quantity = table.Column<double>(type: "float", nullable: true),
                    actual_quantity = table.Column<double>(type: "float", nullable: true),
                    average_quantity = table.Column<double>(type: "float", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    evidence = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    last_changed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    first_detected_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_detected_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    resolved_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_anomalies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_anomaly_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stock_anomaly_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_anomaly_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_anomaly_events_stock_anomalies_stock_anomaly_id",
                        column: x => x.stock_anomaly_id,
                        principalTable: "stock_anomalies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                DECLARE @AdminRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @CreatedAt datetime2 = '2026-04-14T00:00:00';

                DECLARE @Permissions TABLE (
                    Id uniqueidentifier NOT NULL,
                    Code nvarchar(160) NOT NULL,
                    Name nvarchar(200) NOT NULL,
                    Description nvarchar(500) NOT NULL
                );

                INSERT INTO @Permissions (Id, Code, Name, Description)
                VALUES
                    ('cf2bf8d2-64ca-a81d-d412-84c996282d08', N'stok-islemleri.stok-anomali-merkezi.list', N'StokAnomaliMerkezi Listele', N'StokIslemleri > StokAnomaliMerkezi > Listele yetkisi.'),
                    ('21cc55c5-90e0-0a26-3d4e-52904af688cb', N'stok-islemleri.stok-anomali-merkezi.detail', N'StokAnomaliMerkezi Detay', N'StokIslemleri > StokAnomaliMerkezi > Detay yetkisi.'),
                    ('9b024d9f-c340-b2e6-091e-da66431e4a8a', N'stok-islemleri.stok-anomali-merkezi.update', N'StokAnomaliMerkezi Guncelle', N'StokIslemleri > StokAnomaliMerkezi > Guncelle yetkisi.'),
                    ('d1dfa395-df33-6d80-054a-dcd6b77b2b97', N'stok-islemleri.stok-anomali-merkezi.scan', N'StokAnomaliMerkezi Tara', N'StokIslemleri > StokAnomaliMerkezi > Tara yetkisi.');

                INSERT INTO app_permissions (id, code, created_at_utc, description, name, updated_at_utc)
                SELECT source.Id, source.Code, @CreatedAt, source.Description, source.Name, NULL
                FROM @Permissions AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM app_permissions AS existing
                    WHERE existing.code = source.Code OR existing.id = source.Id
                );

                INSERT INTO app_role_permissions (permission_id, role_id, assigned_at_utc)
                SELECT permission.id, @AdminRoleId, @CreatedAt
                FROM @Permissions AS source
                INNER JOIN app_permissions AS permission ON permission.code = source.Code
                WHERE EXISTS (
                    SELECT 1
                    FROM app_roles AS role
                    WHERE role.id = @AdminRoleId
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM app_role_permissions AS existing
                    WHERE existing.permission_id = permission.id
                      AND existing.role_id = @AdminRoleId
                );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_stock_anomalies_type_status_last_detected",
                table: "stock_anomalies",
                columns: new[] { "type", "status", "last_detected_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_anomalies_warehouse_status_last_detected",
                table: "stock_anomalies",
                columns: new[] { "warehouse_no", "status", "last_detected_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_stock_anomalies_source_key",
                table: "stock_anomalies",
                column: "source_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_anomaly_events_anomaly_occurred",
                table: "stock_anomaly_events",
                columns: new[] { "stock_anomaly_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_anomaly_events");

            migrationBuilder.DropTable(
                name: "stock_anomalies");

            migrationBuilder.Sql("""
                DELETE rolePermission
                FROM app_role_permissions AS rolePermission
                INNER JOIN app_permissions AS permission ON permission.id = rolePermission.permission_id
                WHERE permission.code IN (
                    N'stok-islemleri.stok-anomali-merkezi.list',
                    N'stok-islemleri.stok-anomali-merkezi.detail',
                    N'stok-islemleri.stok-anomali-merkezi.update',
                    N'stok-islemleri.stok-anomali-merkezi.scan'
                );

                DELETE FROM app_permissions
                WHERE code IN (
                    N'stok-islemleri.stok-anomali-merkezi.list',
                    N'stok-islemleri.stok-anomali-merkezi.detail',
                    N'stok-islemleri.stok-anomali-merkezi.update',
                    N'stok-islemleri.stok-anomali-merkezi.scan'
                );
                """);
        }
    }
}
