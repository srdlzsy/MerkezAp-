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
        Assert.True(pdf.GetPage(1).Height > pdf.GetPage(1).Width);
        Assert.Contains("Kaynak depo", pdf.GetPage(1).Text);
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

    [Fact]
    public void Render_FitsFortySingleLineItemsOnFirstPage()
    {
        var renderer = new ReceivedWarehouseOrderPdfRenderer();
        var pdfBytes = renderer.Render(
        [
            CreateDocument("F50", 1004, "010001", "URUN", itemCount: 40)
        ]);

        using var pdf = PdfDocument.Open(pdfBytes);

        Assert.True(pdf.NumberOfPages > 1);
        Assert.Contains("URUN 40", pdf.GetPage(1).Text);
        Assert.DoesNotContain(" - DEVAM", pdf.GetPage(1).Text);
    }

    [Fact]
    public void Render_GreenGrocerDocument_UsesNarrowLeftTableAndTurkishAlphabeticalProductOrder()
    {
        var renderer = new ReceivedWarehouseOrderPdfRenderer();
        var baseDocument = CreateDocument("F56", 1005, "010001", "URUN");
        var document = baseDocument with
        {
            Header = baseDocument.Header with
            {
                OutWarehouseNo = 56,
                OutWarehouseName = "MANAV DEPO"
            },
            Items =
            [
                CreateItem(0, "010003", "Zeytin"),
                CreateItem(1, "010001", "Armut"),
                CreateItem(2, "010002", "Çilek")
            ]
        };

        var pdfBytes = renderer.Render([document]);

        using var pdf = PdfDocument.Open(pdfBytes);
        var text = pdf.GetPage(1).Text;

        Assert.True(text.IndexOf("Armut", StringComparison.Ordinal) < text.IndexOf("Çilek", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Çilek", StringComparison.Ordinal) < text.IndexOf("Zeytin", StringComparison.Ordinal));
        Assert.Equal(340d, ReceivedWarehouseOrderPdfRenderer.ResolveTableWidth(56));
        Assert.Equal(510d, ReceivedWarehouseOrderPdfRenderer.ResolveTableWidth(50));
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

    private static WarehouseOrderLineItemDto CreateItem(int lineNo, string stockCode, string stockName) =>
        new(
            Guid.NewGuid(),
            lineNo,
            stockCode,
            stockName,
            "ADET",
            1,
            10,
            2,
            8,
            0,
            0,
            false,
            string.Empty,
            string.Empty,
            string.Empty);
}
