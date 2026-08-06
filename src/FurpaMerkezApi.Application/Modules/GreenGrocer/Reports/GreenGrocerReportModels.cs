namespace FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;

public sealed record GreenGrocerReportDateRequest(
    DateTime Date,
    int? WarehouseNo = null,
    string? TypeCode = null,
    string? Search = null,
    bool IncludeLazyBranches = true,
    int Take = 1000);

public sealed record GreenGrocerDashboardDto(
    DateTime ReportDate,
    int? WarehouseNo,
    int BranchCount,
    int LazyBranchCount,
    int DocumentCount,
    int ProductCount,
    double TotalQuantity,
    GreenGrocerReportCaseInfoDto? CaseInfo,
    IReadOnlyCollection<GreenGrocerTypeSummaryDto> TypeSummaries,
    IReadOnlyCollection<GreenGrocerBranchSummaryDto> Branches,
    IReadOnlyCollection<GreenGrocerProductReportItemDto> TopProducts,
    IReadOnlyCollection<GreenGrocerLazyBranchDto> LazyBranches);

public sealed record GreenGrocerTypeSummaryDto(
    string TypeCode,
    string TypeName,
    int BranchCount,
    int DocumentCount,
    int ProductCount,
    double TotalQuantity,
    GreenGrocerReportCaseInfoDto? CaseInfo = null);

public sealed record GreenGrocerBranchSummaryDto(
    int BranchNo,
    string BranchName,
    GreenGrocerReportWarehouseDto Branch,
    int DocumentCount,
    int ProductCount,
    double TotalQuantity,
    GreenGrocerReportCaseInfoDto? CaseInfo = null);

public sealed record GreenGrocerBranchReportDto(
    IReadOnlyCollection<GreenGrocerBranchReportItemDto> Items,
    IReadOnlyCollection<GreenGrocerLazyBranchDto> LazyBranches);

public sealed record GreenGrocerBranchReportItemDto(
    DateTime OrderDate,
    int BranchNo,
    string BranchName,
    GreenGrocerReportWarehouseDto Branch,
    string DocumentSerie,
    int DocumentOrderNo,
    GreenGrocerReportDocumentDto Document,
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    string StockCode,
    string StockName,
    string UnitName,
    string PrimaryBarcode,
    string GlobalProductCode,
    GreenGrocerReportProductDto Product,
    double Quantity,
    DateTime LatestCreateDate,
    bool CanDelete,
    GreenGrocerReportCaseInfoDto? CaseInfo = null);

public sealed record GreenGrocerProductReportItemDto(
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    string StockCode,
    string StockName,
    string UnitName,
    string PrimaryBarcode,
    string GlobalProductCode,
    GreenGrocerReportProductDto Product,
    double Quantity,
    GreenGrocerReportCaseInfoDto? CaseInfo = null);

public sealed record GreenGrocerProductReportGroupDto(
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    string StockCode,
    string StockName,
    string UnitName,
    string PrimaryBarcode,
    string GlobalProductCode,
    GreenGrocerReportProductDto Product,
    double TotalQuantity,
    GreenGrocerReportCaseInfoDto? CaseInfo,
    IReadOnlyCollection<GreenGrocerProductBranchItemDto> Branches);

public sealed record GreenGrocerProductBranchItemDto(
    int BranchNo,
    string BranchName,
    GreenGrocerReportWarehouseDto Branch,
    string DocumentSerie,
    int DocumentOrderNo,
    GreenGrocerReportDocumentDto Document,
    double Quantity,
    DateTime LatestCreateDate,
    bool CanDelete,
    GreenGrocerReportCaseInfoDto? CaseInfo = null);

public sealed record GreenGrocerGreenReportItemDto(
    DateTime OrderDate,
    int BranchNo,
    string BranchName,
    GreenGrocerReportWarehouseDto Branch,
    string DocumentSerie,
    int DocumentOrderNo,
    GreenGrocerReportDocumentDto Document,
    int RowNo,
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    string StockCode,
    string StockName,
    string UnitName,
    string PrimaryBarcode,
    string GlobalProductCode,
    GreenGrocerReportProductDto Product,
    double Quantity,
    DateTime LatestCreateDate,
    bool CanDelete,
    GreenGrocerReportCaseInfoDto? CaseInfo = null);

public sealed record GreenGrocerReportProductDto(
    string StockCode,
    string ProductCode,
    string StockName,
    string ShortName,
    string DisplayName,
    string ProductName,
    string ModelCode,
    string ModelName,
    string UnitName,
    string GlobalProductCode,
    string PrimaryBarcode);

public sealed record GreenGrocerReportWarehouseDto(
    int WarehouseNo,
    string WarehouseName,
    string RegionCode);

public sealed record GreenGrocerReportDocumentDto(
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo);

public sealed record GreenGrocerReportCaseInfoDto(
    double InputQuantity,
    string InputMode,
    double EstimatedQuantity,
    string MicroUnit,
    double? AverageKgPerCase,
    double? UnitsPerCase,
    string AverageSource,
    string Confidence,
    int? AverageRecordCount,
    int? AverageCaseCount,
    double? CoefficientOfVariation);

public sealed record GreenGrocerLazyBranchDto(
    int BranchNo,
    string BranchName,
    GreenGrocerReportWarehouseDto Branch,
    string RegionCode);

public sealed record GreenGrocerTypeOptionDto(
    string TypeCode,
    string TypeName,
    bool IsGreens);

public sealed record DeleteGreenGrocerOrderRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    int? WarehouseNo);

public sealed record DeleteGreenGrocerOrderResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int? WarehouseNo,
    int DeletedLineCount,
    DateTime LatestCreateDate,
    DateTime DeletedAt);
