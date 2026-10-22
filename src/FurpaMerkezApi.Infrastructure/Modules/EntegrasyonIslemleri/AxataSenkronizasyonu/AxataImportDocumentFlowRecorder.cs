using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataImportDocumentFlowRecorder(IDocumentFlowService documentFlowService)
{
    public Task RecordAsync(
        string taskCode,
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        int? targetWarehouseNo,
        string documentSerie,
        int documentOrderNo,
        string axataDocumentNo,
        string message,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        if (sourceWarehouseNo <= 0 ||
            documentOrderNo <= 0 ||
            string.IsNullOrWhiteSpace(documentSerie))
        {
            return Task.CompletedTask;
        }

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    documentType,
                    sourceWarehouseNo,
                    documentSerie,
                    documentOrderNo),
                documentType,
                sourceWarehouseNo,
                targetWarehouseNo,
                documentSerie,
                documentOrderNo,
                DocumentFlowStep.AxataDocumentImported,
                DocumentFlowStatus.Succeeded,
                $"AXATA {taskCode}: {message}",
                ChangedByUserId: requestedByUserId == Guid.Empty ? null : requestedByUserId,
                ExternalDocumentNo: axataDocumentNo),
            cancellationToken);
    }
}
