namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.PromosyonRaporlari;

public sealed record PromotionBulletinListRequest(
    int? WarehouseNo,
    DateTime? ActiveOn,
    bool OnlyActive,
    string? Search,
    int Take);

public sealed record PromotionPerformanceRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? PromotionCode,
    string? Search,
    int Take);

public sealed record PromotionBulletinListItemDto(
    string PromotionCode,
    string PromotionName,
    string PromotionType,
    string Description,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsPassive,
    bool IsActive,
    string CustomerCode,
    int? PluNo,
    int? ProductPluNo,
    double? LimitAmount,
    double? DiscountRate,
    double? DiscountAmount,
    IReadOnlyCollection<int> BranchNos);

public sealed record PromotionBulletinOptionDto(
    string PromotionCode,
    string PromotionName,
    string PromotionType,
    bool IsActive,
    DateTime? StartDate,
    DateTime? EndDate);

public sealed record PromotionPerformanceReportDto(
    DateTime StartDate,
    DateTime EndDate,
    int? WarehouseNo,
    int PromotionCount,
    int UsageCount,
    int ReceiptCount,
    double SoldQuantity,
    double NetSalesAmount,
    double GrossSalesAmount,
    double DiscountAmount,
    double EstimatedCostAmount,
    double MarginAmount,
    double MarginPercent,
    IReadOnlyCollection<PromotionPerformanceItemDto> Items);

public sealed record PromotionPerformanceItemDto(
    string PromotionCode,
    string PromotionName,
    string PromotionType,
    string Description,
    int UsageCount,
    int ReceiptCount,
    double SoldQuantity,
    double NetSalesAmount,
    double GrossSalesAmount,
    double DiscountAmount,
    double EstimatedCostAmount,
    double MarginAmount,
    double MarginPercent,
    double DiscountToGrossSalesPercent,
    DateTime? FirstSaleDate,
    DateTime? LastSaleDate);

public sealed record PromotionBranchPerformanceItemDto(
    int BranchNo,
    string BranchName,
    string PromotionCode,
    string PromotionName,
    string PromotionType,
    int UsageCount,
    int ReceiptCount,
    double SoldQuantity,
    double NetSalesAmount,
    double GrossSalesAmount,
    double DiscountAmount,
    double EstimatedCostAmount,
    double MarginAmount,
    double MarginPercent);
