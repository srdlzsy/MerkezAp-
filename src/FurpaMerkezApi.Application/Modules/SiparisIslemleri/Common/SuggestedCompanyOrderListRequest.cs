namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record SuggestedCompanyOrderListRequest(
    int WarehouseNo,
    string SupplierCode,
    int LookbackDays = 43,
    int FallbackRecommendedDay = 7);
