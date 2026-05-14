using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructurePermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0c01e5d4-f564-4e22-9454-a5594ec4b38c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4bdc2240-57a0-499d-afb2-3572aa249d53"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("69829857-f745-4280-a0cf-7ca8776562ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7ab07891-823b-4b09-ba69-9e75345f890c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ab3f01c7-4f39-4840-81f3-c95557d5791c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c2c4e6ce-ad5f-4180-8aa4-0ff687462865"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d9105cee-f29e-4783-bdb0-25d47980fb95"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0c01e5d4-f564-4e22-9454-a5594ec4b38c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4bdc2240-57a0-499d-afb2-3572aa249d53"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("69829857-f745-4280-a0cf-7ca8776562ea"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7ab07891-823b-4b09-ba69-9e75345f890c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ab3f01c7-4f39-4840-81f3-c95557d5791c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c2c4e6ce-ad5f-4180-8aa4-0ff687462865"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d9105cee-f29e-4783-bdb0-25d47980fb95"));

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("119a5e97-4947-4c87-9ffd-2d35e343ef53"),
                column: "description",
                value: "Create, update and assign roles.");

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"), "order-operations.received-warehouse-orders.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create new received warehouse orders.", "Create Received Warehouse Order", null },
                    { new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"), "order-operations.supplier-orders.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create new supplier orders.", "Create Supplier Order", null },
                    { new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"), "order-operations.supplier-orders.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View a single supplier order.", "View Supplier Order Detail", null },
                    { new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"), "order-operations.received-warehouse-orders.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View a single received warehouse order.", "View Received Warehouse Order Detail", null },
                    { new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"), "order-operations.received-warehouse-orders.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View the received warehouse order list.", "List Received Warehouse Orders", null },
                    { new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"), "order-operations.supplier-orders.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Update existing supplier orders.", "Update Supplier Order", null },
                    { new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"), "shipment-operations.inter-warehouse-transfers.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View the inter-warehouse transfer list.", "List Inter-Warehouse Transfers", null },
                    { new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"), "order-operations.supplier-orders.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View the supplier order list.", "List Supplier Orders", null },
                    { new Guid("949329f5-0c22-4b84-b45d-8a701a949a3d"), "shipment-operations.inter-warehouse-transfers.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Update existing inter-warehouse transfers.", "Update Inter-Warehouse Transfer", null },
                    { new Guid("a3064a06-c287-4be5-a39b-9bbc9d145839"), "order-operations.received-warehouse-orders.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Update existing received warehouse orders.", "Update Received Warehouse Order", null },
                    { new Guid("e604251d-f4f8-4510-a3c0-a2044b1bcff3"), "shipment-operations.inter-warehouse-transfers.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View a single inter-warehouse transfer.", "View Inter-Warehouse Transfer Detail", null },
                    { new Guid("e9eb2dd0-9289-427f-ae0d-93dfe0fe726f"), "shipment-operations.inter-warehouse-transfers.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create new inter-warehouse transfers.", "Create Inter-Warehouse Transfer", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("949329f5-0c22-4b84-b45d-8a701a949a3d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3064a06-c287-4be5-a39b-9bbc9d145839"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e604251d-f4f8-4510-a3c0-a2044b1bcff3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e9eb2dd0-9289-427f-ae0d-93dfe0fe726f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("949329f5-0c22-4b84-b45d-8a701a949a3d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a3064a06-c287-4be5-a39b-9bbc9d145839"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e604251d-f4f8-4510-a3c0-a2044b1bcff3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e9eb2dd0-9289-427f-ae0d-93dfe0fe726f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("949329f5-0c22-4b84-b45d-8a701a949a3d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a3064a06-c287-4be5-a39b-9bbc9d145839"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e604251d-f4f8-4510-a3c0-a2044b1bcff3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e9eb2dd0-9289-427f-ae0d-93dfe0fe726f"));

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("119a5e97-4947-4c87-9ffd-2d35e343ef53"),
                column: "description",
                value: "Create and update roles.");

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0c01e5d4-f564-4e22-9454-a5594ec4b38c"), "customers.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List customers from Mikro.", "Read Customers", null },
                    { new Guid("4bdc2240-57a0-499d-afb2-3572aa249d53"), "customers.write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update customers in Mikro.", "Write Customers", null },
                    { new Guid("69829857-f745-4280-a0cf-7ca8776562ea"), "warehouses.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List warehouses from Mikro.", "Read Warehouses", null },
                    { new Guid("7ab07891-823b-4b09-ba69-9e75345f890c"), "warehouses.write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update warehouses in Mikro.", "Write Warehouses", null },
                    { new Guid("ab3f01c7-4f39-4840-81f3-c95557d5791c"), "warehouse-stock-rules.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List warehouse stock rule records from Mikro.", "Read Warehouse Stock Rules", null },
                    { new Guid("c2c4e6ce-ad5f-4180-8aa4-0ff687462865"), "products.read", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "List products from Mikro.", "Read Products", null },
                    { new Guid("d9105cee-f29e-4783-bdb0-25d47980fb95"), "warehouse-stock-rules.write", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Create and update warehouse stock rule records in Mikro.", "Write Warehouse Stock Rules", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0c01e5d4-f564-4e22-9454-a5594ec4b38c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4bdc2240-57a0-499d-afb2-3572aa249d53"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("69829857-f745-4280-a0cf-7ca8776562ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7ab07891-823b-4b09-ba69-9e75345f890c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ab3f01c7-4f39-4840-81f3-c95557d5791c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c2c4e6ce-ad5f-4180-8aa4-0ff687462865"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d9105cee-f29e-4783-bdb0-25d47980fb95"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
