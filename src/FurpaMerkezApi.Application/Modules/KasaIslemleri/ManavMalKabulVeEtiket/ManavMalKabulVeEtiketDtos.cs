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
    string SupplierName,
    string? SupplierTitle2 = null,
    string? SupplierTaxNo = null);

public sealed record ManavMalKabulVeEtiketStockSuggestionDto(
    string StockCode,
    string StockName,
    string Barcode,
    string? UnitName = null,
    string? ModelCode = null,
    int? WholesaleTaxPointer = null);

public sealed record ManavMalKabulVeEtiketIncomingInvoiceQuery(
    DateTime StartDate,
    DateTime EndDate,
    string? SupplierCode,
    string? SearchText,
    bool IncludeArchived,
    int Take);

public sealed record ManavMalKabulVeEtiketIncomingInvoiceDto(
    string DocumentId,
    string InvoiceId,
    string SupplierTitle,
    string SupplierTaxNo,
    DateTime? CreateDate,
    DateTime? InvoiceDate,
    string InvoiceType,
    decimal InvoiceTotal,
    decimal TaxExclusiveAmount,
    decimal TaxTotal,
    string DespatchId,
    bool IsProcessed,
    bool IsPrinted,
    bool IsStandard,
    string StatusCode,
    string Status,
    string Message,
    string DocumentCurrencyCode,
    decimal ExchangeRate,
    string OrderDocumentId,
    bool IsArchived,
    string InvoiceTipType,
    int InvoiceTipTypeCode,
    bool? IsSeen,
    DateTime LastSynchronizedAtUtc,
    string? MatchedSupplierCode,
    string? MatchedSupplierName,
    bool CanStartAcceptance);

public sealed record ManavMalKabulVeEtiketInvoiceDetailQuery(
    string InvoiceLookupId,
    string? SupplierCode);

public sealed record ManavMalKabulVeEtiketInvoiceDetailDto(
    string InvoiceLookupId,
    string InvoiceId,
    string DocumentId,
    string SupplierTitle,
    string SupplierTaxNo,
    DateTime? IssueDate,
    string InvoiceTypeCode,
    string DocumentCurrencyCode,
    decimal TaxExclusiveAmount,
    decimal TaxTotal,
    decimal PayableAmount,
    string? DespatchId,
    string? MatchedSupplierCode,
    string? MatchedSupplierName,
    bool CanStartAcceptance,
    IReadOnlyCollection<ManavMalKabulVeEtiketInvoiceLineDto> Lines,
    IReadOnlyCollection<string> Warnings);

public sealed record ManavMalKabulVeEtiketInvoiceLineDto(
    int LineNo,
    string LineId,
    string StockCode,
    string StockName,
    string Barcode,
    string UnitCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineAmount,
    decimal TaxRatePercent,
    decimal TaxAmount,
    int? TaxPointer,
    string? MatchedStockCode,
    string? MatchedStockName,
    string? MatchedBarcode,
    bool CanCreateAcceptance,
    IReadOnlyCollection<string> Warnings);

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
    decimal InvoiceDifference,
    string? SupplierCode = null,
    int LabelRowCount = 0,
    string? DocumentSeries = null,
    string? DocumentNo = null,
    string? SeriesAndNumber = null,
    int MicroRowCount = 0,
    decimal MicroAmount = 0,
    string? MicroDocument = null,
    string? Status = null,
    string? UnitName = null);

public sealed record ManavMalKabulVeEtiketDepotStockReportItemDto(
    string StockCode,
    string StockName,
    string Responsible,
    decimal CurrentStock,
    decimal PurchasePriceWithVat,
    decimal SalesPrice,
    string? Barcode = null,
    string? UnitName = null,
    string? ModelCode = null);

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
    IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptLineDto> Lines,
    string? DocumentNo = null,
    string? InvoiceGuid = null,
    string? OfflineTraceKey = null);

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
    int OutWarehouseNo,
    string? MovementGuid = null,
    string? Barcode = null,
    string? UnitName = null,
    string? Description = null);

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
