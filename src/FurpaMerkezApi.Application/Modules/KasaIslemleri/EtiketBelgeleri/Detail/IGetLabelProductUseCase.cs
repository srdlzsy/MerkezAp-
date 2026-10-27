using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Detail;

public interface IGetLabelProductUseCase
{
    Task<LabelDocumentProductDto?> ExecuteAsync(
        LabelProductDetailRequest request,
        CancellationToken cancellationToken);
}
