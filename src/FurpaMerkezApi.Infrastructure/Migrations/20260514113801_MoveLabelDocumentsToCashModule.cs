using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveLabelDocumentsToCashModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("07f77149-c49d-0e3b-f04a-9a24698d1a05"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.etiket-belgeleri.list", "KasaIslemleri > EtiketBelgeleri > Listele yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("625b8bb0-4009-8ebe-8071-300c19ec095e"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.etiket-belgeleri.create", "KasaIslemleri > EtiketBelgeleri > Ekle yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.etiket-belgeleri.update", "KasaIslemleri > EtiketBelgeleri > Guncelle yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fbc155f3-f37d-a25e-3a0b-74628238f508"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.etiket-belgeleri.detail", "KasaIslemleri > EtiketBelgeleri > Detay yetkisi." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("07f77149-c49d-0e3b-f04a-9a24698d1a05"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.etiket-belgeleri.list", "StokIslemleri > EtiketBelgeleri > Listele yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("625b8bb0-4009-8ebe-8071-300c19ec095e"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.etiket-belgeleri.create", "StokIslemleri > EtiketBelgeleri > Ekle yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.etiket-belgeleri.update", "StokIslemleri > EtiketBelgeleri > Guncelle yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("fbc155f3-f37d-a25e-3a0b-74628238f508"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.etiket-belgeleri.detail", "StokIslemleri > EtiketBelgeleri > Detay yetkisi." });
        }
    }
}
