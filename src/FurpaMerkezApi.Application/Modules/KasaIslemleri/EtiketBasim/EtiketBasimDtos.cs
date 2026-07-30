namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBasim;

public sealed record EtiketBasimReferenceSearchRequest(
    string? Query,
    int Take);

public sealed record EtiketBasimStockSearchRequest(
    string? Query,
    string? Prefix,
    int Take);

public sealed record EtiketBasimSupplierSuggestionDto(
    string SupplierCode,
    string SupplierName);

public sealed record EtiketBasimStockSuggestionDto(
    string StockCode,
    string StockName,
    string Barcode);

public sealed record EtiketBasimCalculationRequest(
    decimal GrossWeight,
    decimal CaseTare,
    int? CaseCount,
    decimal? PalletTare,
    string? StockBarcode);

public sealed record EtiketBasimCalculationDto(
    decimal CaseTotalTare,
    decimal NetReceivedWeight,
    decimal AverageCaseWeight,
    string? LabelBarcodeRaw,
    string? LabelBarcode,
    string BarcodeSymbology);

public sealed record SaveEtiketBasimAcceptanceRecordRequest(
    string SupplierCode,
    string SupplierName,
    string? DocumentSeries,
    string? DocumentNo,
    string StockCode,
    string StockName,
    string StockBarcode,
    decimal GrossWeight,
    decimal CaseTare,
    int? CaseCount,
    decimal? PalletTare,
    string ReceivedBy,
    string CaseType);

public sealed record EtiketBasimAcceptanceRecordDto(
    int Id,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string SupplierCode,
    string SupplierName,
    string DocumentSeries,
    string DocumentNo,
    string SeriesAndNumber,
    string StockCode,
    string StockName,
    string StockBarcode,
    decimal GrossWeight,
    decimal CaseTare,
    int CaseCount,
    decimal CaseTotalTare,
    decimal PalletTare,
    decimal AverageCaseWeight,
    decimal NetReceivedWeight,
    string ReceivedBy,
    bool MicroTransferred,
    string Status,
    string CaseType,
    string? LabelBarcodeRaw,
    string? LabelBarcode,
    string BarcodeSymbology);

public sealed record EtiketBasimLabelDto(
    int? RecordId,
    string StockCode,
    string StockName,
    string StockBarcode,
    string SupplierName,
    decimal AverageCaseWeight,
    DateTime LabelDate,
    int LabelCount,
    string LabelBarcodeRaw,
    string LabelBarcode,
    string BarcodeSymbology,
    decimal CaseTare,
    string CaseType);

public sealed record EtiketBasimReceivedProductReportItemDto(
    string SupplierName,
    string StockCode,
    string Barcode,
    string StockName,
    decimal GrossWeight,
    decimal CaseTotalTare,
    decimal PalletTare,
    int CaseCount,
    decimal NetReceivedWeight,
    decimal InvoiceQuantity,
    decimal InvoiceDifference);

public sealed record EtiketBasimDepotStockReportItemDto(
    string StockCode,
    string StockName,
    string Responsible,
    decimal CurrentStock,
    decimal PurchasePriceWithVat,
    decimal SalesPrice);

public sealed record EtiketBasimMicroTransferRequest(
    DateTime Date,
    string SupplierCode);

public sealed record EtiketBasimMicroTransferUnavailableDto(
    bool IsAvailable,
    string Message,
    string RequiredRule);