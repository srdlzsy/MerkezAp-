namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;

public interface IUyumsoftConnectedQueryService
{
    Task<UyumsoftConnectedServiceOverviewDto> GetOverviewAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UyumsoftOperationDefinitionDto>> GetOperationsAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken);

    Task<UyumsoftOperationResponseDto> InvokeGetOperationAsync(
        UyumsoftConnectedServiceKind serviceKind,
        UyumsoftOperationInvocationRequest request,
        CancellationToken cancellationToken);

    Task<byte[]> GetInboxInvoicePdfFileAsync(
        string invoiceId,
        CancellationToken cancellationToken);

    Task<byte[]> GetOutboxInvoicePdfFileAsync(
        string invoiceId,
        CancellationToken cancellationToken);
}
