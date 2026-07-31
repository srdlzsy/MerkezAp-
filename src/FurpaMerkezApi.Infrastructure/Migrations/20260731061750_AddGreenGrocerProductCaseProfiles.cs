using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGreenGrocerProductCaseProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "green_grocer_order_line_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    warehouse_order_line_guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_serie = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    document_order_no = table.Column<int>(type: "int", nullable: false),
                    row_no = table.Column<int>(type: "int", nullable: false),
                    order_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    source_warehouse_no = table.Column<int>(type: "int", nullable: false),
                    target_warehouse_no = table.Column<int>(type: "int", nullable: false),
                    stock_code = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    input_quantity = table.Column<double>(type: "float", nullable: false),
                    input_mode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    conversion_mode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    average_kg_per_case = table.Column<double>(type: "float", nullable: true),
                    units_per_case = table.Column<double>(type: "float", nullable: true),
                    estimated_quantity = table.Column<double>(type: "float", nullable: false),
                    micro_unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    average_source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    average_record_count = table.Column<int>(type: "int", nullable: true),
                    average_case_count = table.Column<int>(type: "int", nullable: true),
                    coefficient_of_variation = table.Column<double>(type: "float", nullable: true),
                    confidence = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    actual_shipped_quantity = table.Column<double>(type: "float", nullable: true),
                    actual_shipped_case_count = table.Column<double>(type: "float", nullable: true),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_green_grocer_order_line_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_green_grocer_order_line_snapshots_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_green_grocer_order_line_snapshots_app_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "green_grocer_product_case_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    stock_code = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    input_mode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    conversion_mode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    manual_kg_per_case = table.Column<double>(type: "float", nullable: true),
                    manual_units_per_case = table.Column<double>(type: "float", nullable: true),
                    min_expected_kg_per_case = table.Column<double>(type: "float", nullable: true),
                    max_expected_kg_per_case = table.Column<double>(type: "float", nullable: true),
                    average_window_days = table.Column<int>(type: "int", nullable: false),
                    min_average_record_count = table.Column<int>(type: "int", nullable: false),
                    min_average_case_count = table.Column<int>(type: "int", nullable: false),
                    max_coefficient_of_variation = table.Column<double>(type: "float", nullable: false),
                    requires_manual_approval = table.Column<bool>(type: "bit", nullable: false),
                    allow_order_linking = table.Column<bool>(type: "bit", nullable: false),
                    over_delivery_tolerance_percent = table.Column<double>(type: "float", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_green_grocer_product_case_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_green_grocer_product_case_profiles_app_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_green_grocer_product_case_profiles_app_users_updated_by_user_id",
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
                    { new Guid("1e67c648-d9f5-df94-0c66-6c3bdb2cf55a"), "green-grocer.product-case-profiles.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Guncelle yetkisi.", "ProductCaseProfiles Guncelle", null },
                    { new Guid("2ef0061f-7c35-03ae-f62d-0031ce84c39d"), "green-grocer.product-case-profiles.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Tum Depolar yetkisi.", "ProductCaseProfiles Tum Depolar", null },
                    { new Guid("3f3280e7-38b2-bc34-8670-f79cee1179c3"), "green-grocer.product-case-profiles.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Detay yetkisi.", "ProductCaseProfiles Detay", null },
                    { new Guid("4811d3bc-016f-415b-0807-92d30c3c5597"), "green-grocer.product-case-profiles.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Sil yetkisi.", "ProductCaseProfiles Sil", null },
                    { new Guid("8c21c804-6071-72c7-3ff6-b9a659684b0b"), "green-grocer.product-case-profiles.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Listele yetkisi.", "ProductCaseProfiles Listele", null },
                    { new Guid("fe4c9cef-8678-040d-1af7-41236e9da805"), "green-grocer.product-case-profiles.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > ProductCaseProfiles > Ekle yetkisi.", "ProductCaseProfiles Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("1e67c648-d9f5-df94-0c66-6c3bdb2cf55a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2ef0061f-7c35-03ae-f62d-0031ce84c39d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("3f3280e7-38b2-bc34-8670-f79cee1179c3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4811d3bc-016f-415b-0807-92d30c3c5597"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8c21c804-6071-72c7-3ff6-b9a659684b0b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("fe4c9cef-8678-040d-1af7-41236e9da805"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_green_grocer_order_line_snapshots_created_by_user_id",
                table: "green_grocer_order_line_snapshots",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_green_grocer_order_line_snapshots_updated_by_user_id",
                table: "green_grocer_order_line_snapshots",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_green_grocer_order_snapshots_date_target_stock",
                table: "green_grocer_order_line_snapshots",
                columns: new[] { "order_date", "target_warehouse_no", "stock_code" });

            migrationBuilder.CreateIndex(
                name: "ix_green_grocer_order_snapshots_document",
                table: "green_grocer_order_line_snapshots",
                columns: new[] { "document_serie", "document_order_no" });

            migrationBuilder.CreateIndex(
                name: "ux_green_grocer_order_snapshots_order_line_guid",
                table: "green_grocer_order_line_snapshots",
                column: "warehouse_order_line_guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_green_grocer_product_case_profiles_created_by_user_id",
                table: "green_grocer_product_case_profiles",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_green_grocer_product_case_profiles_updated_by_user_id",
                table: "green_grocer_product_case_profiles",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_green_grocer_product_case_profiles_active_stock_code",
                table: "green_grocer_product_case_profiles",
                columns: new[] { "is_active", "stock_code" });

            migrationBuilder.CreateIndex(
                name: "ux_green_grocer_product_case_profiles_stock_code",
                table: "green_grocer_product_case_profiles",
                column: "stock_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "green_grocer_order_line_snapshots");

            migrationBuilder.DropTable(
                name: "green_grocer_product_case_profiles");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1e67c648-d9f5-df94-0c66-6c3bdb2cf55a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2ef0061f-7c35-03ae-f62d-0031ce84c39d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3f3280e7-38b2-bc34-8670-f79cee1179c3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4811d3bc-016f-415b-0807-92d30c3c5597"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8c21c804-6071-72c7-3ff6-b9a659684b0b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("fe4c9cef-8678-040d-1af7-41236e9da805"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1e67c648-d9f5-df94-0c66-6c3bdb2cf55a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2ef0061f-7c35-03ae-f62d-0031ce84c39d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3f3280e7-38b2-bc34-8670-f79cee1179c3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4811d3bc-016f-415b-0807-92d30c3c5597"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c21c804-6071-72c7-3ff6-b9a659684b0b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fe4c9cef-8678-040d-1af7-41236e9da805"));
        }
    }
}
