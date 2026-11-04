using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendyolGoBatchFollowUpTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trendyol_go_batch_follow_up_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    batch_request_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_no = table.Column<int>(type: "int", nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    attempt_count = table.Column<int>(type: "int", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    result_summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trendyol_go_batch_follow_up_tasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_batch_follow_up_tasks_batch_request_id",
                table: "trendyol_go_batch_follow_up_tasks",
                column: "batch_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trendyol_go_batch_follow_up_tasks_status_next_attempt_at_utc",
                table: "trendyol_go_batch_follow_up_tasks",
                columns: new[] { "status", "next_attempt_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trendyol_go_batch_follow_up_tasks");
        }
    }
}
