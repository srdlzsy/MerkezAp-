using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TopLevelModulePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValue: new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"));

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
                keyValue: new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "module.return-operations", "Access to the return operations module.", "Return Operations" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "module.cash-operations", "Access to the cash operations module.", "Cash Operations" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "module.goods-receipt-operations", "Access to the goods receipt operations module.", "Goods Receipt Operations" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "module.order-operations", "Access to the order operations module.", "Order Operations" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "module.shipment-operations", "Access to the shipment operations module.", "Shipment Operations" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "module.user-operations", "Access to the user operations module.", "User Operations" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "order-operations.received-warehouse-orders.create", "Create new received warehouse orders.", "Create Received Warehouse Order" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "order-operations.supplier-orders.create", "Create new supplier orders.", "Create Supplier Order" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "order-operations.received-warehouse-orders.detail", "View a single received warehouse order.", "View Received Warehouse Order Detail" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "order-operations.received-warehouse-orders.list", "View the received warehouse order list.", "List Received Warehouse Orders" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "shipment-operations.inter-warehouse-transfers.list", "View the inter-warehouse transfer list.", "List Inter-Warehouse Transfers" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "order-operations.supplier-orders.list", "View the supplier order list.", "List Supplier Orders" });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"), "order-operations.supplier-orders.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "View a single supplier order.", "View Supplier Order Detail", null },
                    { new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"), "order-operations.supplier-orders.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Update existing supplier orders.", "Update Supplier Order", null },
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
                    { new Guid("318711ff-f0ed-45e2-b55e-ee93f8f1fe36"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("80d80a6c-f985-42d9-8f24-3d62f010f4af"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("949329f5-0c22-4b84-b45d-8a701a949a3d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3064a06-c287-4be5-a39b-9bbc9d145839"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e604251d-f4f8-4510-a3c0-a2044b1bcff3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e9eb2dd0-9289-427f-ae0d-93dfe0fe726f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
