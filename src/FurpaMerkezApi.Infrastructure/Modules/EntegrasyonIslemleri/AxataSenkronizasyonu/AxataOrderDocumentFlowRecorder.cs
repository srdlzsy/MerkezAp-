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
        var documentType = normalizedMovementType == "G02"
            ? DocumentFlowType.ReceivedWarehouseOrder
            : DocumentFlowType.IssuedWarehouseOrder;

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    documentType,
                    header.OutWarehouseNo,
                    header.DocumentSerie,
                    header.DocumentOrderNo),
                documentType,
                header.OutWarehouseNo,
                header.InWarehouseNo,
                header.DocumentSerie,
                header.DocumentOrderNo,
                DocumentFlowStep.AxataOrderSent,
                DocumentFlowStatus.Succeeded,
                $"Siparis AXATA'ya {normalizedMovementType} olarak gonderildi ve Mikro gonderim isareti dogrulandi.",
                ChangedByUserId: requestedByUserId == Guid.Empty ? null : requestedByUserId),
            cancellationToken);
    }

    public Task RecordCompanyOrderSentAsync(
        CompanyOrderDetailDto detail,
        string movementType,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var header = detail.Header;
        var normalizedMovementType = movementType.Trim().ToUpperInvariant();
        var documentType = normalizedMovementType == "C02"
            ? DocumentFlowType.ReceivedCompanyOrder
            : DocumentFlowType.IssuedCompanyOrder;

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    documentType,
                    header.WarehouseNo,
                    header.DocumentSerie,
                    header.DocumentOrderNo),
                documentType,
                header.WarehouseNo,
                null,
                header.DocumentSerie,
                header.DocumentOrderNo,
                DocumentFlowStep.AxataOrderSent,
                DocumentFlowStatus.Succeeded,
                $"Siparis AXATA'ya {normalizedMovementType} olarak gonderildi ve Mikro gonderim isareti dogrulandi.",
                ChangedByUserId: requestedByUserId == Guid.Empty ? null : requestedByUserId),
            cancellationToken);
    }
}
