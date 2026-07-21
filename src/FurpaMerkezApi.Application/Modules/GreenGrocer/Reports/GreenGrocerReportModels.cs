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
    double TotalQuantity);

public sealed record GreenGrocerBranchSummaryDto(
    int BranchNo,
    string BranchName,
    int DocumentCount,
    int ProductCount,
    double TotalQuantity);

public sealed record GreenGrocerBranchReportDto(
    IReadOnlyCollection<GreenGrocerBranchReportItemDto> Items,
    IReadOnlyCollection<GreenGrocerLazyBranchDto> LazyBranches);

public sealed record GreenGrocerBranchReportItemDto(
    DateTime OrderDate,
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    double Quantity,
    DateTime LatestCreateDate,
    bool CanDelete);

public sealed record GreenGrocerProductReportItemDto(
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    double Quantity);

public sealed record GreenGrocerProductReportGroupDto(
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    double TotalQuantity,
    IReadOnlyCollection<GreenGrocerProductBranchItemDto> Branches);

public sealed record GreenGrocerProductBranchItemDto(
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    double Quantity,
    DateTime LatestCreateDate,
    bool CanDelete);

public sealed record GreenGrocerGreenReportItemDto(
    DateTime OrderDate,
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    int RowNo,
    string TypeCode,
    string TypeName,
    string ProductCode,
    string ProductName,
    double Quantity,
    DateTime LatestCreateDate,
    bool CanDelete);

public sealed record GreenGrocerLazyBranchDto(
    int BranchNo,
    string BranchName,
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
