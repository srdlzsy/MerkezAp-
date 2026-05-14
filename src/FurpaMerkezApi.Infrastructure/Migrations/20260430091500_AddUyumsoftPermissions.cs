using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260430091500_AddUyumsoftPermissions")]
    public sealed class AddUyumsoftPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "app_permissions" ("id", "code", "created_at_utc", "description", "name", "updated_at_utc")
                VALUES
                    ('a3c1dc56-b44a-7757-7b56-6635e7270bff', 'entegrasyon-islemleri.uyumsoft-e-fatura.list', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEFatura > Listele yetkisi.', 'UyumsoftEFatura Listele', NULL),
                    ('ab37720f-d47a-d30f-351b-0e6e54b1af29', 'entegrasyon-islemleri.uyumsoft-e-fatura.detail', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEFatura > Detay yetkisi.', 'UyumsoftEFatura Detay', NULL),
                    ('59a34fe4-e6d5-68d5-e072-8a7c3c7f39b4', 'entegrasyon-islemleri.uyumsoft-e-fatura.create', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEFatura > Ekle yetkisi.', 'UyumsoftEFatura Ekle', NULL),
                    ('8b81d761-c6bb-7edf-d160-7caf43d8b229', 'entegrasyon-islemleri.uyumsoft-e-fatura.update', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEFatura > Guncelle yetkisi.', 'UyumsoftEFatura Guncelle', NULL),
                    ('74ae9a31-cde4-1ed7-eafb-27ccf28c032c', 'entegrasyon-islemleri.uyumsoft-e-irsaliye.list', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEIrsaliye > Listele yetkisi.', 'UyumsoftEIrsaliye Listele', NULL),
                    ('ecd7625e-ba3c-e58f-b8fa-1d12ee6be2e3', 'entegrasyon-islemleri.uyumsoft-e-irsaliye.detail', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEIrsaliye > Detay yetkisi.', 'UyumsoftEIrsaliye Detay', NULL),
                    ('a53eb81a-5cd2-c2bf-5354-f79bdd399d34', 'entegrasyon-islemleri.uyumsoft-e-irsaliye.create', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEIrsaliye > Ekle yetkisi.', 'UyumsoftEIrsaliye Ekle', NULL),
                    ('9a3712eb-45b5-bab1-5fe3-e6f7300a9df7', 'entegrasyon-islemleri.uyumsoft-e-irsaliye.update', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'EntegrasyonIslemleri > UyumsoftEIrsaliye > Guncelle yetkisi.', 'UyumsoftEIrsaliye Guncelle', NULL);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "app_role_permissions" ("permission_id", "role_id", "assigned_at_utc")
                VALUES
                    ('a3c1dc56-b44a-7757-7b56-6635e7270bff', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('ab37720f-d47a-d30f-351b-0e6e54b1af29', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('59a34fe4-e6d5-68d5-e072-8a7c3c7f39b4', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('8b81d761-c6bb-7edf-d160-7caf43d8b229', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('74ae9a31-cde4-1ed7-eafb-27ccf28c032c', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('ecd7625e-ba3c-e58f-b8fa-1d12ee6be2e3', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('a53eb81a-5cd2-c2bf-5354-f79bdd399d34', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('9a3712eb-45b5-bab1-5fe3-e6f7300a9df7', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "app_role_permissions"
                WHERE "role_id" = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                  AND "permission_id" IN (
                    'a3c1dc56-b44a-7757-7b56-6635e7270bff',
                    'ab37720f-d47a-d30f-351b-0e6e54b1af29',
                    '59a34fe4-e6d5-68d5-e072-8a7c3c7f39b4',
                    '8b81d761-c6bb-7edf-d160-7caf43d8b229',
                    '74ae9a31-cde4-1ed7-eafb-27ccf28c032c',
                    'ecd7625e-ba3c-e58f-b8fa-1d12ee6be2e3',
                    'a53eb81a-5cd2-c2bf-5354-f79bdd399d34',
                    '9a3712eb-45b5-bab1-5fe3-e6f7300a9df7');
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "app_permissions"
                WHERE "id" IN (
                    'a3c1dc56-b44a-7757-7b56-6635e7270bff',
                    'ab37720f-d47a-d30f-351b-0e6e54b1af29',
                    '59a34fe4-e6d5-68d5-e072-8a7c3c7f39b4',
                    '8b81d761-c6bb-7edf-d160-7caf43d8b229',
                    '74ae9a31-cde4-1ed7-eafb-27ccf28c032c',
                    'ecd7625e-ba3c-e58f-b8fa-1d12ee6be2e3',
                    'a53eb81a-5cd2-c2bf-5354-f79bdd399d34',
                    '9a3712eb-45b5-bab1-5fe3-e6f7300a9df7');
                """);
        }
    }
}
