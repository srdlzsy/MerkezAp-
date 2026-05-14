using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260505154500_AddInvoiceViewingUpdatePermission")]
    public partial class AddInvoiceViewingUpdatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO app_permissions (id, code, created_at_utc, description, name, updated_at_utc)
                VALUES (
                    '2507f0b4-0241-e3c1-9882-55e7bb6869e6'::uuid,
                    'fatura-islemleri.fatura-goruntuleme.update',
                    TIMESTAMPTZ '2026-04-14 00:00:00+00',
                    'FaturaIslemleri > FaturaGoruntuleme > Guncelle yetkisi.',
                    'FaturaGoruntuleme Guncelle',
                    NULL
                )
                ON CONFLICT (id) DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO app_role_permissions (permission_id, role_id, assigned_at_utc)
                VALUES (
                    '2507f0b4-0241-e3c1-9882-55e7bb6869e6'::uuid,
                    '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'::uuid,
                    TIMESTAMPTZ '2026-04-14 00:00:00+00'
                )
                ON CONFLICT (permission_id, role_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM app_role_permissions
                WHERE permission_id = '2507f0b4-0241-e3c1-9882-55e7bb6869e6'::uuid
                  AND role_id = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'::uuid;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM app_permissions
                WHERE id = '2507f0b4-0241-e3c1-9882-55e7bb6869e6'::uuid;
                """);
        }
    }
}
