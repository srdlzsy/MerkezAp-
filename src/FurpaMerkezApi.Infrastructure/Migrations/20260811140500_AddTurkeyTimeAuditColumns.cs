using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260811140500_AddTurkeyTimeAuditColumns")]
    public partial class AddTurkeyTimeAuditColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.mikro_api_write_audits', 'created_at_tr') IS NULL
                BEGIN
                ALTER TABLE [mikro_api_write_audits]
                ADD [created_at_tr] AS DATEADD(HOUR, 3, [created_at_utc]);
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.mikro_api_write_audits', 'completed_at_tr') IS NULL
                BEGIN
                ALTER TABLE [mikro_api_write_audits]
                ADD [completed_at_tr] AS DATEADD(HOUR, 3, [completed_at_utc]);
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.mikro_api_write_audits', 'recovered_at_tr') IS NULL
                BEGIN
                ALTER TABLE [mikro_api_write_audits]
                ADD [recovered_at_tr] AS DATEADD(HOUR, 3, [recovered_at_utc]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.mikro_api_write_audits', 'recovered_at_tr') IS NOT NULL
                BEGIN
                ALTER TABLE [mikro_api_write_audits] DROP COLUMN [recovered_at_tr];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.mikro_api_write_audits', 'completed_at_tr') IS NOT NULL
                BEGIN
                ALTER TABLE [mikro_api_write_audits] DROP COLUMN [completed_at_tr];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.mikro_api_write_audits', 'created_at_tr') IS NOT NULL
                BEGIN
                ALTER TABLE [mikro_api_write_audits] DROP COLUMN [created_at_tr];
                END
                """);
        }
    }
}
