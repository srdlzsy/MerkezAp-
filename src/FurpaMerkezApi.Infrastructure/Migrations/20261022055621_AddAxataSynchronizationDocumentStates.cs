using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAxataSynchronizationDocumentStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "axata_synchronization_document_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    task_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    document_key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    failure_count = table.Column<int>(type: "int", nullable: false),
                    last_error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    first_failed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_failed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axata_synchronization_document_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_axata_sync_document_state_next_attempt",
                table: "axata_synchronization_document_states",
                columns: new[] { "task_code", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_axata_sync_document_state_task_document",
                table: "axata_synchronization_document_states",
                columns: new[] { "task_code", "document_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "axata_synchronization_document_states");
        }
    }
}
