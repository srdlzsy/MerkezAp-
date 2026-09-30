using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableEDespatchSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "edespatch_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_key = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    document_no = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    uuid = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    attempt_count = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edespatch_submissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_edespatch_submissions_document_key",
                table: "edespatch_submissions",
                column: "document_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_edespatch_submissions_document_no",
                table: "edespatch_submissions",
                column: "document_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_edespatch_submissions_status_next_attempt_at_utc",
                table: "edespatch_submissions",
                columns: new[] { "status", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_edespatch_submissions_uuid",
                table: "edespatch_submissions",
                column: "uuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "edespatch_submissions");
        }
    }
}
