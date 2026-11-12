using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendyolGoManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trendyol_go_categories",
                columns: table => new
                {
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    parent_category_id = table.Column<long>(type: "bigint", nullable: true),
                    path = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    is_leaf = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    synced_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendyol_go_categories", x => x.category_id);
                });

            migrationBuilder.CreateTable(
                name: "trendyol_go_category_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    mapping_key = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    mikro_category_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    mikro_main_group_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendyol_go_category_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trendyol_go_product_category_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stock_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendyol_go_product_category_overrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trendyol_go_runtime_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    price_list_no = table.Column<int>(type: "int", nullable: false),
                    payment_plan_no = table.Column<int>(type: "int", nullable: false),
                    stock_reserve_quantity = table.Column<int>(type: "int", nullable: false),
                    branch_pos_price_sync_enabled = table.Column<bool>(type: "bit", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendyol_go_runtime_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trendyol_go_store_settings",
                columns: table => new
                {
                    store_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_no = table.Column<int>(type: "int", nullable: false),
                    store_name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    stock_reserve_quantity = table.Column<int>(type: "int", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    updated_by = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendyol_go_store_settings", x => x.store_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_categories_is_active_is_leaf_name",
                table: "trendyol_go_categories",
                columns: new[] { "is_active", "is_leaf", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_category_mappings_is_active_category_id",
                table: "trendyol_go_category_mappings",
                columns: new[] { "is_active", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_category_mappings_mapping_key",
                table: "trendyol_go_category_mappings",
                column: "mapping_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_product_category_overrides_is_active_category_id",
                table: "trendyol_go_product_category_overrides",
                columns: new[] { "is_active", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_product_category_overrides_stock_code",
                table: "trendyol_go_product_category_overrides",
                column: "stock_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_store_settings_is_active",
                table: "trendyol_go_store_settings",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_store_settings_warehouse_no",
                table: "trendyol_go_store_settings",
                column: "warehouse_no",
                unique: true,
                filter: "[is_active] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trendyol_go_categories");

            migrationBuilder.DropTable(
                name: "trendyol_go_category_mappings");

            migrationBuilder.DropTable(
                name: "trendyol_go_product_category_overrides");

            migrationBuilder.DropTable(
                name: "trendyol_go_runtime_settings");

            migrationBuilder.DropTable(
                name: "trendyol_go_store_settings");
        }
    }
}
