namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;

public sealed record WarehouseLookupItemDto(
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
    bool IsInventoryExcluded);
