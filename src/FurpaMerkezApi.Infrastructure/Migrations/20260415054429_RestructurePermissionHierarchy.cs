using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructurePermissionHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"));

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("119a5e97-4947-4c87-9ffd-2d35e343ef53"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "kullanici-islemleri.roller.manage", "KullaniciIslemleri > Roller > Yonet yetkisi.", "Roller Yonet" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("79925722-5c18-4db4-9c7d-c44d6f6fd779"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "kullanici-islemleri.yetkiler.manage", "KullaniciIslemleri > Yetkiler > Yonet yetkisi.", "Yetkiler Yonet" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fdf63a66-e9b4-4ca5-8700-2a6a34231c01"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "kullanici-islemleri.kullanicilar.manage", "KullaniciIslemleri > Kullanicilar > Yonet yetkisi.", "Kullanicilar Yonet" });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0108f924-7db3-7ca6-c2ea-80ac5da8d9a4"), "kasa-islemleri.kasa-sayimlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaSayimlari > Detay yetkisi.", "KasaSayimlari Detay", null },
                    { new Guid("03f3bca3-0670-619a-d6e1-9da9cf4a759c"), "siparis-islemleri.verilen-depo-siparisleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenDepoSiparisleri > Guncelle yetkisi.", "VerilenDepoSiparisleri Guncelle", null },
                    { new Guid("0e7ee02d-a676-0224-dbc6-a2f529d93a2e"), "mal-kabul-islemleri.irsaliye-kabulleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Ekle yetkisi.", "IrsaliyeKabulleri Ekle", null },
                    { new Guid("143af022-4d7e-66eb-c558-624aad50fb7f"), "iade-islemleri.tedarikci-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Guncelle yetkisi.", "TedarikciIadeleri Guncelle", null },
                    { new Guid("1c957882-c345-0563-b714-044a014b3685"), "sevk-islemleri.sevk-planlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Detay yetkisi.", "SevkPlanlari Detay", null },
                    { new Guid("1e557c20-df49-dac6-dc69-b0aa103560d4"), "sevk-islemleri.depolar-arasi-sevkler.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > DepolarArasiSevkler > Ekle yetkisi.", "DepolarArasiSevkler Ekle", null },
                    { new Guid("1fc04da2-9486-4a6c-04cb-d4022706d928"), "siparis-islemleri.verilen-firma-siparisleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenFirmaSiparisleri > Listele yetkisi.", "VerilenFirmaSiparisleri Listele", null },
                    { new Guid("22976e1d-77c4-5489-fe6d-c5db0aac96ad"), "kasa-islemleri.kasa-hareketleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Ekle yetkisi.", "KasaHareketleri Ekle", null },
                    { new Guid("22cb488c-9c07-29dc-e0a0-726025b88fc1"), "kasa-islemleri.kasa-hareketleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Listele yetkisi.", "KasaHareketleri Listele", null },
                    { new Guid("26dba67f-ab23-495f-e360-94933649a26a"), "siparis-islemleri.alinan-firma-siparisleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanFirmaSiparisleri > Ekle yetkisi.", "AlinanFirmaSiparisleri Ekle", null },
                    { new Guid("298fe457-a222-8a88-e870-2a812e93c8c6"), "sevk-islemleri.sevk-planlari.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Guncelle yetkisi.", "SevkPlanlari Guncelle", null },
                    { new Guid("2e05f41f-3e70-647d-b2c4-0de5b71b45fb"), "sevk-islemleri.sevk-planlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Listele yetkisi.", "SevkPlanlari Listele", null },
                    { new Guid("2fd4f83b-41dd-9fbb-d1d2-e40bdaa9f041"), "kasa-islemleri.kasa-sayimlari.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaSayimlari > Ekle yetkisi.", "KasaSayimlari Ekle", null },
                    { new Guid("30b314b2-71d4-0960-0861-b6418dbd8f79"), "mal-kabul-islemleri.irsaliye-kabulleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Guncelle yetkisi.", "IrsaliyeKabulleri Guncelle", null },
                    { new Guid("35961219-4e49-744c-f302-d4e1b627c81d"), "iade-islemleri.tedarikci-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Ekle yetkisi.", "TedarikciIadeleri Ekle", null },
                    { new Guid("381aca3b-27ca-8310-dc62-960634672984"), "iade-islemleri.musteri-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Listele yetkisi.", "MusteriIadeleri Listele", null },
                    { new Guid("42719ec3-0fe1-e97d-825d-5c9fe42594b4"), "siparis-islemleri.verilen-depo-siparisleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenDepoSiparisleri > Listele yetkisi.", "VerilenDepoSiparisleri Listele", null },
                    { new Guid("524b256e-b696-ccd1-6e28-481adb431a5b"), "sevk-islemleri.depolar-arasi-sevkler.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > DepolarArasiSevkler > Detay yetkisi.", "DepolarArasiSevkler Detay", null },
                    { new Guid("54ec0134-ed15-2ebc-b9c8-3208bd3ad0d9"), "kasa-islemleri.kasa-hareketleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Detay yetkisi.", "KasaHareketleri Detay", null },
                    { new Guid("5c60c66b-7679-bc4d-af47-11208ed6bb52"), "kasa-islemleri.kasa-hareketleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaHareketleri > Guncelle yetkisi.", "KasaHareketleri Guncelle", null },
                    { new Guid("608b0c7e-a2da-5e4a-6f2b-00736ebdc5f7"), "siparis-islemleri.alinan-firma-siparisleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanFirmaSiparisleri > Listele yetkisi.", "AlinanFirmaSiparisleri Listele", null },
                    { new Guid("66c64021-d1e2-6cce-9ee6-77806ffb8e27"), "iade-islemleri.musteri-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Guncelle yetkisi.", "MusteriIadeleri Guncelle", null },
                    { new Guid("6867eb33-82ba-f745-b9fc-b950c650b04b"), "iade-islemleri.musteri-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Ekle yetkisi.", "MusteriIadeleri Ekle", null },
                    { new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"), "mal-kabul-islemleri.mal-kabuller.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Ekle yetkisi.", "MalKabuller Ekle", null },
                    { new Guid("7195883f-f16d-baea-fea6-189050aa0b69"), "siparis-islemleri.alinan-depo-siparisleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanDepoSiparisleri > Ekle yetkisi.", "AlinanDepoSiparisleri Ekle", null },
                    { new Guid("7aec07c7-50e5-84f3-9787-c23842130105"), "siparis-islemleri.verilen-firma-siparisleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenFirmaSiparisleri > Detay yetkisi.", "VerilenFirmaSiparisleri Detay", null },
                    { new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"), "iade-islemleri.tedarikci-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Listele yetkisi.", "TedarikciIadeleri Listele", null },
                    { new Guid("90695dec-7135-8331-8a53-cc0b108a364c"), "sevk-islemleri.depolar-arasi-sevkler.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > DepolarArasiSevkler > Listele yetkisi.", "DepolarArasiSevkler Listele", null },
                    { new Guid("96dd46d0-cab5-1833-8804-30cad0c68c2e"), "siparis-islemleri.verilen-firma-siparisleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenFirmaSiparisleri > Guncelle yetkisi.", "VerilenFirmaSiparisleri Guncelle", null },
                    { new Guid("97b6ea99-c766-a946-5912-61af6ef2f0fe"), "kasa-islemleri.kasa-sayimlari.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaSayimlari > Guncelle yetkisi.", "KasaSayimlari Guncelle", null },
                    { new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"), "sevk-islemleri.sevk-planlari.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > SevkPlanlari > Ekle yetkisi.", "SevkPlanlari Ekle", null },
                    { new Guid("9bd65dad-0bf1-ae43-12ae-2af790f6103a"), "siparis-islemleri.verilen-depo-siparisleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenDepoSiparisleri > Ekle yetkisi.", "VerilenDepoSiparisleri Ekle", null },
                    { new Guid("a1e4861f-cdcb-532e-e9f1-08e984d770a6"), "siparis-islemleri.alinan-depo-siparisleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanDepoSiparisleri > Detay yetkisi.", "AlinanDepoSiparisleri Detay", null },
                    { new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"), "iade-islemleri.tedarikci-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > TedarikciIadeleri > Detay yetkisi.", "TedarikciIadeleri Detay", null },
                    { new Guid("a9e11671-15d8-6ca3-9317-ac8ef1129dee"), "siparis-islemleri.alinan-firma-siparisleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanFirmaSiparisleri > Guncelle yetkisi.", "AlinanFirmaSiparisleri Guncelle", null },
                    { new Guid("b33d1d66-e2be-57c6-e4b3-f49080e36312"), "siparis-islemleri.alinan-depo-siparisleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanDepoSiparisleri > Listele yetkisi.", "AlinanDepoSiparisleri Listele", null },
                    { new Guid("b6abd05b-a3ea-7282-7655-b28f1f0a26b1"), "sevk-islemleri.depolar-arasi-sevkler.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > DepolarArasiSevkler > Guncelle yetkisi.", "DepolarArasiSevkler Guncelle", null },
                    { new Guid("bd5765e8-6f52-a3e4-af46-54ff0b5ae4b2"), "kasa-islemleri.kasa-sayimlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaSayimlari > Listele yetkisi.", "KasaSayimlari Listele", null },
                    { new Guid("c617022a-192a-8579-98c9-2e8ae71873b1"), "siparis-islemleri.verilen-depo-siparisleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenDepoSiparisleri > Detay yetkisi.", "VerilenDepoSiparisleri Detay", null },
                    { new Guid("c9fb1ab0-900b-ebb3-502f-29803ea66f73"), "siparis-islemleri.alinan-depo-siparisleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanDepoSiparisleri > Guncelle yetkisi.", "AlinanDepoSiparisleri Guncelle", null },
                    { new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"), "mal-kabul-islemleri.mal-kabuller.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Listele yetkisi.", "MalKabuller Listele", null },
                    { new Guid("d1e35eac-313d-b9e0-1fe2-6e4ad3e29117"), "siparis-islemleri.alinan-firma-siparisleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanFirmaSiparisleri > Detay yetkisi.", "AlinanFirmaSiparisleri Detay", null },
                    { new Guid("d476adc9-a850-a787-7525-a68429fbda72"), "iade-islemleri.musteri-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > MusteriIadeleri > Detay yetkisi.", "MusteriIadeleri Detay", null },
                    { new Guid("da4d8624-84a7-c1cc-eb06-baef90f94422"), "mal-kabul-islemleri.irsaliye-kabulleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Listele yetkisi.", "IrsaliyeKabulleri Listele", null },
                    { new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"), "mal-kabul-islemleri.mal-kabuller.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Guncelle yetkisi.", "MalKabuller Guncelle", null },
                    { new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"), "mal-kabul-islemleri.mal-kabuller.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > MalKabuller > Detay yetkisi.", "MalKabuller Detay", null },
                    { new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"), "mal-kabul-islemleri.irsaliye-kabulleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "MalKabulIslemleri > IrsaliyeKabulleri > Detay yetkisi.", "IrsaliyeKabulleri Detay", null },
                    { new Guid("f63bad15-ce3b-0e08-eb0d-4e8ce49b755c"), "siparis-islemleri.verilen-firma-siparisleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > VerilenFirmaSiparisleri > Ekle yetkisi.", "VerilenFirmaSiparisleri Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0108f924-7db3-7ca6-c2ea-80ac5da8d9a4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("03f3bca3-0670-619a-d6e1-9da9cf4a759c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0e7ee02d-a676-0224-dbc6-a2f529d93a2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("143af022-4d7e-66eb-c558-624aad50fb7f"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("1c957882-c345-0563-b714-044a014b3685"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("1e557c20-df49-dac6-dc69-b0aa103560d4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("1fc04da2-9486-4a6c-04cb-d4022706d928"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22976e1d-77c4-5489-fe6d-c5db0aac96ad"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22cb488c-9c07-29dc-e0a0-726025b88fc1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("26dba67f-ab23-495f-e360-94933649a26a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("298fe457-a222-8a88-e870-2a812e93c8c6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2e05f41f-3e70-647d-b2c4-0de5b71b45fb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2fd4f83b-41dd-9fbb-d1d2-e40bdaa9f041"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("30b314b2-71d4-0960-0861-b6418dbd8f79"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("35961219-4e49-744c-f302-d4e1b627c81d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("381aca3b-27ca-8310-dc62-960634672984"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("42719ec3-0fe1-e97d-825d-5c9fe42594b4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("524b256e-b696-ccd1-6e28-481adb431a5b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("54ec0134-ed15-2ebc-b9c8-3208bd3ad0d9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5c60c66b-7679-bc4d-af47-11208ed6bb52"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("608b0c7e-a2da-5e4a-6f2b-00736ebdc5f7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("66c64021-d1e2-6cce-9ee6-77806ffb8e27"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6867eb33-82ba-f745-b9fc-b950c650b04b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7195883f-f16d-baea-fea6-189050aa0b69"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7aec07c7-50e5-84f3-9787-c23842130105"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("90695dec-7135-8331-8a53-cc0b108a364c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("96dd46d0-cab5-1833-8804-30cad0c68c2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("97b6ea99-c766-a946-5912-61af6ef2f0fe"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9bd65dad-0bf1-ae43-12ae-2af790f6103a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1e4861f-cdcb-532e-e9f1-08e984d770a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a9e11671-15d8-6ca3-9317-ac8ef1129dee"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b33d1d66-e2be-57c6-e4b3-f49080e36312"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b6abd05b-a3ea-7282-7655-b28f1f0a26b1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("bd5765e8-6f52-a3e4-af46-54ff0b5ae4b2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c617022a-192a-8579-98c9-2e8ae71873b1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c9fb1ab0-900b-ebb3-502f-29803ea66f73"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d1e35eac-313d-b9e0-1fe2-6e4ad3e29117"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d476adc9-a850-a787-7525-a68429fbda72"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("da4d8624-84a7-c1cc-eb06-baef90f94422"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f63bad15-ce3b-0e08-eb0d-4e8ce49b755c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0108f924-7db3-7ca6-c2ea-80ac5da8d9a4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("03f3bca3-0670-619a-d6e1-9da9cf4a759c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("1e557c20-df49-dac6-dc69-b0aa103560d4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1fc04da2-9486-4a6c-04cb-d4022706d928"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("26dba67f-ab23-495f-e360-94933649a26a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("2fd4f83b-41dd-9fbb-d1d2-e40bdaa9f041"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("42719ec3-0fe1-e97d-825d-5c9fe42594b4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("524b256e-b696-ccd1-6e28-481adb431a5b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("608b0c7e-a2da-5e4a-6f2b-00736ebdc5f7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7195883f-f16d-baea-fea6-189050aa0b69"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7aec07c7-50e5-84f3-9787-c23842130105"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("90695dec-7135-8331-8a53-cc0b108a364c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("96dd46d0-cab5-1833-8804-30cad0c68c2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("97b6ea99-c766-a946-5912-61af6ef2f0fe"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9bd65dad-0bf1-ae43-12ae-2af790f6103a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a1e4861f-cdcb-532e-e9f1-08e984d770a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a9e11671-15d8-6ca3-9317-ac8ef1129dee"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b33d1d66-e2be-57c6-e4b3-f49080e36312"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b6abd05b-a3ea-7282-7655-b28f1f0a26b1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("bd5765e8-6f52-a3e4-af46-54ff0b5ae4b2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c617022a-192a-8579-98c9-2e8ae71873b1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c9fb1ab0-900b-ebb3-502f-29803ea66f73"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d1e35eac-313d-b9e0-1fe2-6e4ad3e29117"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

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
                keyValues: new object[] { new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f63bad15-ce3b-0e08-eb0d-4e8ce49b755c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0108f924-7db3-7ca6-c2ea-80ac5da8d9a4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("03f3bca3-0670-619a-d6e1-9da9cf4a759c"));

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
                keyValue: new Guid("1e557c20-df49-dac6-dc69-b0aa103560d4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1fc04da2-9486-4a6c-04cb-d4022706d928"));

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
                keyValue: new Guid("26dba67f-ab23-495f-e360-94933649a26a"));

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
                keyValue: new Guid("2fd4f83b-41dd-9fbb-d1d2-e40bdaa9f041"));

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
                keyValue: new Guid("42719ec3-0fe1-e97d-825d-5c9fe42594b4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("524b256e-b696-ccd1-6e28-481adb431a5b"));

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
                keyValue: new Guid("608b0c7e-a2da-5e4a-6f2b-00736ebdc5f7"));

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
                keyValue: new Guid("693f14b5-0c0e-4902-9ec1-2441bba53003"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7195883f-f16d-baea-fea6-189050aa0b69"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7aec07c7-50e5-84f3-9787-c23842130105"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("88d9b895-3b28-f035-bc4c-d66bb274aac8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("90695dec-7135-8331-8a53-cc0b108a364c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("96dd46d0-cab5-1833-8804-30cad0c68c2e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("97b6ea99-c766-a946-5912-61af6ef2f0fe"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9af7b27e-33f6-715b-d009-f3a7bf177075"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9bd65dad-0bf1-ae43-12ae-2af790f6103a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a1e4861f-cdcb-532e-e9f1-08e984d770a6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a91159f2-6606-df6c-adff-fc380515ffa3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a9e11671-15d8-6ca3-9317-ac8ef1129dee"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b33d1d66-e2be-57c6-e4b3-f49080e36312"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b6abd05b-a3ea-7282-7655-b28f1f0a26b1"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("bd5765e8-6f52-a3e4-af46-54ff0b5ae4b2"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c617022a-192a-8579-98c9-2e8ae71873b1"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c9fb1ab0-900b-ebb3-502f-29803ea66f73"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d01a6f35-5dc4-17a5-ba91-43fb7a870d74"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d1e35eac-313d-b9e0-1fe2-6e4ad3e29117"));

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
                keyValue: new Guid("eeb59bad-31a8-ce76-6b64-7dc2326b1c7b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f0d1cc11-9d18-ea2c-6937-5aedfe2cb211"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f3a84204-122d-27e5-b6af-8dd17b328f76"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f63bad15-ce3b-0e08-eb0d-4e8ce49b755c"));

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("119a5e97-4947-4c87-9ffd-2d35e343ef53"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "roles.manage", "Create, update and assign roles.", "Manage Roles" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("79925722-5c18-4db4-9c7d-c44d6f6fd779"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "permissions.manage", "Create and update permissions.", "Manage Permissions" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fdf63a66-e9b4-4ca5-8700-2a6a34231c01"),
                columns: new[] { "code", "description", "name" },
                values: new object[] { "users.manage", "Update users and assign roles.", "Manage Users" });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"), "module.return-operations", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Access to the return operations module.", "Return Operations", null },
                    { new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"), "module.cash-operations", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Access to the cash operations module.", "Cash Operations", null },
                    { new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"), "module.goods-receipt-operations", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Access to the goods receipt operations module.", "Goods Receipt Operations", null },
                    { new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"), "module.order-operations", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Access to the order operations module.", "Order Operations", null },
                    { new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"), "module.shipment-operations", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Access to the shipment operations module.", "Shipment Operations", null },
                    { new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"), "module.user-operations", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Access to the user operations module.", "User Operations", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("23f24b09-fdf8-48b4-b064-463266c7b01b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2f1f08c8-40fc-4c7a-b2ad-0b96a42b32ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("48299ef2-7866-4890-9537-6f7ab756d3a4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("56859677-3c5b-4935-b95c-3dfdfc295c64"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8c86e0d6-507a-4d16-9bb5-b745fcb054f7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8ff01b24-aa35-4b5b-b4f6-4a0a74ad9ff0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
