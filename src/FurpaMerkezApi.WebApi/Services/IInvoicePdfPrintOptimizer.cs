namespace FurpaMerkezApi.WebApi.Services;

public interface IInvoicePdfPrintOptimizer
{
    byte[] OptimizeForPrinting(byte[] pdfBytes);
}
