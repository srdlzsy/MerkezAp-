namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public sealed record WarehouseOrderDetailDto(
    WarehouseOrderHeaderDto Header,
    IReadOnlyCollection<WarehouseOrderLineItemDto> Items);
