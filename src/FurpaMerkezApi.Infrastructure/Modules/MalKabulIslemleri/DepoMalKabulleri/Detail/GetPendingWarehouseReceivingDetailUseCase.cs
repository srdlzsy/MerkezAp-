using System.Globalization;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;

public sealed class GetPendingWarehouseReceivingDetailUseCase(WarehouseShippingDetailQueryExecutor queryExecutor)
    : IGetPendingWarehouseReceivingDetailUseCase
{
    public async Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await queryExecutor.ExecutePendingIncomingAsync(request, cancellationToken);
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);
        var sortedItems = detail.Items
            .OrderBy(item => item.StockName, comparer)
            .ThenBy(item => item.LineNo)
            .ToArray();

        return detail with { Items = sortedItems };
    }
}
