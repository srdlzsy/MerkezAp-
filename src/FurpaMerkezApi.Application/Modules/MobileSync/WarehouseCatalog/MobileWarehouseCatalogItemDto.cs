namespace FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;

public sealed record MobileWarehouseCatalogItemDto(
    int WarehouseNo,
    string WarehouseName,
    int? CompanyNo,
    int? BranchNo,
    string GroupCode,
    byte? WarehouseType,
    string ResponsibilityCenterCode,
    string ProjectCode,
    string Address,
    string District,
    string Province,
    bool IsInventoryExcluded,
    bool IsDeleted,
    DateTime UpdatedAt);
