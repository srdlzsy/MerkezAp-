using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdministratorUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_users",
                columns: new[] { "id", "created_at_utc", "email", "first_name", "is_active", "last_name", "normalized_email", "normalized_username", "password_hash", "updated_at_utc", "username", "warehouse_name", "warehouse_no" },
                values: new object[] { new Guid("af8e4919-5d2e-4c3a-981f-bafe1c1988ff"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "admin@furpamerkez.local", "System", true, "Administrator", "ADMIN@FURPAMERKEZ.LOCAL", "ADMIN", "PBKDF2$100000$AAECAwQFBgcICQoLDA0ODw==$FdMPxR1Ml1GQMslxpUzbpxpAI5NoO/6gzn9FA8Rqaio=", null, "admin", "MERKEZ", "0" });

            migrationBuilder.InsertData(
                table: "app_user_roles",
                columns: new[] { "role_id", "user_id", "assigned_at_utc" },
                values: new object[] { new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new Guid("af8e4919-5d2e-4c3a-981f-bafe1c1988ff"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_user_roles",
                keyColumns: new[] { "role_id", "user_id" },
                keyValues: new object[] { new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new Guid("af8e4919-5d2e-4c3a-981f-bafe1c1988ff") });

            migrationBuilder.DeleteData(
                table: "app_users",
                keyColumn: "id",
                keyValue: new Guid("af8e4919-5d2e-4c3a-981f-bafe1c1988ff"));
        }
    }
}
