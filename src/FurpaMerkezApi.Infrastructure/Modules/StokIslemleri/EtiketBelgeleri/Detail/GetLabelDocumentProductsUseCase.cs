using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Detail;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.EtiketBelgeleri.Detail;

public sealed class GetLabelDocumentProductsUseCase(
    LabelDocumentQueryExecutor labelDocumentQueryExecutor,
    LabelProductQueryExecutor labelProductQueryExecutor)
    : IGetLabelDocumentProductsUseCase
{
    public async Task<IReadOnlyCollection<LabelDocumentProductDto>> ExecuteAsync(
        LabelDocumentDetailRequest request,
        CancellationToken cancellationToken)
    {
        var productCodes = await labelDocumentQueryExecutor.GetDetailProductCodesAsync(request, cancellationToken);

        if (productCodes.Count == 0)
        {
            return Array.Empty<LabelDocumentProductDto>();
        }

        var productsByCode = await labelProductQueryExecutor.ExecuteAsync(
            request.WarehouseNo,
            productCodes,
            request.DocumentId,
            cancellationToken);

        return productCodes
            .Select(productCode =>
                productsByCode.TryGetValue(productCode, out var product)
                    ? product
                    : new LabelDocumentProductDto
                    {
                        ProductCode = productCode,
                        LastUpdateDate = DateTime.MinValue,
                        DocumentOrderNo = request.DocumentId
                    })
            .ToArray();
    }
}
