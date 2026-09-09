using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.WebApi.Services;

public interface IReceivedWarehouseOrderPdfRenderer
{
    byte[] Render(IReadOnlyCollection<WarehouseOrderDetailDto> documents);
}
