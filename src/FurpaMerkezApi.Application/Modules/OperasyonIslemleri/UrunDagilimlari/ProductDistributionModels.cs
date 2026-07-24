namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.UrunDagilimlari;

public interface IProductDistributionService
{
    Task<IReadOnlyCollection<ProductDistributionCenterDto>> GetDistributionCentersAsync(CancellationToken cancellationToken);

    Task<ProductDistributionProposalDto> CreateProposalAsync(ProductDistributionProposalRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProductDistributionListItemDto>> ListAsync(ProductDistributionListRequest request, CancellationToken cancellationToken);

    Task<ProductDistributionDetailDto> GetAsync(string documentNo, CancellationToken cancellationToken);

    Task<ProductDistributionDetailDto> SaveAsync(ProductDistributionSaveRequest request, CancellationToken cancellationToken);

    Task<ProductDistributionDetailDto> UpdateAsync(string documentNo, ProductDistributionSaveRequest request, CancellationToken cancellationToken);

    Task<ProductDistributionNotificationDto> NotifyAsync(string documentNo, ProductDistributionNotifyRequest request, CancellationToken cancellationToken);

    Task<ProductDistributionFinalizeDto> FinalizeAsync(string documentNo, ProductDistributionFinalizeRequest request, CancellationToken cancellationToken);

    Task<ProductDistributionDeleteDto> DeleteAsync(string documentNo, CancellationToken cancellationToken);
}

public sealed record ProductDistributionCenterDto(
    int WarehouseNo,
    string WarehouseName,
    string? RegionCode);

public sealed record ProductDistributionProposalRequest(
    string StockCode,
    int DistributionCenterWarehouseNo,
    int TotalCaseQuantity,
    int? SalesDayCount,
    DateTime? ReferenceDate,
    bool IncludeBranchesWithoutSales);

public sealed record ProductDistributionProposalDto(
    ProductDistributionStockDto Stock,
    ProductDistributionWarehouseDto DistributionCenter,
    ProductDistributionSummaryDto Summary,
    IReadOnlyCollection<ProductDistributionLineDto> Lines,
    IReadOnlyCollection<string> Warnings);

public sealed record ProductDistributionListRequest(
    int? Status,
    string? DocumentNo,
    string? StockCode,
    int? DistributionCenterWarehouseNo,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    int? Take);

public sealed record ProductDistributionListItemDto(
    string DocumentNo,
    ProductDistributionStatusDto Status,
    DateTime CreatedAt,
    DateTime? FinalizedAt,
    ProductDistributionStockDto Stock,
    ProductDistributionWarehouseDto DistributionCenter,
    string? DistributedBy,
    int LineCount,
    int TotalCaseQuantity,
    int TotalUnitQuantity);

public sealed record ProductDistributionSaveRequest(
    string StockCode,
    int DistributionCenterWarehouseNo,
    int TotalCaseQuantity,
    string? DistributedBy,
    IReadOnlyCollection<ProductDistributionSaveLineRequest> Lines);

public sealed record ProductDistributionSaveLineRequest(
    int WarehouseNo,
    int CaseQuantity,
    int? UnitQuantity,
    double? LastSalesQuantity,
    double? CompanyAverageDailySales,
    double? BranchAverageDailySales);

public sealed record ProductDistributionDetailDto(
    ProductDistributionHeaderDto Header,
    ProductDistributionSummaryDto Summary,
    IReadOnlyCollection<ProductDistributionLineDto> Lines,
    IReadOnlyCollection<ProductDistributionActionDto> AvailableActions);

public sealed record ProductDistributionHeaderDto(
    string DocumentNo,
    ProductDistributionStatusDto Status,
    DateTime CreatedAt,
    DateTime? FinalizedAt,
    ProductDistributionStockDto Stock,
    ProductDistributionWarehouseDto DistributionCenter,
    string? DistributedBy);

public sealed record ProductDistributionStockDto(
    string StockCode,
    string StockName,
    string? Barcode,
    int PackageFactor,
    string? UnitName);

public sealed record ProductDistributionWarehouseDto(
    int WarehouseNo,
    string WarehouseName,
    string? RegionCode);

public sealed record ProductDistributionLineDto(
    int WarehouseNo,
    string WarehouseName,
    string? RegionCode,
    double LastSalesQuantity,
    double CurrentStockQuantity,
    double CompanyAverageDailySales,
    double BranchAverageDailySales,
    int CaseQuantity,
    int UnitQuantity,
    string Reason);

public sealed record ProductDistributionSummaryDto(
    int SalesDayCount,
    DateTime ReferenceDate,
    int LineCount,
    int TotalCaseQuantity,
    int AllocatedCaseQuantity,
    int CaseDifference,
    int TotalUnitQuantity,
    bool IsBalanced,
    string Message);

public sealed record ProductDistributionStatusDto(
    int Code,
    string Name,
    string Severity);

public sealed record ProductDistributionActionDto(
    string Code,
    string Label,
    bool Enabled,
    string? Reason);

public sealed record ProductDistributionNotifyRequest(
    string? NotifyBy,
    bool MarkStockOrderingStopped);

public sealed record ProductDistributionNotificationDto(
    string DocumentNo,
    ProductDistributionStatusDto Status,
    bool StatusChanged,
    bool StockOrderingStopped,
    IReadOnlyCollection<ProductDistributionNotificationRecipientDto> Recipients,
    string Subject,
    string Message);

public sealed record ProductDistributionNotificationRecipientDto(
    string? RegionCode,
    string? ManagerName,
    string? Email,
    int LineCount,
    int TotalCaseQuantity,
    int TotalUnitQuantity);

public sealed record ProductDistributionFinalizeRequest(
    string? FinalizeBy,
    DateTime? OrderDate,
    DateTime? DeliveryDate,
    bool AllowFinalizeWithoutNotification);

public sealed record ProductDistributionFinalizeDto(
    string DocumentNo,
    ProductDistributionStatusDto Status,
    DateTime FinalizedAt,
    int CreatedDocumentCount,
    int ExistingDocumentCount,
    int TotalUnitQuantity,
    IReadOnlyCollection<ProductDistributionWarehouseOrderDto> Orders);

public sealed record ProductDistributionWarehouseOrderDto(
    string DocumentSerie,
    int DocumentOrderNo,
    int InWarehouseNo,
    string InWarehouseName,
    int OutWarehouseNo,
    string OutWarehouseName,
    int LineCount,
    int TotalUnitQuantity,
    bool AlreadyExisted);

public sealed record ProductDistributionDeleteDto(
    string DocumentNo,
    bool Deleted,
    string Message);
