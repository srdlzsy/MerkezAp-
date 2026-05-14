namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

public sealed record StockReceiptDetailDto(
    StockReceiptHeaderDto Header,
    IReadOnlyCollection<StockReceiptLineItemDto> Items);
