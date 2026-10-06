using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public interface ILabelPromotionLookup
{
    Task<IReadOnlyDictionary<int, LabelPromotionDto>> GetActiveCardPromotionsAsync(
        int warehouseNo,
        IReadOnlyDictionary<int, double> pricesByPlu,
        CancellationToken cancellationToken);
}
