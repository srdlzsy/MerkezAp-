namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed record ManavCodeListItemDto(
    string ProductGroup,
    string ProductName,
    int Code,
    int? MnvsfCode,
    string StockCode,
    string? MnvsfStockCode,
    string CategoryCode,
    int MnvsfMatchCount);
