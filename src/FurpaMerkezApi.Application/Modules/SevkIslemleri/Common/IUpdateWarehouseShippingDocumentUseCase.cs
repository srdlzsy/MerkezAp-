namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public interface IUpdateWarehouseShippingDocumentUseCase
{
    Task<UpdateWarehouseShippingDocumentResponse> ExecuteAsync(
        UpdateWarehouseShippingDocumentRequest request,
        CancellationToken cancellationToken);

    Task<DeleteWarehouseShippingDocumentResponse> DeleteAsync(
        DeleteWarehouseShippingDocumentRequest request,
        CancellationToken cancellationToken);
}
