using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace FurpaMerkezApi.WebApi.Services;

public sealed class InvoicePdfPrintOptimizer : IInvoicePdfPrintOptimizer
{
    private const int MaxPagesToCompact = 3;
    private const int MinPagesToCompact = 2;
    private const double MaxLastPageContentRatio = 0.18d;
    private const double MaxLastPageHeightRatio = 0.24d;
    private const double PageMargin = 18d;
    private const double GapBetweenPages = 8d;
    private const double FirstPageMinScale = 0.80d;
    private const double TailPageCropPadding = 24d;

    public byte[] OptimizeForPrinting(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0)
        {
            return pdfBytes;
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"furpa-invoice-pdf-{Guid.NewGuid():N}.pdf");

        try
        {
            File.WriteAllBytes(tempFile, pdfBytes);

            using var input = PdfReader.Open(tempFile, PdfDocumentOpenMode.Import);
            if (input.PageCount is < MinPagesToCompact or > MaxPagesToCompact)
            {
                return pdfBytes;
            }

            var pageContents = MeasureTextContents(pdfBytes, input.PageCount);
            if (pageContents.Count != input.PageCount)
            {
                return pdfBytes;
            }

            var nonBlankPageCount = CountPagesBeforeBlankTail(pageContents);
            if (nonBlankPageCount < input.PageCount)
            {
                return CopyFirstPages(tempFile, nonBlankPageCount);
            }

            if (!HasTinyTailPage(pageContents))
            {
                return pdfBytes;
            }

            var output = new PdfDocument();
            var targetPage = output.AddPage();
            targetPage.Width = input.Pages[0].Width;
            targetPage.Height = input.Pages[0].Height;
            targetPage.Orientation = input.Pages[0].Orientation;

            using var gfx = XGraphics.FromPdfPage(targetPage);
            using var form = XPdfForm.FromFile(tempFile);

            var pageWidth = targetPage.Width.Point;
            var pageHeight = targetPage.Height.Point;
            var contentWidth = pageWidth - (PageMargin * 2d);
            var contentHeight = pageHeight - (PageMargin * 2d);
            var tailPageContents = pageContents.Skip(1).ToArray();
            var tailSlotHeights = tailPageContents
                .Select(content => content.CropHeight * (contentWidth / content.PageWidth))
                .ToArray();
            var firstSlotHeight = contentHeight - tailSlotHeights.Sum() - (tailSlotHeights.Length * GapBetweenPages);
            var firstScale = Math.Min(contentWidth / input.Pages[0].Width.Point, firstSlotHeight / input.Pages[0].Height.Point);

            if (firstScale < FirstPageMinScale)
            {
                return pdfBytes;
            }

            DrawPage(form, input.Pages[0], gfx, 1, PageMargin, PageMargin, contentWidth, firstSlotHeight);

            var y = PageMargin + firstSlotHeight + GapBetweenPages;
            for (var index = 1; index < input.PageCount; index++)
            {
                var content = pageContents[index];
                var tailSlotHeight = tailSlotHeights[index - 1];
                DrawCroppedPage(form, gfx, index + 1, content, PageMargin, y, contentWidth, tailSlotHeight);
                y += tailSlotHeight + GapBetweenPages;
            }

            using var outputStream = new MemoryStream();
            output.Save(outputStream, closeStream: false);
            return outputStream.ToArray();
        }
        catch
        {
            return pdfBytes;
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    private static bool HasTinyTailPage(IReadOnlyList<PageTextContent> pageContents)
    {
        var firstPageContent = pageContents[0];
        var lastPageContent = pageContents[^1];

        if (!firstPageContent.HasContent || !lastPageContent.HasContent)
        {
            return false;
        }

        if (pageContents.Skip(1).Any(content =>
                !content.HasContent ||
                content.LetterCount / (double)firstPageContent.LetterCount > MaxLastPageContentRatio ||
                content.ContentHeight / content.PageHeight > MaxLastPageHeightRatio))
        {
            return false;
        }

        return true;
    }

    private static int CountPagesBeforeBlankTail(IReadOnlyList<PageTextContent> pageContents)
    {
        var pageCount = pageContents.Count;
        while (pageCount > 1 &&
               !pageContents[pageCount - 1].HasContent)
        {
            pageCount--;
        }

        return pageCount;
    }

    private static byte[] CopyFirstPages(string tempFile, int pageCount)
    {
        using var input = PdfReader.Open(tempFile, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();

        for (var index = 0; index < pageCount; index++)
        {
            output.AddPage(input.Pages[index]);
        }

        using var outputStream = new MemoryStream();
        output.Save(outputStream, closeStream: false);
        return outputStream.ToArray();
    }

    private static IReadOnlyList<PageTextContent> MeasureTextContents(byte[] pdfBytes, int pageCount)
    {
        using var document = PdfPigDocument.Open(pdfBytes);
        if (document.NumberOfPages != pageCount)
        {
            return [];
        }

        return Enumerable.Range(1, document.NumberOfPages)
            .Select(pageNumber => MeasureTextContent(document.GetPage(pageNumber)))
            .ToArray();
    }

    private static PageTextContent MeasureTextContent(UglyToad.PdfPig.Content.Page page)
    {
        var letters = page.Letters.ToArray();
        if (letters.Length == 0)
        {
            return new PageTextContent(false, 0, 0d, page.Width, page.Height, 0d, 0d);
        }

        var top = letters.Max(letter => letter.GlyphRectangle.Top);
        var bottom = letters.Min(letter => letter.GlyphRectangle.Bottom);
        var paddedTop = Math.Min(page.Height, top + TailPageCropPadding);
        var paddedBottom = Math.Max(0d, bottom - TailPageCropPadding);
        return new PageTextContent(
            true,
            letters.Length,
            top - bottom,
            page.Width,
            page.Height,
            paddedTop,
            paddedBottom);
    }

    private static void DrawPage(
        XPdfForm form,
        PdfPage sourcePage,
        XGraphics gfx,
        int pageNumber,
        double x,
        double y,
        double maxWidth,
        double maxHeight)
    {
        form.PageNumber = pageNumber;

        var sourceWidth = sourcePage.Width.Point;
        var sourceHeight = sourcePage.Height.Point;
        var scale = Math.Min(maxWidth / sourceWidth, maxHeight / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var centeredX = x + ((maxWidth - width) / 2d);

        gfx.DrawImage(form, centeredX, y, width, height);
    }

    private static void DrawCroppedPage(
        XPdfForm form,
        XGraphics gfx,
        int pageNumber,
        PageTextContent sourceContent,
        double x,
        double y,
        double width,
        double height)
    {
        form.PageNumber = pageNumber;

        var sourceY = sourceContent.PageHeight - sourceContent.CropTop;
        var sourceRect = new XRect(0d, sourceY, sourceContent.PageWidth, sourceContent.CropHeight);
        var destinationRect = new XRect(x, y, width, height);

        gfx.DrawImage(form, destinationRect, sourceRect, XGraphicsUnit.Point);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temp file cleanup should not block returning the original PDF.
        }
    }

    private readonly record struct PageTextContent(
        bool HasContent,
        int LetterCount,
        double ContentHeight,
        double PageWidth,
        double PageHeight,
        double CropTop,
        double CropBottom)
    {
        public double CropHeight => CropTop - CropBottom;
    }
}
