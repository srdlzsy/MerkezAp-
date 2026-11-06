namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed record KasaHareketBranchDto(
    int BranchNo,
    string BranchName,
    string Region);

public sealed record KasaHareketCashRegisterDto(
    int BranchNo,
    int CashRegisterNo,
    byte CashRegisterType,
    string CashRegisterTypeName,
    string CashRegisterTypeDescription);

public sealed record KasaHareketImportRequest(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyCollection<int>? Branches,
    IReadOnlyCollection<int>? CashRegisters,
    string? FileRootPath,
    bool SkipExisting,
    bool DryRun);

public sealed record KasaHareketScheduledImportRequest(
    DateTime? Date,
    int? AddDay,
    string? FileRootPath,
    bool SkipExisting,
    bool DryRun);

public sealed record KasaHareketDeleteStagingRequest(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo);

public sealed record KasaHareketMikroTransferRequest(
    DateTime Date,
    int? BranchNo,
    DateTime? EndDate = null);

public sealed record KasaHareketMikroTransferRangeRequest(
    DateTime StartDate,
    DateTime EndDate);

public sealed record KasaHareketTransferStatusRequest(
    DateTime StartDate,
    DateTime EndDate,
    int? BranchNo,
    int? CashRegisterNo,
    IReadOnlyCollection<int>? CashRegisterNos = null);

public sealed record KasaHareketReportRequest(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo);

public sealed record KasaHareketSalesComparisonRequest(
    DateTime Date,
    int? BranchNo);

public sealed record KasaHareketCashSummaryComparisonRequest(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo,
    decimal Tolerance);

public sealed record KasaHareketDetailRequest(
    DateTime Date,
    int BranchNo,
    int CashRegisterNo,
    int ReceiptTake);

public sealed record KasaHareketImportResultDto(
    string RunId,
    string ImportType,
    string Status,
    int ProcessedFiles,
    int ProcessedInvoices,
    int SkippedExistingInvoices,
    int InsertedLines,
    int InsertedPayments,
    int InsertedPromotions,
    IReadOnlyCollection<KasaHareketImportIssueDto> Warnings,
    IReadOnlyCollection<KasaHareketImportIssueDto> Errors);

public sealed record KasaHareketImportIssueDto(
    int? BranchNo,
    int? CashRegisterNo,
    string? File,
    string? ReceiptNo,
    int? LineNo,
    string Message);

public sealed record KasaHareketProcedureResultDto(
    string Procedure,
    string Message,
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo);

public sealed record KasaHareketTransferStatusDto(
    DateTime StartDate,
    DateTime EndDate,
    int? BranchNo,
    int? CashRegisterNo,
    int TotalReceiptCount,
    int TransferredReceiptCount,
    int PendingReceiptCount,
    int CanceledReceiptCount,
    DateTime GeneratedAtUtc);

public sealed record KasaHareketReportRowDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    decimal NetAmount,
    decimal Expense,
    decimal CheckAmount,
    decimal Difference);

public sealed record KasaHareketReportSummaryDto(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo,
    int RowCount,
    decimal TotalNetAmount,
    decimal TotalExpense,
    decimal TotalCheckAmount,
    decimal TotalDifference);

public sealed record KasaHareketSalesComparisonDto(
    DateTime Date,
    int? BranchNo,
    decimal Tolerance,
    KasaHareketSalesComparisonSummaryDto Summary,
    IReadOnlyCollection<KasaHareketSalesComparisonRowDto> Rows);

public sealed record KasaHareketSalesComparisonSummaryDto(
    int RowCount,
    int DifferenceCount,
    decimal TotalStockMovementAmount,
    decimal TotalCurrentAccountAmount,
    decimal TotalTransferredAmount,
    decimal TotalExpenseReceiptAmount,
    decimal TotalZReportAmount,
    decimal TotalZReportTransferDifference,
    decimal TotalTransferredTurnoverDifference,
    decimal TotalCurrentStockDifference,
    decimal TotalShortage,
    decimal TotalExcess,
    decimal TotalRetailStockAmount,
    decimal TotalRetailTransferredAmount,
    decimal TotalRetailDifference,
    decimal TotalInvoiceStockAmount,
    decimal TotalInvoiceTransferredAmount,
    decimal TotalInvoiceDifference,
    decimal TotalExpenseSlipStockAmount,
    decimal TotalExpenseSlipTransferredAmount,
    decimal TotalExpenseSlipDifference,
    decimal TotalNetStockTurnover,
    decimal TotalNetTransferredTurnover,
    decimal TotalNetDifference);

public sealed record KasaHareketSalesComparisonRowDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    decimal StockMovementAmount,
    decimal CurrentAccountAmount,
    decimal TransferredAmount,
    decimal ExpenseExcludedAmount,
    decimal ExpenseReceiptAmount,
    decimal ZReportAmount,
    decimal ZReportTransferDifference,
    decimal TransferredTurnoverDifference,
    decimal CurrentStockDifference,
    bool HasDifference,
    decimal RetailStockAmount,
    decimal RetailTransferredAmount,
    decimal RetailDifference,
    decimal InvoiceStockAmount,
    decimal InvoiceTransferredAmount,
    decimal InvoiceDifference,
    decimal ExpenseSlipStockAmount,
    decimal ExpenseSlipTransferredAmount,
    decimal ExpenseSlipDifference,
    decimal NetStockTurnover,
    decimal NetTransferredTurnover,
    decimal NetDifference);

public sealed record KasaHareketCashSummaryComparisonDto(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo,
    decimal Tolerance,
    KasaHareketCashSummaryComparisonSummaryDto Summary,
    IReadOnlyCollection<KasaHareketCashSummaryComparisonRowDto> Rows);

public sealed record KasaHareketCashSummaryComparisonSummaryDto(
    int RowCount,
    int BalancedCount,
    int DifferenceCount,
    int MissingCashSummaryCount,
    int MissingMovementCount,
    decimal TotalMovementZReportAmount,
    decimal TotalCashSummaryAmount,
    decimal TotalDifferenceAmount);

public sealed record KasaHareketCashSummaryComparisonRowDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    decimal MovementNetAmount,
    decimal MovementExpense,
    decimal MovementCheckAmount,
    decimal MovementZReportAmount,
    decimal CashSummaryAmount,
    int CashSummaryDocumentCount,
    decimal DifferenceAmount,
    string Status,
    string StatusName);

public sealed record KasaHareketDetailDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    KasaHareketReportRowDto? MovementReport,
    KasaHareketCashSummaryComparisonRowDto Comparison,
    KasaHareketDetailSummaryDto Summary,
    IReadOnlyCollection<KasaHareketCashierSummaryDto> CashierSummaries,
    IReadOnlyCollection<KasaHareketPaymentSummaryDto> MovementPaymentSummaries,
    IReadOnlyCollection<KasaHareketCashSummaryPaymentDto> CashSummaryPayments,
    IReadOnlyCollection<KasaHareketCashSummaryDocumentDto> CashSummaryDocuments,
    IReadOnlyCollection<KasaHareketReceiptDto> Receipts,
    IReadOnlyCollection<KasaHareketReceiptDto> CanceledReceipts);

public sealed record KasaHareketDetailSummaryDto(
    int ReceiptCount,
    int ReturnedReceiptCount,
    int CanceledReceiptCount,
    int MovementLineCount,
    int MovementPaymentCount,
    int CashSummaryDocumentCount,
    int CashSummaryPaymentCount,
    decimal MovementNetAmount,
    decimal MovementExpense,
    decimal MovementCheckAmount,
    decimal MovementZReportAmount,
    decimal CashSummaryAmount,
    decimal DifferenceAmount,
    int? MinReceiptNo,
    int? MaxReceiptNo,
    IReadOnlyCollection<int> MissingReceiptNos);

public sealed record KasaHareketCashierSummaryDto(
    string CashierCode,
    string CashierName,
    int ReceiptCount,
    int LineCount,
    decimal NetAmount,
    decimal Expense,
    decimal CheckAmount,
    decimal ZReportAmount);

public sealed record KasaHareketPaymentSummaryDto(
    int PaymentType,
    string PaymentTypeName,
    int PaymentCount,
    decimal Amount);

public sealed record KasaHareketCashSummaryPaymentDto(
    int PaymentTypeId,
    string PaymentTypeName,
    string AccountCode,
    int SlipCount,
    decimal Amount,
    bool IsIncludedInComparison);

public sealed record KasaHareketCashSummaryDocumentDto(
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    int CashNo,
    int ZReportNo,
    int CashierNo,
    string CashierName,
    int ManagerNo,
    string ManagerName,
    DateTime SummaryDate,
    decimal TotalAmount,
    int PaymentLineCount,
    DateTime CreateDate);

