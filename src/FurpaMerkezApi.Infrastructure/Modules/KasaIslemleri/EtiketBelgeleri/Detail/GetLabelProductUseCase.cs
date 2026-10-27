using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Detail;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Detail;

public sealed class GetLabelProductUseCase(LabelProductQueryExecutor labelProductQueryExecutor)
    : IGetLabelProductUseCase
{
    public async Task<LabelDocumentProductDto?> ExecuteAsync(
        LabelProductDetailRequest request,
        CancellationToken cancellationToken)
    {
        var productCode = request.ProductCode.Trim();
        if (request.WarehouseNo <= 0 || productCode.Length == 0)
        {
            return null;
        }

        var products = await labelProductQueryExecutor.ExecuteAsync(
            request.WarehouseNo,
            [productCode],
            documentId: 0,
            cancellationToken);

        return products.TryGetValue(productCode, out var product) &&
               !string.IsNullOrWhiteSpace(product.ProductName)
            ? product
            : null;
    }
}
