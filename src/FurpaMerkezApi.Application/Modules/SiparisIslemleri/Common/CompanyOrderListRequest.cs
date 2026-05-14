namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record CompanyOrderListRequest(
    int WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? CustomerCode = null,
    bool OnlyOpen = false);
