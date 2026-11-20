namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Models;

public sealed record YeniKasaAppSettingsDto(
    int Id,
    int PavoFis,
    string? FisGenislik,
    int? PrinterDotsPerLine,
    decimal? FisFaturaLimit,
    decimal? NakitLimit,
    string? FtpHost,
    int? FtpPort,
    string? FtpUsername,
    string? FtpPassword,
    string? FtpFolderPath,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpdateYeniKasaAppSettingsRequest(
    int PavoFis,
    string? FisGenislik,
    int? PrinterDotsPerLine,
    decimal? FisFaturaLimit,
    decimal? NakitLimit,
    string? FtpHost,
    string? FtpUsername,
    string? FtpPassword);

public sealed record TestFtpConnectionRequest(
    string FtpHost,
    int? FtpPort,
    string? FtpUsername,
    string? FtpPassword,
    string? FtpFolderPath);

public sealed record TestFtpConnectionResultDto(
    bool IsSuccess,
    long LatencyMs,
    string Message);

public sealed record SyncPushResultDto(
    bool IsSuccess,
    string Message,
    DateTime TriggeredAtUtc);

public sealed record YeniKasaPaymentMethodDto(
    int Id,
    string Name,
    string? Description,
    int Status,
    int Sirano,
    int PavoIsActive,
    string? PavoType,
    string? PavoMediator,
    string? Brand,
    string? Group);

public sealed record UpdateYeniKasaPaymentMethodRequest(
    int Id,
    string Name,
    string? Description,
    int Status,
    int Sirano,
    int PavoIsActive,
    string? PavoType,
    string? PavoMediator,
    string? Brand,
    string? Group);

public sealed record PaymentMethodOrderUpdateItem(
    int Id,
    int Sirano,
    int Status);

public sealed record UpdatePaymentMethodsOrderRequest(
    IReadOnlyList<PaymentMethodOrderUpdateItem> Items);

public sealed record YeniKasaPaymentLookupItemDto(
    string Code,
    string Label);

public sealed record YeniKasaPaymentMethodLookupsDto(
    IReadOnlyList<string> Groups,
    IReadOnlyList<YeniKasaPaymentLookupItemDto> Brands);

public sealed record YeniKasaRoleDto(
    int Id,
    string Name,
    string? Description);

public sealed record YeniKasaPermissionDto(
    int Id,
    string Name,
    string Code,
    string? Description);

public sealed record YeniKasaRolePermissionDto(
    int RoleId,
    int PermissionId);

public sealed record YeniKasaRolePermissionMatrixDto(
    IReadOnlyList<YeniKasaRoleDto> Roles,
    IReadOnlyList<YeniKasaPermissionDto> Permissions,
    IReadOnlyList<YeniKasaRolePermissionDto> RolePermissions);

public sealed record UpdateRolePermissionsRequest(
    int RoleId,
    IReadOnlyList<int> PermissionIds);

public sealed record BulkRolePermissionUpdateItem(
    int RoleId,
    IReadOnlyList<int> PermissionIds);

public sealed record BulkUpdateRolePermissionsRequest(
    IReadOnlyList<BulkRolePermissionUpdateItem> Roles);

public sealed record CopyRolePermissionsRequest(
    int SourceRoleId,
    int TargetRoleId);

public sealed record CopyRolePermissionsResultDto(
    bool IsSuccess,
    int CopiedPermissionCount,
    string Message);

public sealed record YeniKasaDisplaySettingsDto(
    int Id,
    string PageType,
    int IsTwoSlider,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpdateYeniKasaDisplaySettingsRequest(
    int Id,
    int IsTwoSlider);

public sealed record YeniKasaDisplayContentDto(
    int Id,
    string? Type,
    string? Name,
    int Sirano,
    DateTime? StartDate,
    DateTime? EndDate,
    string? PageType,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    string? ImageUrl);

public sealed record CreateYeniKasaDisplayContentRequest(
    string? Type,
    string? Name,
    int Sirano,
    DateTime? StartDate,
    DateTime? EndDate,
    string? PageType,
    string? Base64Data);

public sealed record DisplayContentOrderUpdateItem(
    int Id,
    int Sirano);

public sealed record UpdateDisplayContentsOrderRequest(
    IReadOnlyList<DisplayContentOrderUpdateItem> Items);

public sealed record YeniKasaFastAccessCategoryDto(
    int Id,
    string Name,
    string? Description,
    string CategoryKey,
    int ItemCount,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateFastAccessCategoryRequest(
    string Name,
    string? Description);

public sealed record UpdateFastAccessCategoryRequest(
    int Id,
    string Name,
    string? Description);

public sealed record YeniKasaFastAccessItemDto(
    int Id,
    string InventoryCode,
    string? ProductName,
    string? Barcode,
    string? ImagePath,
    string CategoryKey,
    string? ImageUrl,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateFastAccessItemRequest(
    string CategoryKey,
    string InventoryCode,
    string? Barcode,
    string? Base64Data);

public sealed record YeniKasaStockLookupDto(
    string StockCode,
    string StockName,
    string? Barcode,
    string? Unit,
    decimal? SalePrice);
