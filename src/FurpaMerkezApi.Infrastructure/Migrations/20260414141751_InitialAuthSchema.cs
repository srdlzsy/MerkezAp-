using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    warehouse_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    warehouse_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_app_role_permissions_app_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "app_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_app_role_permissions_app_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "app_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_app_user_roles_app_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "app_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_app_user_roles_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0c01e5d4-f564-4e22-9454-a5594ec4b38c"), "customers.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List customers from Mikro.", "Read Customers", null },
                    { new Guid("119a5e97-4947-4c87-9ffd-2d35e343ef53"), "roles.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update roles.", "Manage Roles", null },
                    { new Guid("4bdc2240-57a0-499d-afb2-3572aa249d53"), "customers.write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update customers in Mikro.", "Write Customers", null },
                    { new Guid("69829857-f745-4280-a0cf-7ca8776562ea"), "warehouses.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List warehouses from Mikro.", "Read Warehouses", null },
                    { new Guid("79925722-5c18-4db4-9c7d-c44d6f6fd779"), "permissions.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update permissions.", "Manage Permissions", null },
                    { new Guid("7ab07891-823b-4b09-ba69-9e75345f890c"), "warehouses.write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update warehouses in Mikro.", "Write Warehouses", null },
                    { new Guid("ab3f01c7-4f39-4840-81f3-c95557d5791c"), "warehouse-stock-rules.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List warehouse stock rule records from Mikro.", "Read Warehouse Stock Rules", null },
                    { new Guid("c2c4e6ce-ad5f-4180-8aa4-0ff687462865"), "products.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List products from Mikro.", "Read Products", null },
                    { new Guid("d9105cee-f29e-4783-bdb0-25d47980fb95"), "warehouse-stock-rules.write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update warehouse stock rule records in Mikro.", "Write Warehouse Stock Rules", null },
                    { new Guid("fdf63a66-e9b4-4ca5-8700-2a6a34231c01"), "users.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Update users and assign roles.", "Manage Users", null }
                });

            migrationBuilder.InsertData(
                table: "app_roles",
                columns: new[] { "id", "created_at_utc", "description", "is_active", "name", "updated_at_utc" },
                values: new object[] { new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "System administrator role with all permissions.", true, "Administrator", null });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0c01e5d4-f564-4e22-9454-a5594ec4b38c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("119a5e97-4947-4c87-9ffd-2d35e343ef53"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4bdc2240-57a0-499d-afb2-3572aa249d53"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("69829857-f745-4280-a0cf-7ca8776562ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("79925722-5c18-4db4-9c7d-c44d6f6fd779"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7ab07891-823b-4b09-ba69-9e75345f890c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ab3f01c7-4f39-4840-81f3-c95557d5791c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c2c4e6ce-ad5f-4180-8aa4-0ff687462865"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d9105cee-f29e-4783-bdb0-25d47980fb95"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("fdf63a66-e9b4-4ca5-8700-2a6a34231c01"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ux_app_permissions_code",
                table: "app_permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_role_permissions_permission_id",
                table: "app_role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ux_app_roles_name",
                table: "app_roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_roles_role_id",
                table: "app_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_app_users_normalized_email",
                table: "app_users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_app_users_normalized_username",
                table: "app_users",
                column: "normalized_username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_role_permissions");

            migrationBuilder.DropTable(
                name: "app_user_roles");

            migrationBuilder.DropTable(
                name: "app_permissions");

            migrationBuilder.DropTable(
                name: "app_roles");

            migrationBuilder.DropTable(
                name: "app_users");
        }
    }
}
