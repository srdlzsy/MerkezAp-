using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Print;

public interface IGetReceivedWarehouseOrdersForPrintUseCase
{
    Task<IReadOnlyCollection<WarehouseOrderDetailDto>> ExecuteAsync(
        IReadOnlyCollection<WarehouseOrderDetailRequest> requests,
        CancellationToken cancellationToken);
}
