using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Services;
using UglyToad.PdfPig;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.SiparisIslemleri.AlinanDepoSiparisleri;

public sealed class ReceivedWarehouseOrderBulkPrintTests
{
    [Fact]
    public void PermissionCatalog_IncludesPrintPermission()
    {
        Assert.Contains(
            PermissionCatalog.Codes,
            code => code == "siparis-islemleri.alinan-depo-siparisleri.print");
    }

    [Fact]
    public void Render_StartsEveryDocumentOnANewPage()
    {
        var renderer = new ReceivedWarehouseOrderPdfRenderer();
        var pdfBytes = renderer.Render(
        [
            CreateDocument("F50", 1001, "015550", "BIRINCI URUN"),
            CreateDocument("F50", 1002, "016201", "IKINCI URUN")
        ]);

        using var pdf = PdfDocument.Open(pdfBytes);

        Assert.Equal(2, pdf.NumberOfPages);
        Assert.Contains("F50/1001", pdf.GetPage(1).Text);
        Assert.DoesNotContain("F50/1002", pdf.GetPage(1).Text);
        Assert.Contains("F50/1002", pdf.GetPage(2).Text);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }

    [Fact]
    public void Render_RepeatsDocumentHeaderOnContinuationPages()
    {
        var renderer = new ReceivedWarehouseOrderPdfRenderer();
        var pdfBytes = renderer.Render(
        [
            CreateDocument("F50", 1003, "010001", "UZUN SIPARIS URUNU", itemCount: 60)
        ]);

        using var pdf = PdfDocument.Open(pdfBytes);

        Assert.True(pdf.NumberOfPages > 1);
        Assert.Contains("F50/1003 - DEVAM", pdf.GetPage(2).Text);
        Assert.Contains("Stok Kodu", pdf.GetPage(2).Text);
    }

    private static WarehouseOrderDetailDto CreateDocument(
        string documentSerie,
        int documentOrderNo,
        string stockCode,
        string stockName,
        int itemCount = 1)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(lineNo => new WarehouseOrderLineItemDto(
                Guid.NewGuid(),
                lineNo,
                $"{stockCode}-{lineNo:00}",
                $"{stockName} {lineNo + 1}",
                "ADET",
                1,
                10,
                2,
                8,
                0,
                0,
                false,
                "Test aciklamasi",
                string.Empty,
                string.Empty))
            .ToArray();
        var header = new WarehouseOrderHeaderDto(
            WarehouseOrderDocumentKey.Create(50, documentSerie, documentOrderNo),
            new DateTime(2026, 9, 9),
            new DateTime(2026, 9, 10),
            documentSerie,
            documentOrderNo,
            string.Empty,
            50,
            "MERKEZ DEPO",
            120,
            "DEPO 120",
            120,
            "DEPO 120",
            50,
            "MERKEZ DEPO",
            itemCount,
            itemCount * 10,
            itemCount * 2,
            itemCount * 8,
            0,
            false);

        return new WarehouseOrderDetailDto(header, items);
    }
}
