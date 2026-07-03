namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.TedarikciPerformansKarnesi;

public sealed record SupplierPerformanceRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? CustomerCode,
    int Take);

public sealed record SupplierPerformanceDetailRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string CustomerCode,
    int EventTake);

public sealed record SupplierPerformanceReportDto(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    DateTime GeneratedAtUtc,
    SupplierPerformanceSummaryDto Summary,
    IReadOnlyCollection<SupplierPerformanceCardDto> Items);

public sealed record SupplierPerformanceSummaryDto(
    int SupplierCount,
    double AverageScore,
    int CriticalSupplierCount,
    int WarningSupplierCount,
    double TotalOrderedQuantity,
    double TotalReceivedQuantity,
    double TotalReturnedQuantity,
    double TotalMissingQuantity,
    double TotalExcessQuantity,
    double TotalOutageImpactQuantity,
    double TotalIssuedInvoiceAmount,
    double TotalIncomingInvoiceAmount,
    double TotalInvoiceDifferenceAmount,
    string InvoiceMetricsState);

public sealed record SupplierPerformanceCardDto(
    string CustomerCode,
    string CustomerTitle,
    string TaxNoOrTckn,
    double Score,
    string Grade,
    string RiskLevel,
    SupplierOrderPerformanceDto Orders,
    SupplierReceivingPerformanceDto Receiving,
    SupplierReturnPerformanceDto Returns,
    SupplierOutageImpactDto OutageImpact,
    SupplierInvoicePerformanceDto Invoices,
    SupplierPerformanceScoreBreakdownDto ScoreBreakdown);

public sealed record SupplierOrderPerformanceDto(
    int DocumentCount,
    int LineCount,
    double OrderedQuantity,
    double DeliveredQuantity,
    double RemainingQuantity,
    double DeliveryRate,
    int LateDeliveredLineCount,
    int OpenLateLineCount,
    double AverageLateDays);

public sealed record SupplierReceivingPerformanceDto(
    int DocumentCount,
    int LineCount,
    double ReceivedQuantity,
    double ReceivedAmount,
    int DifferenceLineCount,
    double MissingQuantity,
    double ExcessQuantity,
    double DifferenceRate);

public sealed record SupplierReturnPerformanceDto(
    int DocumentCount,
    int LineCount,
    double ReturnedQuantity,
    double ReturnedAmount,
    double ReturnRate);

public sealed record SupplierOutageImpactDto(
    int DocumentCount,
    int LineCount,
    double Quantity,
    double Amount,
    double QuantityRate,
    string Attribution);

public sealed record SupplierInvoicePerformanceDto(
    int IssuedInvoiceCount,
    double IssuedInvoiceAmount,
    int IncomingInvoiceCount,
    double IncomingInvoiceAmount,
    double InvoiceDifferenceAmount,
    double InvoiceDifferenceRate,
    string State,
    string Note);

public sealed record SupplierPerformanceScoreBreakdownDto(
    double DeliveryPenalty,
    double DifferencePenalty,
    double ReturnPenalty,
    double OutagePenalty,
    double InvoicePenalty,
    double TotalPenalty);

public sealed record SupplierPerformanceDetailDto(
    SupplierPerformanceCardDto Card,
    IReadOnlyCollection<SupplierPerformanceEventDto> Events);

public sealed record SupplierPerformanceEventDto(
    string Source,
    string EventType,
    DateTime? EventDate,
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    string StockCode,
    string StockName,
    int WarehouseNo,
    string WarehouseName,
    double Quantity,
    double RelatedQuantity,
    double Amount,
    string Description);
