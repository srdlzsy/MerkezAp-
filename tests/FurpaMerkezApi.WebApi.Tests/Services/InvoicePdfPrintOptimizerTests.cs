using FurpaMerkezApi.WebApi.Services;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Services;

public sealed class InvoicePdfPrintOptimizerTests
{
    [Fact]
    public void OptimizeForPrinting_ReturnsSinglePagePdf_WhenTailPageIsTiny()
    {
        var optimizer = new InvoicePdfPrintOptimizer();
        var source = CreatePdf(firstPageLineCount: 40, tailPageLineCount: 1);

        var result = optimizer.OptimizeForPrinting(source);

        Assert.True(
            CountPages(result) == 1,
            BuildPdfTextDiagnostic(source, result));
    }

    [Fact]
    public void OptimizeForPrinting_KeepsTwoPages_WhenTailPageCompactionWouldReduceReadability()
    {
        var optimizer = new InvoicePdfPrintOptimizer();
        var source = CreatePdf(firstPageLineCount: 48, tailPageLineCount: 3);

        var result = optimizer.OptimizeForPrinting(source);

        Assert.Equal(2, CountPages(result));
    }

    [Fact]
    public void OptimizeForPrinting_KeepsSinglePagePdf()
    {
        var optimizer = new InvoicePdfPrintOptimizer();
        var source = CreatePdf(firstPageLineCount: 10, tailPageLineCount: 0);

        var result = optimizer.OptimizeForPrinting(source);

        Assert.Equal(1, CountPages(result));
    }

    [Fact]
    public void OptimizeForPrinting_RemovesBlankTailPage()
    {
        var optimizer = new InvoicePdfPrintOptimizer();
        var source = CreatePdfWithBlankTailPage(firstPageLineCount: 40);

        var result = optimizer.OptimizeForPrinting(source);

        Assert.Equal(1, CountPages(result));
    }

    [Fact]
    public void OptimizeForPrinting_RemovesTextlessTailPageWithDrawingContent()
    {
        var optimizer = new InvoicePdfPrintOptimizer();
        var source = CreatePdfWithTextlessTailPage(firstPageLineCount: 40);

        var result = optimizer.OptimizeForPrinting(source);

        Assert.Equal(1, CountPages(result));
    }

    private static byte[] CreatePdf(int firstPageLineCount, int tailPageLineCount)
    {
        using var document = new PdfDocument();
        AddPage(document, firstPageLineCount);

        if (tailPageLineCount > 0)
        {
            AddPage(document, tailPageLineCount);
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static byte[] CreatePdfWithBlankTailPage(int firstPageLineCount)
    {
        using var document = new PdfDocument();
        AddPage(document, firstPageLineCount);
        document.AddPage();

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static byte[] CreatePdfWithTextlessTailPage(int firstPageLineCount)
    {
        using var document = new PdfDocument();
        AddPage(document, firstPageLineCount);

        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(XPens.Transparent, 0, 0, page.Width, page.Height);

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void AddPage(PdfDocument document, int lineCount)
    {
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 10);

        for (var index = 0; index < lineCount; index++)
        {
            gfx.DrawString(
                $"Invoice line {index + 1:00} - product quantity amount tax total",
                font,
                XBrushes.Black,
                40,
                40 + (index * 16));
        }
    }

    private static int CountPages(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    private static string BuildPdfTextDiagnostic(byte[] source, byte[] result)
    {
        using var sourceDocument = PdfPigDocument.Open(source);
        var first = sourceDocument.GetPage(1);
        var last = sourceDocument.GetPage(sourceDocument.NumberOfPages);

        return
            $"Expected compacted page count 1 but got {CountPages(result)}. " +
            $"Source pages={sourceDocument.NumberOfPages}; " +
            $"firstLetters={first.Letters.Count}; lastLetters={last.Letters.Count}; " +
            $"firstHeight={MeasureHeight(first):0.##}; lastHeight={MeasureHeight(last):0.##}; " +
            $"pageHeight={last.Height:0.##}";
    }

    private static double MeasureHeight(UglyToad.PdfPig.Content.Page page)
    {
        if (page.Letters.Count == 0)
        {
            return 0;
        }

        return page.Letters.Max(letter => letter.GlyphRectangle.Top) -
               page.Letters.Min(letter => letter.GlyphRectangle.Bottom);
    }
}
