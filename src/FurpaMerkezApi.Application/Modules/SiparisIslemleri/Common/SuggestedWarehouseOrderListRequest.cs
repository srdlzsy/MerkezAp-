namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record SuggestedWarehouseOrderListRequest(
    int TargetWarehouseNo,
    int SourceWarehouseNo,
    int LookbackDays = 43,
    int FallbackRecommendedDay = 7);
