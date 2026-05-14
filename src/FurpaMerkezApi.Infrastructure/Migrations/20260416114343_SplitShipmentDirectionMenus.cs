using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitShipmentDirectionMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO app_permissions (id, code, created_at_utc, description, name, updated_at_utc)
                VALUES
                    ('e2374c43-09c7-5a71-fae8-4820f962c9a8', 'sevk-islemleri.giden-depolar-arasi-sevkler.list', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenDepolarArasiSevkler > Listele yetkisi.', 'GidenDepolarArasiSevkler Listele', NULL),
                    ('50dc419c-f466-3e14-7bf1-220e1afe4a0c', 'sevk-islemleri.giden-depolar-arasi-sevkler.detail', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenDepolarArasiSevkler > Detay yetkisi.', 'GidenDepolarArasiSevkler Detay', NULL),
                    ('fd5c3ee5-3cc6-3ff3-e8b9-c80aae4fb499', 'sevk-islemleri.giden-depolar-arasi-sevkler.create', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenDepolarArasiSevkler > Ekle yetkisi.', 'GidenDepolarArasiSevkler Ekle', NULL),
                    ('cd57b161-835e-f6dd-1993-24bad38b34bc', 'sevk-islemleri.giden-depolar-arasi-sevkler.update', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenDepolarArasiSevkler > Guncelle yetkisi.', 'GidenDepolarArasiSevkler Guncelle', NULL),
                    ('a6337bda-15cd-a2ab-c634-316e99497c08', 'sevk-islemleri.gelen-depolar-arasi-sevkler.list', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenDepolarArasiSevkler > Listele yetkisi.', 'GelenDepolarArasiSevkler Listele', NULL),
                    ('cab66b00-0c16-e18a-2429-c40e6446c39c', 'sevk-islemleri.gelen-depolar-arasi-sevkler.detail', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenDepolarArasiSevkler > Detay yetkisi.', 'GelenDepolarArasiSevkler Detay', NULL),
                    ('f56438fa-89a7-181f-1345-519ec3a64a64', 'sevk-islemleri.gelen-depolar-arasi-sevkler.create', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenDepolarArasiSevkler > Ekle yetkisi.', 'GelenDepolarArasiSevkler Ekle', NULL),
                    ('bb143bfa-d87a-c151-4b43-1e80aab2522a', 'sevk-islemleri.gelen-depolar-arasi-sevkler.update', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenDepolarArasiSevkler > Guncelle yetkisi.', 'GelenDepolarArasiSevkler Guncelle', NULL),
                    ('b9e1ce0f-49bb-91ff-5b24-b160321f7abf', 'sevk-islemleri.giden-firma-sevkleri.list', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenFirmaSevkleri > Listele yetkisi.', 'GidenFirmaSevkleri Listele', NULL),
                    ('3d5b774b-ef3f-183b-17ed-c088c27b869f', 'sevk-islemleri.giden-firma-sevkleri.detail', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenFirmaSevkleri > Detay yetkisi.', 'GidenFirmaSevkleri Detay', NULL),
                    ('035189db-d89f-9432-3a5d-36e5fefe8249', 'sevk-islemleri.giden-firma-sevkleri.create', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenFirmaSevkleri > Ekle yetkisi.', 'GidenFirmaSevkleri Ekle', NULL),
                    ('65319fbe-a3cd-1f72-260c-1ec1e95b7077', 'sevk-islemleri.giden-firma-sevkleri.update', '2026-04-14T00:00:00Z', 'SevkIslemleri > GidenFirmaSevkleri > Guncelle yetkisi.', 'GidenFirmaSevkleri Guncelle', NULL),
                    ('9f23d946-fe23-0d73-3bf1-a26b11795c36', 'sevk-islemleri.gelen-firma-sevkleri.list', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenFirmaSevkleri > Listele yetkisi.', 'GelenFirmaSevkleri Listele', NULL),
                    ('b4f229ba-b0b1-7be5-25a0-89229fbdb9b2', 'sevk-islemleri.gelen-firma-sevkleri.detail', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenFirmaSevkleri > Detay yetkisi.', 'GelenFirmaSevkleri Detay', NULL),
                    ('21769530-5e11-270f-adb7-5cc8de6347a7', 'sevk-islemleri.gelen-firma-sevkleri.create', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenFirmaSevkleri > Ekle yetkisi.', 'GelenFirmaSevkleri Ekle', NULL),
                    ('a2464f1a-66d0-c112-8e91-b1d2ac35edc0', 'sevk-islemleri.gelen-firma-sevkleri.update', '2026-04-14T00:00:00Z', 'SevkIslemleri > GelenFirmaSevkleri > Guncelle yetkisi.', 'GelenFirmaSevkleri Guncelle', NULL)
                ON CONFLICT (code) DO UPDATE SET
                    name = EXCLUDED.name,
                    description = EXCLUDED.description;

                WITH permission_map(old_code, new_code) AS (
                    VALUES
                        ('sevk-islemleri.depolar-arasi-sevkler.list', 'sevk-islemleri.giden-depolar-arasi-sevkler.list'),
                        ('sevk-islemleri.depolar-arasi-sevkler.detail', 'sevk-islemleri.giden-depolar-arasi-sevkler.detail'),
                        ('sevk-islemleri.depolar-arasi-sevkler.create', 'sevk-islemleri.giden-depolar-arasi-sevkler.create'),
                        ('sevk-islemleri.depolar-arasi-sevkler.update', 'sevk-islemleri.giden-depolar-arasi-sevkler.update'),
                        ('sevk-islemleri.depolar-arasi-sevkler.list', 'sevk-islemleri.gelen-depolar-arasi-sevkler.list'),
                        ('sevk-islemleri.depolar-arasi-sevkler.detail', 'sevk-islemleri.gelen-depolar-arasi-sevkler.detail'),
                        ('sevk-islemleri.depolar-arasi-sevkler.create', 'sevk-islemleri.gelen-depolar-arasi-sevkler.create'),
                        ('sevk-islemleri.depolar-arasi-sevkler.update', 'sevk-islemleri.gelen-depolar-arasi-sevkler.update'),
                        ('sevk-islemleri.firma-sevkleri.list', 'sevk-islemleri.giden-firma-sevkleri.list'),
                        ('sevk-islemleri.firma-sevkleri.detail', 'sevk-islemleri.giden-firma-sevkleri.detail'),
                        ('sevk-islemleri.firma-sevkleri.create', 'sevk-islemleri.giden-firma-sevkleri.create'),
                        ('sevk-islemleri.firma-sevkleri.update', 'sevk-islemleri.giden-firma-sevkleri.update'),
                        ('sevk-islemleri.firma-sevkleri.list', 'sevk-islemleri.gelen-firma-sevkleri.list'),
                        ('sevk-islemleri.firma-sevkleri.detail', 'sevk-islemleri.gelen-firma-sevkleri.detail'),
                        ('sevk-islemleri.firma-sevkleri.create', 'sevk-islemleri.gelen-firma-sevkleri.create'),
                        ('sevk-islemleri.firma-sevkleri.update', 'sevk-islemleri.gelen-firma-sevkleri.update')
                )
                INSERT INTO app_role_permissions (role_id, permission_id, assigned_at_utc)
                SELECT old_role_permission.role_id, new_permission.id, old_role_permission.assigned_at_utc
                FROM app_role_permissions old_role_permission
                INNER JOIN app_permissions old_permission ON old_permission.id = old_role_permission.permission_id
                INNER JOIN permission_map ON permission_map.old_code = old_permission.code
                INNER JOIN app_permissions new_permission ON new_permission.code = permission_map.new_code
                ON CONFLICT (role_id, permission_id) DO NOTHING;

                INSERT INTO app_role_permissions (role_id, permission_id, assigned_at_utc)
                SELECT '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', id, '2026-04-14T00:00:00Z'
                FROM app_permissions
                WHERE code IN (
                    'sevk-islemleri.giden-depolar-arasi-sevkler.list',
                    'sevk-islemleri.giden-depolar-arasi-sevkler.detail',
                    'sevk-islemleri.giden-depolar-arasi-sevkler.create',
                    'sevk-islemleri.giden-depolar-arasi-sevkler.update',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.list',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.detail',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.create',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.update',
                    'sevk-islemleri.giden-firma-sevkleri.list',
                    'sevk-islemleri.giden-firma-sevkleri.detail',
                    'sevk-islemleri.giden-firma-sevkleri.create',
                    'sevk-islemleri.giden-firma-sevkleri.update',
                    'sevk-islemleri.gelen-firma-sevkleri.list',
                    'sevk-islemleri.gelen-firma-sevkleri.detail',
                    'sevk-islemleri.gelen-firma-sevkleri.create',
                    'sevk-islemleri.gelen-firma-sevkleri.update')
                ON CONFLICT (role_id, permission_id) DO NOTHING;

                DELETE FROM app_role_permissions
                WHERE permission_id IN (
                    SELECT id
                    FROM app_permissions
                    WHERE code IN (
                        'sevk-islemleri.depolar-arasi-sevkler.list',
                        'sevk-islemleri.depolar-arasi-sevkler.detail',
                        'sevk-islemleri.depolar-arasi-sevkler.create',
                        'sevk-islemleri.depolar-arasi-sevkler.update',
                        'sevk-islemleri.firma-sevkleri.list',
                        'sevk-islemleri.firma-sevkleri.detail',
                        'sevk-islemleri.firma-sevkleri.create',
                        'sevk-islemleri.firma-sevkleri.update'));

                DELETE FROM app_permissions
                WHERE code IN (
                    'sevk-islemleri.depolar-arasi-sevkler.list',
                    'sevk-islemleri.depolar-arasi-sevkler.detail',
                    'sevk-islemleri.depolar-arasi-sevkler.create',
                    'sevk-islemleri.depolar-arasi-sevkler.update',
                    'sevk-islemleri.firma-sevkleri.list',
                    'sevk-islemleri.firma-sevkleri.detail',
                    'sevk-islemleri.firma-sevkleri.create',
                    'sevk-islemleri.firma-sevkleri.update');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO app_permissions (id, code, created_at_utc, description, name, updated_at_utc)
                VALUES
                    ('90695dec-7135-8331-8a53-cc0b108a364c', 'sevk-islemleri.depolar-arasi-sevkler.list', '2026-04-14T00:00:00Z', 'SevkIslemleri > DepolarArasiSevkler > Listele yetkisi.', 'DepolarArasiSevkler Listele', NULL),
                    ('524b256e-b696-ccd1-6e28-481adb431a5b', 'sevk-islemleri.depolar-arasi-sevkler.detail', '2026-04-14T00:00:00Z', 'SevkIslemleri > DepolarArasiSevkler > Detay yetkisi.', 'DepolarArasiSevkler Detay', NULL),
                    ('1e557c20-df49-dac6-dc69-b0aa103560d4', 'sevk-islemleri.depolar-arasi-sevkler.create', '2026-04-14T00:00:00Z', 'SevkIslemleri > DepolarArasiSevkler > Ekle yetkisi.', 'DepolarArasiSevkler Ekle', NULL),
                    ('b6abd05b-a3ea-7282-7655-b28f1f0a26b1', 'sevk-islemleri.depolar-arasi-sevkler.update', '2026-04-14T00:00:00Z', 'SevkIslemleri > DepolarArasiSevkler > Guncelle yetkisi.', 'DepolarArasiSevkler Guncelle', NULL),
                    ('2cb773b0-fb98-357e-103a-d4f1c6b62f78', 'sevk-islemleri.firma-sevkleri.list', '2026-04-14T00:00:00Z', 'SevkIslemleri > FirmaSevkleri > Listele yetkisi.', 'FirmaSevkleri Listele', NULL),
                    ('90a43d70-aa52-a61c-45aa-a07defb35d1a', 'sevk-islemleri.firma-sevkleri.detail', '2026-04-14T00:00:00Z', 'SevkIslemleri > FirmaSevkleri > Detay yetkisi.', 'FirmaSevkleri Detay', NULL),
                    ('218c674f-77bb-027c-9e68-dfbf9e2f6cf0', 'sevk-islemleri.firma-sevkleri.create', '2026-04-14T00:00:00Z', 'SevkIslemleri > FirmaSevkleri > Ekle yetkisi.', 'FirmaSevkleri Ekle', NULL),
                    ('7933b9ad-9225-22ad-6003-f91158f0cd95', 'sevk-islemleri.firma-sevkleri.update', '2026-04-14T00:00:00Z', 'SevkIslemleri > FirmaSevkleri > Guncelle yetkisi.', 'FirmaSevkleri Guncelle', NULL)
                ON CONFLICT (code) DO UPDATE SET
                    name = EXCLUDED.name,
                    description = EXCLUDED.description;

                WITH permission_map(new_code, old_code) AS (
                    VALUES
                        ('sevk-islemleri.giden-depolar-arasi-sevkler.list', 'sevk-islemleri.depolar-arasi-sevkler.list'),
                        ('sevk-islemleri.giden-depolar-arasi-sevkler.detail', 'sevk-islemleri.depolar-arasi-sevkler.detail'),
                        ('sevk-islemleri.giden-depolar-arasi-sevkler.create', 'sevk-islemleri.depolar-arasi-sevkler.create'),
                        ('sevk-islemleri.giden-depolar-arasi-sevkler.update', 'sevk-islemleri.depolar-arasi-sevkler.update'),
                        ('sevk-islemleri.gelen-depolar-arasi-sevkler.list', 'sevk-islemleri.depolar-arasi-sevkler.list'),
                        ('sevk-islemleri.gelen-depolar-arasi-sevkler.detail', 'sevk-islemleri.depolar-arasi-sevkler.detail'),
                        ('sevk-islemleri.gelen-depolar-arasi-sevkler.create', 'sevk-islemleri.depolar-arasi-sevkler.create'),
                        ('sevk-islemleri.gelen-depolar-arasi-sevkler.update', 'sevk-islemleri.depolar-arasi-sevkler.update'),
                        ('sevk-islemleri.giden-firma-sevkleri.list', 'sevk-islemleri.firma-sevkleri.list'),
                        ('sevk-islemleri.giden-firma-sevkleri.detail', 'sevk-islemleri.firma-sevkleri.detail'),
                        ('sevk-islemleri.giden-firma-sevkleri.create', 'sevk-islemleri.firma-sevkleri.create'),
                        ('sevk-islemleri.giden-firma-sevkleri.update', 'sevk-islemleri.firma-sevkleri.update'),
                        ('sevk-islemleri.gelen-firma-sevkleri.list', 'sevk-islemleri.firma-sevkleri.list'),
                        ('sevk-islemleri.gelen-firma-sevkleri.detail', 'sevk-islemleri.firma-sevkleri.detail'),
                        ('sevk-islemleri.gelen-firma-sevkleri.create', 'sevk-islemleri.firma-sevkleri.create'),
                        ('sevk-islemleri.gelen-firma-sevkleri.update', 'sevk-islemleri.firma-sevkleri.update')
                )
                INSERT INTO app_role_permissions (role_id, permission_id, assigned_at_utc)
                SELECT new_role_permission.role_id, old_permission.id, new_role_permission.assigned_at_utc
                FROM app_role_permissions new_role_permission
                INNER JOIN app_permissions new_permission ON new_permission.id = new_role_permission.permission_id
                INNER JOIN permission_map ON permission_map.new_code = new_permission.code
                INNER JOIN app_permissions old_permission ON old_permission.code = permission_map.old_code
                ON CONFLICT (role_id, permission_id) DO NOTHING;

                DELETE FROM app_role_permissions
                WHERE permission_id IN (
                    SELECT id
                    FROM app_permissions
                    WHERE code IN (
                        'sevk-islemleri.giden-depolar-arasi-sevkler.list',
                        'sevk-islemleri.giden-depolar-arasi-sevkler.detail',
                        'sevk-islemleri.giden-depolar-arasi-sevkler.create',
                        'sevk-islemleri.giden-depolar-arasi-sevkler.update',
                        'sevk-islemleri.gelen-depolar-arasi-sevkler.list',
                        'sevk-islemleri.gelen-depolar-arasi-sevkler.detail',
                        'sevk-islemleri.gelen-depolar-arasi-sevkler.create',
                        'sevk-islemleri.gelen-depolar-arasi-sevkler.update',
                        'sevk-islemleri.giden-firma-sevkleri.list',
                        'sevk-islemleri.giden-firma-sevkleri.detail',
                        'sevk-islemleri.giden-firma-sevkleri.create',
                        'sevk-islemleri.giden-firma-sevkleri.update',
                        'sevk-islemleri.gelen-firma-sevkleri.list',
                        'sevk-islemleri.gelen-firma-sevkleri.detail',
                        'sevk-islemleri.gelen-firma-sevkleri.create',
                        'sevk-islemleri.gelen-firma-sevkleri.update'));

                DELETE FROM app_permissions
                WHERE code IN (
                    'sevk-islemleri.giden-depolar-arasi-sevkler.list',
                    'sevk-islemleri.giden-depolar-arasi-sevkler.detail',
                    'sevk-islemleri.giden-depolar-arasi-sevkler.create',
                    'sevk-islemleri.giden-depolar-arasi-sevkler.update',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.list',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.detail',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.create',
                    'sevk-islemleri.gelen-depolar-arasi-sevkler.update',
                    'sevk-islemleri.giden-firma-sevkleri.list',
                    'sevk-islemleri.giden-firma-sevkleri.detail',
                    'sevk-islemleri.giden-firma-sevkleri.create',
                    'sevk-islemleri.giden-firma-sevkleri.update',
                    'sevk-islemleri.gelen-firma-sevkleri.list',
                    'sevk-islemleri.gelen-firma-sevkleri.detail',
                    'sevk-islemleri.gelen-firma-sevkleri.create',
                    'sevk-islemleri.gelen-firma-sevkleri.update');
                """);
        }
    }
}
