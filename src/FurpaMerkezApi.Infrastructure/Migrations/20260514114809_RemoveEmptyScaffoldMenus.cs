using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmptyScaffoldMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0e7ee02d-a676-0224-dbc6-a2f529d93a2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("143af022-4d7e-66eb-c558-624aad50fb7f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1c957882-c345-0563-b714-044a014b3685"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("22976e1d-77c4-5489-fe6d-c5db0aac96ad"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("22cb488c-9c07-29dc-e0a0-726025b88fc1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("298fe457-a222-8a88-e870-2a812e93c8c6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2e05f41f-3e70-647d-b2c4-0de5b71b45fb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("30b314b2-71d4-0960-0861-b6418dbd8f79"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("35961219-4e49-744c-f302-d4e1b627c81d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("381aca3b-27ca-8310-dc62-960634672984"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("54ec0134-ed15-2ebc-b9c8-3208bd3ad0d9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5c60c66b-7679-bc4d-af47-11208ed6bb52"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("66c64021-d1e2-6cce-9ee6-77806ffb8e27"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6867eb33-82ba-f745-b9fc-b950c650b04b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d476adc9-a850-a787-7525-a68429fbda72"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("da4d8624-84a7-c1cc-eb06-baef90f94422"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0e7ee02d-a676-0224-dbc6-a2f529d93a2e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("143af022-4d7e-66eb-c558-624aad50fb7f"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1c957882-c345-0563-b714-044a014b3685"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("22976e1d-77c4-5489-fe6d-c5db0aac96ad"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("22cb488c-9c07-29dc-e0a0-726025b88fc1"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("298fe457-a222-8a88-e870-2a812e93c8c6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2e05f41f-3e70-647d-b2c4-0de5b71b45fb"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("30b314b2-71d4-0960-0861-b6418dbd8f79"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("35961219-4e49-744c-f302-d4e1b627c81d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("381aca3b-27ca-8310-dc62-960634672984"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("54ec0134-ed15-2ebc-b9c8-3208bd3ad0d9"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5c60c66b-7679-bc4d-af47-11208ed6bb52"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("66c64021-d1e2-6cce-9ee6-77806ffb8e27"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6867eb33-82ba-f745-b9fc-b950c650b04b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d476adc9-a850-a787-7525-a68429fbda72"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("da4d8624-84a7-c1cc-eb06-baef90f94422"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0e7ee02d-a676-0224-dbc6-a2f529d93a2e"), "mal-kabul-islemleri.irsaliye-kabulleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Ekle yetkisi.", "IrsaliyeKabulleri Ekle", null },
                    { new Guid("143af022-4d7e-66eb-c558-624aad50fb7f"), "iade-islemleri.tedarikci-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Guncelle yetkisi.", "TedarikciIadeleri Guncelle", null },
                    { new Guid("1c957882-c345-0563-b714-044a014b3685"), "sevk-islemleri.sevk-planlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Detay yetkisi.", "SevkPlanlari Detay", null },
                    { new Guid("22976e1d-77c4-5489-fe6d-c5db0aac96ad"), "kasa-islemleri.kasa-hareketleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Ekle yetkisi.", "KasaHareketleri Ekle", null },
                    { new Guid("22cb488c-9c07-29dc-e0a0-726025b88fc1"), "kasa-islemleri.kasa-hareketleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Listele yetkisi.", "KasaHareketleri Listele", null },
                    { new Guid("298fe457-a222-8a88-e870-2a812e93c8c6"), "sevk-islemleri.sevk-planlari.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Guncelle yetkisi.", "SevkPlanlari Guncelle", null },
                    { new Guid("2e05f41f-3e70-647d-b2c4-0de5b71b45fb"), "sevk-islemleri.sevk-planlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Listele yetkisi.", "SevkPlanlari Listele", null },
                    { new Guid("30b314b2-71d4-0960-0861-b6418dbd8f79"), "mal-kabul-islemleri.irsaliye-kabulleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Guncelle yetkisi.", "IrsaliyeKabulleri Guncelle", null },
                    { new Guid("35961219-4e49-744c-f302-d4e1b627c81d"), "iade-islemleri.tedarikci-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Ekle yetkisi.", "TedarikciIadeleri Ekle", null },
                    { new Guid("381aca3b-27ca-8310-dc62-960634672984"), "iade-islemleri.musteri-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Listele yetkisi.", "MusteriIadeleri Listele", null },
                    { new Guid("54ec0134-ed15-2ebc-b9c8-3208bd3ad0d9"), "kasa-islemleri.kasa-hareketleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Detay yetkisi.", "KasaHareketleri Detay", null },
                    { new Guid("5c60c66b-7679-bc4d-af47-11208ed6bb52"), "kasa-islemleri.kasa-hareketleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Guncelle yetkisi.", "KasaHareketleri Guncelle", null },
                    { new Guid("66c64021-d1e2-6cce-9ee6-77806ffb8e27"), "iade-islemleri.musteri-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Guncelle yetkisi.", "MusteriIadeleri Guncelle", null },
                    { new Guid("6867eb33-82ba-f745-b9fc-b950c650b04b"), "iade-islemleri.musteri-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Ekle yetkisi.", "MusteriIadeleri Ekle", null },
                    { new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"), "iade-islemleri.tedarikci-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Listele yetkisi.", "TedarikciIadeleri Listele", null },
                    { new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"), "sevk-islemleri.sevk-planlari.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Ekle yetkisi.", "SevkPlanlari Ekle", null },
                    { new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"), "iade-islemleri.tedarikci-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Detay yetkisi.", "TedarikciIadeleri Detay", null },
                    { new Guid("d476adc9-a850-a787-7525-a68429fbda72"), "iade-islemleri.musteri-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Detay yetkisi.", "MusteriIadeleri Detay", null },
                    { new Guid("da4d8624-84a7-c1cc-eb06-baef90f94422"), "mal-kabul-islemleri.irsaliye-kabulleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Listele yetkisi.", "IrsaliyeKabulleri Listele", null },
                    { new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"), "mal-kabul-islemleri.irsaliye-kabulleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Detay yetkisi.", "IrsaliyeKabulleri Detay", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0e7ee02d-a676-0224-dbc6-a2f529d93a2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("143af022-4d7e-66eb-c558-624aad50fb7f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("1c957882-c345-0563-b714-044a014b3685"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22976e1d-77c4-5489-fe6d-c5db0aac96ad"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22cb488c-9c07-29dc-e0a0-726025b88fc1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("298fe457-a222-8a88-e870-2a812e93c8c6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2e05f41f-3e70-647d-b2c4-0de5b71b45fb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30b314b2-71d4-0960-0861-b6418dbd8f79"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("35961219-4e49-744c-f302-d4e1b627c81d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("381aca3b-27ca-8310-dc62-960634672984"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("54ec0134-ed15-2ebc-b9c8-3208bd3ad0d9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5c60c66b-7679-bc4d-af47-11208ed6bb52"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("66c64021-d1e2-6cce-9ee6-77806ffb8e27"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6867eb33-82ba-f745-b9fc-b950c650b04b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d476adc9-a850-a787-7525-a68429fbda72"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("da4d8624-84a7-c1cc-eb06-baef90f94422"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
