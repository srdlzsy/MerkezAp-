namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record WarehouseShippingDetailDto(
    WarehouseShippingHeaderDto Header,
    IReadOnlyCollection<WarehouseShippingLineItemDto> Items);
