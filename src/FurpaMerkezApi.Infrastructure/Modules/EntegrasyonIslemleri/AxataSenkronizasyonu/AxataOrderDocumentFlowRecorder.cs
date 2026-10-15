using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataOrderDocumentFlowRecorder(IDocumentFlowService documentFlowService)
{
    public Task RecordWarehouseOrderSentAsync(
        WarehouseOrderDetailDto detail,
        string movementType,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var header = detail.Header;
        var normalizedMovementType = movementType.Trim().ToUpperInvariant();

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    DocumentFlowType.IssuedWarehouseOrder,
                    header.InWarehouseNo,
                    header.DocumentSerie,
                    header.DocumentOrderNo),
                DocumentFlowType.IssuedWarehouseOrder,
                header.InWarehouseNo,
                header.OutWarehouseNo,
                header.DocumentSerie,
                header.DocumentOrderNo,
                DocumentFlowStep.AxataOrderSent,
                DocumentFlowStatus.Succeeded,
                $"Siparis AXATA'ya {normalizedMovementType} olarak gonderildi ve Mikro gonderim isareti dogrulandi.",
                ChangedByUserId: requestedByUserId == Guid.Empty ? null : requestedByUserId),
            cancellationToken);
    }
}
