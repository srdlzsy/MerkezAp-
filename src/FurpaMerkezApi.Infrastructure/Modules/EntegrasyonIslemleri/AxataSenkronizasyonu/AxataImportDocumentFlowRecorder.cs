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
        CancellationToken cancellationToken,
        string? documentNo = null,
        int? flowDocumentOrderNo = null)
    {
        if (sourceWarehouseNo <= 0 ||
            documentOrderNo <= 0 ||
            string.IsNullOrWhiteSpace(documentSerie))
        {
            return Task.CompletedTask;
        }

        var normalizedDocumentNo = string.IsNullOrWhiteSpace(documentNo) ? null : documentNo.Trim();
        var baseFlowKey = DocumentFlowKeys.Create(
            documentType,
            sourceWarehouseNo,
            documentSerie,
            flowDocumentOrderNo ?? documentOrderNo);
        var flowKey = normalizedDocumentNo is null
            ? baseFlowKey
            : $"{baseFlowKey}:{normalizedDocumentNo}";

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                flowKey,
                documentType,
                sourceWarehouseNo,
                targetWarehouseNo,
                documentSerie,
                documentOrderNo,
                DocumentFlowStep.AxataDocumentImported,
                DocumentFlowStatus.Succeeded,
                $"AXATA {taskCode}: {message}",
                ChangedByUserId: requestedByUserId == Guid.Empty ? null : requestedByUserId,
                DocumentNo: normalizedDocumentNo,
                ExternalDocumentNo: axataDocumentNo),
            cancellationToken);
    }

    public Task RecordFailureAsync(
        string taskCode,
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        int? targetWarehouseNo,
        string documentSerie,
        int documentOrderNo,
        string axataDocumentNo,
        string errorMessage,
        Guid requestedByUserId,
        CancellationToken cancellationToken,
        string? documentNo = null)
    {
        if (sourceWarehouseNo <= 0 ||
            documentOrderNo <= 0 ||
            string.IsNullOrWhiteSpace(documentSerie))
        {
            return Task.CompletedTask;
        }

        var normalizedDocumentNo = string.IsNullOrWhiteSpace(documentNo) ? null : documentNo.Trim();
        var baseFlowKey = DocumentFlowKeys.Create(
            documentType,
            sourceWarehouseNo,
            documentSerie,
            documentOrderNo);
        var flowKey = normalizedDocumentNo is null
            ? baseFlowKey
            : $"{baseFlowKey}:{normalizedDocumentNo}";

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                flowKey,
                documentType,
                sourceWarehouseNo,
                targetWarehouseNo,
                documentSerie,
                documentOrderNo,
                DocumentFlowStep.AxataDocumentImported,
                DocumentFlowStatus.Failed,
                $"AXATA {taskCode}: Belge aktarilamadi.",
                Error: errorMessage,
                ChangedByUserId: requestedByUserId == Guid.Empty ? null : requestedByUserId,
                DocumentNo: normalizedDocumentNo,
                ExternalDocumentNo: axataDocumentNo),
            cancellationToken);
    }
}