public sealed record KasaHareketReceiptDto(
    Guid InvoiceGuid,
    DateTime Date,
    TimeSpan Time,
    int BranchNo,
    int CashRegisterNo,
    int ReceiptNo,
    string ZNo,
    byte DocumentKind,
    string DocumentKindName,
    string CashierCode,
    string CashierName,
    string CardNumber,
    string CustomerCurrentCode,
    decimal GrossAmount,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal ExpenseAmount,
    decimal CheckAmount,
    decimal ZReportAmount,
    int LineCount,
    int PaymentCount,
    int PromotionCount,
    string FiscalMemoryCode,
    string ProcessResult,
    byte CancelReason,
    string CancelReasonName);

public sealed record KasaHareketSatisMutabakatRequest(
    DateTime Date,
    int? BranchNo);

public sealed record KasaHareketSatisMutabakatReportDto(
    DateTime Date,
    int? BranchNo,
    KasaHareketSatisMutabakatSummaryDto Summary,
    IReadOnlyCollection<KasaHareketSatisMutabakatRowDto> Rows);

public sealed record KasaHareketSatisMutabakatSummaryDto(
    int TotalBranches,
    int BalancedCount,
    int DifferenceCount,
    int DuplicateTransferCount,
    int MissingTransferCount,
    decimal TotalAraTabloTutar,
    decimal TotalGiderPusulasiTutar,
    decimal TotalGPSHaricTutar,
    decimal TotalAktarilanTutar,
    decimal TotalNakit,
    decimal TotalKrediKarti,
    decimal TotalYemekKarti,
    decimal TotalHediyeKarti,
    decimal TotalZRaporuTutari,
    decimal TotalGercekKasaFarki);

public sealed record KasaHareketSatisMutabakatRowDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    decimal AraTabloTutar,
    decimal GiderPusulasiTutar,
    decimal GPSHaricTutar,
    decimal AktarilanTutar,
    decimal Nakit,
    decimal KrediKarti,
    decimal YemekKarti,
    decimal HediyeKarti,
    decimal ZRaporuTutari,
    decimal GercekKasaFarki,
    string Durum,
    string FarkSebebi);

// --- Hareket Dosyası Kopyalama ve Görüntüleme Modülü DTO'ları ---
public sealed record KasaHareketDosyaBilgiRequest(
    int BranchNo,
    int CashRegisterNo,
    DateTime? Date);

public sealed record KasaHareketDosyaBilgiDto(
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    string FormattedKasaNo,
    DateTime Date,
    string FileName,
    string BranchIpAddress,
    string PosGenelFolderPath,
    string SourceFilePath,
    bool SourceFileExists,
    long? SourceFileSizeBytes,
    DateTime? SourceFileLastModified,
    string CentralFolderPath,
    string DestinationFilePath,
    bool DestinationFileExists,
    long? DestinationFileSizeBytes,
    DateTime? DestinationFileLastModified);

public sealed record KasaHareketDosyaKopyalaRequest(
    int BranchNo,
    int CashRegisterNo,
    DateTime? Date,
    bool Overwrite = true);

public sealed record KasaHareketDosyaKopyalaResultDto(
    bool Success,
    string Message,
    string SourceFilePath,
    string DestinationFilePath,
    long BytesCopied,
    long ElapsedMilliseconds);

public sealed record KasaHareketDosyaIcerikRequest(
    int BranchNo,
    int CashRegisterNo,
    DateTime? Date,
    string? TargetLocation = "destination",
    int? MaxLines = null);

public sealed record KasaHareketDosyaIcerikDto(
    string FileName,
    string FilePath,
    string ReadFrom,
    int TotalLines,
    int DisplayedLines,
    IReadOnlyCollection<string> Lines);

// --- Kasa Bazlı Z Raporu Detayı (FormDetay.cs) ---
public sealed record KasaHareketKasaZDetayRequest(
    DateTime Date,
    int BranchNo);

public sealed record KasaHareketKasaZDetayReportDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    decimal TotalAmount,
    IReadOnlyCollection<KasaHareketKasaZDetayRowDto> Rows);

public sealed record KasaHareketKasaZDetayRowDto(
    int BranchNo,
    string KasaNo,
    decimal ZTutari,
    string DocumentSeries,
    int DocumentSequence,
    string Description);

