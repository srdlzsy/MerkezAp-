using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260507170500_AddPosMuhasebeAktarimiPermissions")]
    public sealed class AddPosMuhasebeAktarimiPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "app_permissions" ("id", "code", "created_at_utc", "description", "name", "updated_at_utc")
                VALUES
                    ('1c35eb14-d2be-40ef-317d-98de311e312f', 'entegrasyon-islemleri.pos-muhasebe-aktarimi.list', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > PosMuhasebeAktarimi > Listele yetkisi.', 'PosMuhasebeAktarimi Listele', NULL),
                    ('25eae8fe-dea3-1b83-0fd7-9e8e3feceacf', 'entegrasyon-islemleri.pos-muhasebe-aktarimi.detail', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > PosMuhasebeAktarimi > Detay yetkisi.', 'PosMuhasebeAktarimi Detay', NULL),
                    ('18f6cc98-7380-9a0a-c904-654f5b97b31d', 'entegrasyon-islemleri.pos-muhasebe-aktarimi.create', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > PosMuhasebeAktarimi > Ekle yetkisi.', 'PosMuhasebeAktarimi Ekle', NULL),
                    ('f9cc06e1-4c12-98e9-170a-7ce8ded04660', 'entegrasyon-islemleri.pos-muhasebe-aktarimi.update', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > PosMuhasebeAktarimi > Guncelle yetkisi.', 'PosMuhasebeAktarimi Guncelle', NULL)
                ON CONFLICT ("id") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "app_role_permissions" ("permission_id", "role_id", "assigned_at_utc")
                VALUES
                    ('1c35eb14-d2be-40ef-317d-98de311e312f', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('25eae8fe-dea3-1b83-0fd7-9e8e3feceacf', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('18f6cc98-7380-9a0a-c904-654f5b97b31d', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('f9cc06e1-4c12-98e9-170a-7ce8ded04660', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00')
                ON CONFLICT ("permission_id", "role_id") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "app_role_permissions"
                WHERE "role_id" = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                  AND "permission_id" IN (
                    '1c35eb14-d2be-40ef-317d-98de311e312f',
                    '25eae8fe-dea3-1b83-0fd7-9e8e3feceacf',
                    '18f6cc98-7380-9a0a-c904-654f5b97b31d',
                    'f9cc06e1-4c12-98e9-170a-7ce8ded04660');
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "app_permissions"
                WHERE "id" IN (
                    '1c35eb14-d2be-40ef-317d-98de311e312f',
                    '25eae8fe-dea3-1b83-0fd7-9e8e3feceacf',
                    '18f6cc98-7380-9a0a-c904-654f5b97b31d',
                    'f9cc06e1-4c12-98e9-170a-7ce8ded04660');
                """);
        }
    }
}
