namespace FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;

public sealed record GreenGrocerReportDateRequest(DateTime Date);

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
    string ProductCode,
    string ProductName,
    double Quantity);

public sealed record GreenGrocerProductReportItemDto(
    string TypeCode,
    string ProductCode,
    string ProductName,
    double Quantity);

public sealed record GreenGrocerProductReportGroupDto(
    string TypeCode,
    string ProductCode,
    string ProductName,
    double TotalQuantity,
    IReadOnlyCollection<GreenGrocerProductBranchItemDto> Branches);

public sealed record GreenGrocerProductBranchItemDto(
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    double Quantity);

public sealed record GreenGrocerGreenReportItemDto(
    DateTime OrderDate,
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    int RowNo,
    string TypeCode,
    string ProductCode,
    string ProductName,
    double Quantity);

public sealed record GreenGrocerLazyBranchDto(
    int BranchNo,
    string BranchName,
    string RegionCode);

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
