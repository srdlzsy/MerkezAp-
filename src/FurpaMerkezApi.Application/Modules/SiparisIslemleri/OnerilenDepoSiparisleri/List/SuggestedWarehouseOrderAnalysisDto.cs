using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;

public sealed record SuggestedWarehouseOrderExclusionSummaryDto(
    string Code,
    string Label,
    int Count);

public sealed record SuggestedWarehouseOrderAnalysisDto(
    int CandidateCount,
    int SuggestedCount,
    IReadOnlyCollection<SuggestedWarehouseOrderListItemDto> Items,
    IReadOnlyCollection<SuggestedWarehouseOrderExclusionSummaryDto> Exclusions);
