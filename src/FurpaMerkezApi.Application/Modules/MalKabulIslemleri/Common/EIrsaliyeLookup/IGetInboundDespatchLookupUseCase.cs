namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;

public interface IGetInboundDespatchLookupUseCase
{
    Task<InboundDespatchLookupResponse> ExecuteAsync(
        InboundDespatchLookupRequest request,
        CancellationToken cancellationToken);
}
