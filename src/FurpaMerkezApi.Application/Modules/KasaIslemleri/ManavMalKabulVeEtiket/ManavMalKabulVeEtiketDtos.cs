namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed record ManavMalKabulVeEtiketReferenceSearchRequest(
    string? Query,
    int Take);

public sealed record ManavMalKabulVeEtiketStockSearchRequest(
    string? Query,
    string? Prefix,
    int Take);

public sealed record ManavMalKabulVeEtiketSupplierSuggestionDto(
    string SupplierCode,
    string SupplierName);

public sealed record ManavMalKabulVeEtiketStockSuggestionDto(
    string StockCode,
    string StockName,
    string Barcode);

public sealed record ManavMalKabulVeEtiketCalculationRequest(
    decimal GrossWeight,
    decimal CaseTare,
    int? CaseCount,
    decimal? PalletTare,
    string? StockBarcode);

public sealed record ManavMalKabulVeEtiketCalculationDto(
    decimal CaseTotalTare,
    decimal NetReceivedWeight,
    decimal AverageCaseWeight,
    string? LabelBarcodeRaw,
    string? LabelBarcode,
    string BarcodeSymbology);

public sealed record SaveManavMalKabulVeEtiketAcceptanceRecordRequest(
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

public sealed record ManavMalKabulVeEtiketAcceptanceRecordDto(
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

public sealed record ManavMalKabulVeEtiketLabelDto(
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

public sealed record ManavMalKabulVeEtiketReceivedProductReportItemDto(
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

public sealed record ManavMalKabulVeEtiketDepotStockReportItemDto(
    string StockCode,
    string StockName,
    string Responsible,
    decimal CurrentStock,
    decimal PurchasePriceWithVat,
    decimal SalesPrice);

public sealed record ManavMalKabulVeEtiketMicroGoodsReceiptQuery(
    DateTime Date,
    string? SupplierCode);

public sealed record ManavMalKabulVeEtiketMicroGoodsReceiptDocumentDto(
    DateTime Date,
    string DocumentSeries,
    int DocumentOrderNo,
    string SeriesAndNumber,
    string SupplierCode,
    string SupplierName,
    int CreateUserNo,
    int LineCount,
    decimal TotalQuantity,
    decimal TotalAmount,
    decimal TotalTax,
    DateTime FirstCreatedAt,
    DateTime LastCreatedAt,
    IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptLineDto> Lines);

public sealed record ManavMalKabulVeEtiketMicroGoodsReceiptLineDto(
    int LineNo,
    string StockCode,
    string StockName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    decimal TaxAmount,
    int TaxPointer,
    int InWarehouseNo,
    int OutWarehouseNo);

public sealed record ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto(
    DateTime Date,
    string SupplierCode,
    string SupplierName,
    string StockCode,
    string StockName,
    int LabelRowCount,
    decimal LabelNetWeight,
    int MicroRowCount,
    decimal MicroQuantity,
    decimal Difference,
    decimal MicroAmount,
    string MicroDocument,
    string Status);

public sealed record ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest(
    DateTime Date,
    string SupplierCode,
    string? DocumentSeries,
    int? DocumentOrderNo,
    string? DocumentNo,
    int? MikroUserNo,
    string? Description,
    bool MarkAcceptanceRecordsTransferred,
    IReadOnlyCollection<ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest> Lines);

public sealed record ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest(
    int? AcceptanceRecordId,
    string StockCode,
    decimal Quantity,
    decimal UnitPrice,
    int UnitPointer,
    int? TaxPointer,
    decimal? TaxRatePercent,
    decimal? TaxAmount,
    string? Description);

public sealed record ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto(
    DateTime Date,
    string DocumentSeries,
    int DocumentOrderNo,
    string SeriesAndNumber,
    string SupplierCode,
    int CreateUserNo,
    int LineCount,
    decimal TotalQuantity,
    decimal TotalAmount,
    decimal TotalTax,
    int UpdatedAcceptanceRecordCount,
    string OfflineTraceKey,
    IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptLineDto> Lines);

public sealed record ManavMalKabulVeEtiketMicroTransferRequest(
    DateTime Date,
    string SupplierCode);

public sealed record ManavMalKabulVeEtiketMicroTransferUnavailableDto(
    bool IsAvailable,
    string Message,
    string RequiredRule);
