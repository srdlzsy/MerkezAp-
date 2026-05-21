using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveKunyeLabelPrintingToCashModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("91f4518e-007d-ca11-e8b4-03967a2b4624"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.kunye-etiket-yazdirma.update", "KasaIslemleri > KunyeEtiketYazdirma > Guncelle yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c80495b3-55cf-b620-df21-3b052d3bfdcb"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.kunye-etiket-yazdirma.detail", "KasaIslemleri > KunyeEtiketYazdirma > Detay yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.kunye-etiket-yazdirma.list", "KasaIslemleri > KunyeEtiketYazdirma > Listele yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"),
                columns: new[] { "code", "description" },
                values: new object[] { "kasa-islemleri.kunye-etiket-yazdirma.create", "KasaIslemleri > KunyeEtiketYazdirma > Ekle yetkisi." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("91f4518e-007d-ca11-e8b4-03967a2b4624"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.kunye-etiket-yazdirma.update", "StokIslemleri > KunyeEtiketYazdirma > Guncelle yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c80495b3-55cf-b620-df21-3b052d3bfdcb"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.kunye-etiket-yazdirma.detail", "StokIslemleri > KunyeEtiketYazdirma > Detay yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.kunye-etiket-yazdirma.list", "StokIslemleri > KunyeEtiketYazdirma > Listele yetkisi." });

            migrationBuilder.UpdateData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"),
                columns: new[] { "code", "description" },
                values: new object[] { "stok-islemleri.kunye-etiket-yazdirma.create", "StokIslemleri > KunyeEtiketYazdirma > Ekle yetkisi." });
        }
    }
}
