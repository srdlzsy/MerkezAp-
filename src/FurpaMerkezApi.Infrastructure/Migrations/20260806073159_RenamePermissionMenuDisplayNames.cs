using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePermissionMenuDisplayNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1e67c648-d9f5-df94-0c66-6c3bdb2cf55a"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Guncelle yetkisi.", "ManavKasaProfilleri Guncelle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2ef0061f-7c35-03ae-f62d-0031ce84c39d"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Tum Depolar yetkisi.", "ManavKasaProfilleri Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3f3280e7-38b2-bc34-8670-f79cee1179c3"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Detay yetkisi.", "ManavKasaProfilleri Detay" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("421772b7-2615-d8e9-41f6-929c9a40e598"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operasyonlar > Sayfa yetkisi.", "Operasyonlar Sayfa" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4811d3bc-016f-415b-0807-92d30c3c5597"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Sil yetkisi.", "ManavKasaProfilleri Sil" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4c1d56b3-8118-1b26-61bc-3d7a14929c56"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavOperasyonPaneli > Tum Depolar yetkisi.", "ManavOperasyonPaneli Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("599a768c-e6aa-92bf-d190-a19967ec91c2"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavRaporlari > Tum Depolar yetkisi.", "ManavRaporlari Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5de21f6b-8f62-7572-e757-24d3496082d7"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavOperasyonPaneli > Listele yetkisi.", "ManavOperasyonPaneli Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6b6947e2-6d4c-be49-209d-df7e2d729376"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operasyonlar > Ekle yetkisi.", "Operasyonlar Ekle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6c4f09e8-5e52-b8d8-e8c2-0cd656e87d05"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavRaporlari > Guncelle yetkisi.", "ManavRaporlari Guncelle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6ff0fa91-1ee1-007a-155e-302ab904eebf"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavOperasyonPaneli > Sayfa yetkisi.", "ManavOperasyonPaneli Sayfa" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("70805ff4-2179-ba22-e5e4-820aa8859302"),
                column: "description",
                value: "AnaSayfa > DepoOncelikleri > Sayfa yetkisi.");

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("719de8b9-05de-e6c0-bc48-97e07a6a7b32"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavRaporlari > Sayfa yetkisi.", "ManavRaporlari Sayfa" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("808659ad-b79d-6ce4-2583-da7e64762c3d"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavRaporlari > Listele yetkisi.", "ManavRaporlari Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c21c804-6071-72c7-3ff6-b9a659684b0b"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Listele yetkisi.", "ManavKasaProfilleri Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9e531925-eb3e-bf49-2463-4761e8b4276f"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operasyonlar > Tum Depolar yetkisi.", "Operasyonlar Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a6419da9-86be-45e1-6384-616080de59f5"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Yonet yetkisi.", "ManavKasaProfilleri Yonet" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aa6b811d-ebe3-77da-817a-6c6a0db4807d"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operasyonlar > Listele yetkisi.", "Operasyonlar Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aa824ba7-40df-cd78-4c6d-cc7f0662dfd6"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operasyonlar > Guncelle yetkisi.", "Operasyonlar Guncelle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ac61bf1b-9ccf-c771-332b-3ab210633343"),
                column: "description",
                value: "AnaSayfa > DepoOncelikleri > Tum Depolar yetkisi.");

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("cc67c409-9264-efcf-0e23-3fa96cb40d73"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavOperasyonPaneli > Ekle yetkisi.", "ManavOperasyonPaneli Ekle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ddcfd86f-cb2e-bafc-d56d-e9e4bcf7ac4a"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavRaporlari > Detay yetkisi.", "ManavRaporlari Detay" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e562f7cc-2e91-f49e-b80f-200acb23acce"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operasyonlar > Detay yetkisi.", "Operasyonlar Detay" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fe4c9cef-8678-040d-1af7-41236e9da805"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manav > ManavKasaProfilleri > Ekle yetkisi.", "ManavKasaProfilleri Ekle" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1e67c648-d9f5-df94-0c66-6c3bdb2cf55a"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Guncelle yetkisi.", "ProductCaseProfiles Guncelle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2ef0061f-7c35-03ae-f62d-0031ce84c39d"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Tum Depolar yetkisi.", "ProductCaseProfiles Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3f3280e7-38b2-bc34-8670-f79cee1179c3"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Detay yetkisi.", "ProductCaseProfiles Detay" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("421772b7-2615-d8e9-41f6-929c9a40e598"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operations > Sayfa yetkisi.", "Operations Sayfa" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4811d3bc-016f-415b-0807-92d30c3c5597"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Sil yetkisi.", "ProductCaseProfiles Sil" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4c1d56b3-8118-1b26-61bc-3d7a14929c56"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Operations > Tum Depolar yetkisi.", "Operations Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("599a768c-e6aa-92bf-d190-a19967ec91c2"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Reports > Tum Depolar yetkisi.", "Reports Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5de21f6b-8f62-7572-e757-24d3496082d7"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Operations > Listele yetkisi.", "Operations Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6b6947e2-6d4c-be49-209d-df7e2d729376"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operations > Ekle yetkisi.", "Operations Ekle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6c4f09e8-5e52-b8d8-e8c2-0cd656e87d05"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Reports > Guncelle yetkisi.", "Reports Guncelle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6ff0fa91-1ee1-007a-155e-302ab904eebf"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Operations > Sayfa yetkisi.", "Operations Sayfa" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("70805ff4-2179-ba22-e5e4-820aa8859302"),
                column: "description",
                value: "Home > DepoOncelikleri > Sayfa yetkisi.");

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("719de8b9-05de-e6c0-bc48-97e07a6a7b32"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Reports > Sayfa yetkisi.", "Reports Sayfa" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("808659ad-b79d-6ce4-2583-da7e64762c3d"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Reports > Listele yetkisi.", "Reports Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c21c804-6071-72c7-3ff6-b9a659684b0b"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Listele yetkisi.", "ProductCaseProfiles Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9e531925-eb3e-bf49-2463-4761e8b4276f"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operations > Tum Depolar yetkisi.", "Operations Tum Depolar" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a6419da9-86be-45e1-6384-616080de59f5"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Yonet yetkisi.", "ProductCaseProfiles Yonet" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aa6b811d-ebe3-77da-817a-6c6a0db4807d"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operations > Listele yetkisi.", "Operations Listele" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aa824ba7-40df-cd78-4c6d-cc7f0662dfd6"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operations > Guncelle yetkisi.", "Operations Guncelle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ac61bf1b-9ccf-c771-332b-3ab210633343"),
                column: "description",
                value: "Home > DepoOncelikleri > Tum Depolar yetkisi.");

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("cc67c409-9264-efcf-0e23-3fa96cb40d73"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Operations > Ekle yetkisi.", "Operations Ekle" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ddcfd86f-cb2e-bafc-d56d-e9e4bcf7ac4a"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > Reports > Detay yetkisi.", "Reports Detay" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e562f7cc-2e91-f49e-b80f-200acb23acce"),
                columns: new[] { "description", "name" },
                values: new object[] { "OperasyonIslemleri > Operations > Detay yetkisi.", "Operations Detay" });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fe4c9cef-8678-040d-1af7-41236e9da805"),
                columns: new[] { "description", "name" },
                values: new object[] { "GreenGrocer > ProductCaseProfiles > Ekle yetkisi.", "ProductCaseProfiles Ekle" });
        }
    }
}
