using System.Globalization;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace FurpaMerkezApi.WebApi.Services;

public sealed class ReceivedWarehouseOrderPdfRenderer : IReceivedWarehouseOrderPdfRenderer
{
    private const double PageMargin = 42.5d;
    private const double FooterHeight = 24d;
    private const double HeaderHeight = 126d;
    private const double TableHeaderHeight = 24d;
    private const double MinimumRowHeight = 22d;
    private const double CellPadding = 4d;

    private static readonly XColor BorderColor = XColor.FromArgb(25, 25, 25);
    private static readonly XColor LightHeaderColor = XColor.FromArgb(238, 238, 238);

    static ReceivedWarehouseOrderPdfRenderer()
    {
        PdfSharpConfiguration.EnsureInitialized();
    }

    private readonly XFont titleFont = new("Times New Roman", 18, XFontStyleEx.Bold);
    private readonly XFont documentFont = new("Times New Roman", 11, XFontStyleEx.Bold);
    private readonly XFont headerFont = new("Times New Roman", 9, XFontStyleEx.Bold);
    private readonly XFont bodyFont = new("Times New Roman", 9, XFontStyleEx.Regular);
    private readonly XFont smallFont = new("Times New Roman", 7, XFontStyleEx.Regular);
    private readonly XFont smallBoldFont = new("Times New Roman", 8, XFontStyleEx.Bold);

    public byte[] Render(IReadOnlyCollection<WarehouseOrderDetailDto> documents)
    {
        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one warehouse order is required.", nameof(documents));
        }

        using var pdf = new PdfDocument();
        pdf.Info.Title = "Alinan Depo Siparisleri";
        pdf.Info.Subject = "Toplu depo siparisi yazdirma";
        pdf.Info.Creator = "FurpaMerkezApi";

        foreach (var document in documents)
        {
            RenderDocument(pdf, document);
        }

