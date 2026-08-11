using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    public partial class AddTurkeyTimeAuditColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [mikro_api_write_audits]
                ADD [created_at_tr] AS DATEADD(HOUR, 3, [created_at_utc]);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [mikro_api_write_audits]
                ADD [completed_at_tr] AS DATEADD(HOUR, 3, [completed_at_utc]);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [mikro_api_write_audits]
                ADD [recovered_at_tr] AS DATEADD(HOUR, 3, [recovered_at_utc]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [mikro_api_write_audits] DROP COLUMN [recovered_at_tr];
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [mikro_api_write_audits] DROP COLUMN [completed_at_tr];
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [mikro_api_write_audits] DROP COLUMN [created_at_tr];
                """);
        }
    }
}
