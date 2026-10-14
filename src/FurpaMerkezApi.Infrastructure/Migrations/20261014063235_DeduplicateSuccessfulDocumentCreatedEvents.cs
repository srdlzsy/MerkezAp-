using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeduplicateSuccessfulDocumentCreatedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ;WITH duplicate_events AS
                (
                    SELECT
                        [id],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY [document_flow_id]
                            ORDER BY [occurred_at_utc], [id]
                        ) AS [row_number]
                    FROM [document_flow_events]
                    WHERE [step] = N'DocumentCreated'
                      AND [status] = N'Succeeded'
                )
                DELETE FROM duplicate_events
                WHERE [row_number] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_document_flow_events_successful_creation",
                table: "document_flow_events",
                column: "document_flow_id",
                unique: true,
                filter: "[step] = N'DocumentCreated' AND [status] = N'Succeeded'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_document_flow_events_successful_creation",
                table: "document_flow_events");
        }
    }
}
