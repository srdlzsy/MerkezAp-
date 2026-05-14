namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record CompanyOrderDetailDto(
    CompanyOrderHeaderDto Header,
    IReadOnlyCollection<CompanyOrderLineItemDto> Items);
