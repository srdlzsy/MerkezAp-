namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

public sealed record InventoryCountDetailDto(
    InventoryCountHeaderDto Header,
    IReadOnlyCollection<InventoryCountLineItemDto> Items);
