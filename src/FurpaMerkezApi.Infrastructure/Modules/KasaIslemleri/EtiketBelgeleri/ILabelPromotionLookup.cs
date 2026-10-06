using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public interface ILabelPromotionLookup
{
    Task<IReadOnlyDictionary<string, LabelPromotionDto>> GetActiveProductPromotionsAsync(
        int warehouseNo,
        IReadOnlyDictionary<string, double> pricesByStockCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetActiveProductCodesAsync(
        int warehouseNo,
        CancellationToken cancellationToken);
}