// --- Birlik Kart / Hediye Kartı Raporu (FrmBirlikKart.cs) ---
public sealed record KasaHareketBirlikKartRequest(
    DateTime Date,
    int? BranchNo);

public sealed record KasaHareketBirlikKartReportDto(
    DateTime Date,
    int? BranchNo,
    decimal TotalAmount,
    int TotalCount,
    IReadOnlyCollection<KasaHareketBirlikKartRowDto> Rows);

public sealed record KasaHareketBirlikKartRowDto(
    int BranchNo,
    string BranchName,
    string KasaMaliBellekKodu,
    int CashRegisterNo,
    decimal Tutar,
    string CekNumarasi);

// --- Belge / Fiş İptal Raporu & Fiş Detayı (FrmBelgeIptal.cs & FrmFis.cs) ---
public sealed record KasaHareketFisIptalRequest(
    DateTime StartDate,
    DateTime EndDate,
    int? BranchNo);

public sealed record KasaHareketFisIptalReportDto(
    DateTime StartDate,
    DateTime EndDate,
    int? BranchNo,
    decimal TotalAmount,
    int TotalCount,
    IReadOnlyCollection<KasaHareketFisIptalRowDto> Rows);

public sealed record KasaHareketFisIptalRowDto(
    string RegionCode,
    int BranchNo,
    string BranchName,
    DateTime Tarih,
    string Saat,
    int ZNo,
    int FisNo,
    int KasaNo,
    decimal Tutar,
    string CashierName,
    int CashierCode,
    Guid FaturaGuid);

public sealed record KasaHareketFisIptalDetailDto(
    Guid FaturaGuid,
    DateTime Tarih,
    string Saat,
    int BranchNo,
    string BranchName,
    int KasaNo,
    string CashierName,
    decimal ToplamTutar,
    IReadOnlyCollection<KasaHareketFisIptalLineDto> Lines);

public sealed record KasaHareketFisIptalLineDto(
    string Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

// --- Gider Pusulası Raporu & Detayı (FrmGiderPusulasi.cs & FrmGPS.cs) ---
public sealed record KasaHareketGiderPusulasiRequest(
    DateTime StartDate,
    DateTime EndDate,
    int? BranchNo);

public sealed record KasaHareketGiderPusulasiReportDto(
    DateTime StartDate,
    DateTime EndDate,
    int? BranchNo,
    decimal TotalAmount,
    int TotalCount,
    IReadOnlyCollection<KasaHareketGiderPusulasiRowDto> Rows);

public sealed record KasaHareketGiderPusulasiRowDto(
    int BranchNo,
    string BranchName,
    DateTime Tarih,
    string Saat,
    int FisNo,
    int KasaNo,
    int CashierCode,
    string CashierName,
    decimal Tutar,
    Guid FaturaGuid);

public sealed record KasaHareketGiderPusulasiDetailDto(
    Guid FaturaGuid,
    DateTime Tarih,
    string Saat,
    int BranchNo,
    string BranchName,
    int KasaNo,
    string CashierName,
    decimal ToplamTutar,
    IReadOnlyCollection<KasaHareketGiderPusulasiLineDto> Lines);

public sealed record KasaHareketGiderPusulasiLineDto(
    string ProductCode,
    string Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

// --- Trendyol Fiş Raporu (FrmTrendyol.cs) ---
public sealed record KasaHareketTrendyolRequest(
    DateTime Date,
    int? BranchNo);

public sealed record KasaHareketTrendyolReportDto(
    DateTime Date,
    int? BranchNo,
    decimal TotalAmount,
    int TotalCount,
    IReadOnlyCollection<KasaHareketTrendyolRowDto> Rows);

public sealed record KasaHareketTrendyolRowDto(
    int BranchNo,
    string BranchName,
    DateTime Tarih,
    int FisNo,
    int KasaNo,
    string CashierName,
    decimal Tutar);

// --- Nakit Raporu (FrmNakitRapor.cs) ---
public sealed record KasaHareketNakitRequest(
    DateTime Date);

public sealed record KasaHareketNakitReportDto(
    DateTime Date,
    decimal TotalAmount,
    int TotalCount,
    IReadOnlyCollection<KasaHareketNakitRowDto> Rows);

public sealed record KasaHareketNakitRowDto(
    int BranchNo,
    string BranchName,
    decimal NakitTutar,
    string Description);