        using var output = new MemoryStream();
        pdf.Save(output, closeStream: false);
        return output.ToArray();
    }

    private void RenderDocument(PdfDocument pdf, WarehouseOrderDetailDto document)
    {
        var documentPageNo = 0;
        var pageState = CreatePage(pdf, document, ++documentPageNo, continuation: false);
        var rowIndex = 0;

        foreach (var item in document.Items.OrderBy(item => item.LineNo))
        {
            var rowHeight = CalculateRowHeight(item);
            if (pageState.Y + rowHeight > pageState.ContentBottom)
            {
                pageState.Dispose();
                pageState = CreatePage(pdf, document, ++documentPageNo, continuation: true);
            }

            DrawRow(pageState.Graphics, pageState.Y, rowHeight, item, rowIndex++ % 2 == 1);
            pageState.Y += rowHeight;
        }

        const double totalsHeight = 29d;
        const double signaturesHeight = 52d;
        if (pageState.Y + totalsHeight + signaturesHeight > pageState.ContentBottom)
        {
            pageState.Dispose();
            pageState = CreatePage(pdf, document, ++documentPageNo, continuation: true);
        }

        DrawTotals(pageState.Graphics, pageState.Y, document.Header);
        pageState.Y += totalsHeight;
        DrawSignatures(pageState.Graphics, pageState.Y);
        pageState.Dispose();
    }

    private PageState CreatePage(
        PdfDocument pdf,
        WarehouseOrderDetailDto document,
        int documentPageNo,
        bool continuation)
    {
        var page = pdf.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Portrait;
        var graphics = XGraphics.FromPdfPage(page);
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;

        DrawDocumentHeader(graphics, pageWidth, document.Header, continuation);
        DrawTableHeader(graphics, HeaderHeight);
        graphics.DrawString(
            $"Evrak sayfasi: {documentPageNo}",
            smallFont,
            XBrushes.Black,
            new XRect(PageMargin, pageHeight - FooterHeight, pageWidth - (2 * PageMargin), 12d),
            XStringFormats.CenterRight);
        graphics.DrawString(
            $"Yazdirma: {DateTime.Now:dd.MM.yyyy HH:mm}",
            smallFont,
            XBrushes.Black,
            new XRect(PageMargin, pageHeight - FooterHeight, pageWidth - (2 * PageMargin), 12d),
            XStringFormats.CenterLeft);

        return new PageState(
            graphics,
            HeaderHeight + TableHeaderHeight,
            pageHeight - FooterHeight - 4d);
    }

    private void DrawDocumentHeader(
        XGraphics graphics,
        double pageWidth,
        WarehouseOrderHeaderDto header,
        bool continuation)
    {
        var contentWidth = pageWidth - (2 * PageMargin);
        graphics.DrawString(
            "ALINAN DEPO SIPARISI",
            titleFont,
            XBrushes.Black,
            new XRect(PageMargin, PageMargin, contentWidth, 24d),
            XStringFormats.TopCenter);
        graphics.DrawLine(new XPen(BorderColor, 1.2d), PageMargin, 69d, pageWidth - PageMargin, 69d);

        var documentLabel = $"{header.DocumentSerie}/{header.DocumentOrderNo}";
        if (continuation)
        {
            documentLabel += " - DEVAM";
        }

        graphics.DrawString(
            documentLabel,
            documentFont,
            XBrushes.Black,
            new XRect(PageMargin, 74d, contentWidth * 0.34d, 16d),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            $"Siparis: {FormatDate(header.DocumentDate)}  Teslim: {FormatDate(header.DeliveryDate)}",
            bodyFont,
            XBrushes.Black,
            new XRect(PageMargin + (contentWidth * 0.34d), 74d, contentWidth * 0.66d, 16d),
            XStringFormats.CenterRight);
        graphics.DrawString(
            $"Siparisi veren: {header.InWarehouseNo} - {EmptyAsDash(header.InWarehouseName)}",
            bodyFont,
            XBrushes.Black,
            new XRect(PageMargin, 94d, contentWidth, 14d),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            $"Kaynak depo: {header.OutWarehouseNo} - {EmptyAsDash(header.OutWarehouseName)}",
            bodyFont,
            XBrushes.Black,
            new XRect(PageMargin, 110d, contentWidth * 0.7d, 14d),
            XStringFormats.CenterLeft);
        graphics.DrawString(
            $"Belge no: {EmptyAsDash(header.DocumentNumber)}",
            bodyFont,
            XBrushes.Black,
            new XRect(PageMargin + (contentWidth * 0.7d), 110d, contentWidth * 0.3d, 14d),
            XStringFormats.CenterRight);
    }

    private void DrawTableHeader(XGraphics graphics, double y)
    {
        var x = PageMargin;
        foreach (var column in Columns)
        {
            graphics.DrawRectangle(new XSolidBrush(LightHeaderColor), x, y, column.Width, TableHeaderHeight);
            graphics.DrawRectangle(new XPen(BorderColor, 0.7d), x, y, column.Width, TableHeaderHeight);
            graphics.DrawString(
                column.Title,
                headerFont,
                XBrushes.Black,
                new XRect(x + CellPadding, y + 3d, column.Width - (2 * CellPadding), TableHeaderHeight - 6d),
                column.Alignment);
            x += column.Width;
        }
    }

    private void DrawRow(
        XGraphics graphics,
        double y,
        double height,
        WarehouseOrderLineItemDto item,
        bool alternate)
    {
        var values = new[]
        {
            (item.LineNo + 1).ToString(CultureInfo.InvariantCulture),
            item.StockCode,
            FormatProduct(item),
            FormatNumber(item.Quantity),
            item.UnitName,
            FormatNumber(item.DeliveredQuantity),
            FormatNumber(item.RemainingQuantity)
        };

        var x = PageMargin;
        for (var index = 0; index < Columns.Length; index++)
        {
            var column = Columns[index];
            if (alternate)
            {
                graphics.DrawRectangle(XBrushes.WhiteSmoke, x, y, column.Width, height);
            }

            graphics.DrawRectangle(new XPen(BorderColor, 0.45d), x, y, column.Width, height);
            var textRect = new XRect(
                x + CellPadding,
                y + 4d,
                column.Width - (2 * CellPadding),
                height - 6d);

            if (index == 2)
            {
                var lines = WrapText(graphics, values[index], bodyFont, textRect.Width, 3);
                for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    graphics.DrawString(
                        lines[lineIndex],
                        bodyFont,
                        XBrushes.Black,
                        new XRect(textRect.X, textRect.Y + (lineIndex * 10d), textRect.Width, 10d),
                        XStringFormats.TopLeft);
                }
            }
            else
            {
                graphics.DrawString(
                    values[index] ?? string.Empty,
                    bodyFont,
                    XBrushes.Black,
                    textRect,
                    column.Alignment);
            }

            x += column.Width;
        }
    }

    private void DrawTotals(XGraphics graphics, double y, WarehouseOrderHeaderDto header)
    {
        var firstThreeColumnsWidth = Columns.Take(3).Sum(column => column.Width);
        var x = PageMargin;
        DrawTotalCell(graphics, x, y, firstThreeColumnsWidth, $"Toplam {header.LineCount} kalem");
        x += firstThreeColumnsWidth;
        DrawTotalCell(graphics, x, y, Columns[3].Width, FormatNumber(header.TotalQuantity));
        x += Columns[3].Width;
        DrawTotalCell(graphics, x, y, Columns[4].Width, string.Empty);
        x += Columns[4].Width;
        DrawTotalCell(graphics, x, y, Columns[5].Width, FormatNumber(header.TotalDeliveredQuantity));
        x += Columns[5].Width;
        DrawTotalCell(graphics, x, y, Columns[6].Width, FormatNumber(header.TotalRemainingQuantity));
    }

    private void DrawTotalCell(XGraphics graphics, double x, double y, double width, string value)
    {
        graphics.DrawRectangle(new XSolidBrush(LightHeaderColor), x, y, width, 27d);
        graphics.DrawRectangle(new XPen(BorderColor, 0.7d), x, y, width, 27d);
        graphics.DrawString(
            value,
            headerFont,
            XBrushes.Black,
            new XRect(x + CellPadding, y + 4d, width - (2 * CellPadding), 19d),
            XStringFormats.CenterRight);
    }

    private void DrawSignatures(XGraphics graphics, double y)
    {
        var contentWidth = Columns.Sum(column => column.Width);
        var boxWidth = contentWidth / 3d;
        var labels = new[] { "Hazirlayan", "Kontrol Eden", "Teslim Alan" };

        for (var index = 0; index < labels.Length; index++)
        {
            var x = PageMargin + (index * boxWidth);
            graphics.DrawRectangle(new XPen(BorderColor, 0.6d), x, y + 7d, boxWidth, 40d);
            graphics.DrawString(
                labels[index],
                smallBoldFont,
                XBrushes.Black,
                new XRect(x + CellPadding, y + 10d, boxWidth - (2 * CellPadding), 12d),
                XStringFormats.TopCenter);
        }
    }

    private double CalculateRowHeight(WarehouseOrderLineItemDto item)
    {
        var productLines = EstimateLineCount(FormatProduct(item), Columns[2].Width, 47d);
        return Math.Max(MinimumRowHeight, 8d + (productLines * 10d));
    }

    private static string FormatProduct(WarehouseOrderLineItemDto item) =>
        string.IsNullOrWhiteSpace(item.Description)
            ? item.StockName
            : $"{item.StockName} - {item.Description}";

    private static int EstimateLineCount(string? value, double columnWidth, double averageCharacterWidth)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1;
        }

        var charactersPerLine = Math.Max(8, (int)((columnWidth - (2 * CellPadding)) / averageCharacterWidth * 8d));
        return Math.Min(3, (int)Math.Ceiling(value.Trim().Length / (double)charactersPerLine));
    }

    private static IReadOnlyList<string> WrapText(
        XGraphics graphics,
        string? value,
        XFont font,
        double maxWidth,
        int maxLines)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [string.Empty];
        }

        var words = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (graphics.MeasureString(candidate, font).Width <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            currentLine = word;
            if (lines.Count == maxLines - 1)
            {
                break;
            }
        }

        if (lines.Count < maxLines && !string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        if (lines.Count == maxLines && words.Length > lines.Sum(line => line.Split(' ').Length))
        {
            lines[^1] = TrimToWidth(graphics, lines[^1] + "...", font, maxWidth);
        }

        return lines
            .Select(line => TrimToWidth(graphics, line, font, maxWidth))
            .ToArray();
    }

    private static string TrimToWidth(XGraphics graphics, string value, XFont font, double maxWidth)
    {
        var result = value;
        while (result.Length > 3 && graphics.MeasureString(result, font).Width > maxWidth)
        {
            result = result[..^4].TrimEnd() + "...";
        }

        return result;
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue && value.Value != DateTime.MinValue
            ? value.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"))
            : "-";

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR"));

    private static string EmptyAsDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static readonly Column[] Columns =
    [
        new("No", 25d, XStringFormats.Center),
        new("Stok Kodu", 66d, XStringFormats.CenterLeft),
        new("Urun", 212d, XStringFormats.CenterLeft),
        new("Siparis", 54d, XStringFormats.CenterRight),
        new("Birim", 45d, XStringFormats.Center),
        new("Teslim", 54d, XStringFormats.CenterRight),
        new("Kalan", 54d, XStringFormats.CenterRight)
    ];

    private sealed record Column(string Title, double Width, XStringFormat Alignment);

    private sealed class PageState(XGraphics graphics, double y, double contentBottom) : IDisposable
    {
        public XGraphics Graphics { get; } = graphics;
        public double Y { get; set; } = y;
        public double ContentBottom { get; } = contentBottom;

        public void Dispose() => Graphics.Dispose();
    }
}
