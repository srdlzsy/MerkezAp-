using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed record InvoiceSendingListRequest(
    DateTime StartDate,
    DateTime EndDate,
    InvoiceSendingScenario Scenario,
    int SentState);

public sealed record InvoiceSendingDocumentRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    InvoiceSendingScenario Scenario);

public sealed record InvoiceSendingRenderRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    InvoiceSendingScenario Scenario,
    InvoiceDocumentProfile Profile,
    bool? PreferEmbeddedXslt,
    bool FallbackToDefaultXslt);

public sealed record SendInvoiceDocumentsRequest(
    InvoiceSendingScenario Scenario,
    IReadOnlyCollection<SendInvoiceDocumentSelection> Documents);

public sealed record ValidateInvoiceDocumentsRequest(
    InvoiceSendingScenario Scenario,
    IReadOnlyCollection<SendInvoiceDocumentSelection> Documents);

public sealed record SendInvoiceDocumentSelection(
    string DocumentSerie,
    int DocumentOrderNo);

public sealed record InvoiceReturnReferenceCandidatesRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    InvoiceSendingScenario Scenario);

public sealed record UpdateInvoiceReturnReferenceRequest(
    string DocumentSerie,
    int DocumentOrderNo,
    InvoiceSendingScenario Scenario,
    string? SourceDocumentSerie,
    int? SourceDocumentOrderNo,
    bool UseFallbackWhenNotSelected);

public sealed record InvoiceSendingListResponse(
    int TotalCount,
    IReadOnlyCollection<InvoiceSendingListItemDto> Items);

public sealed record InvoiceSendingListItemDto(
    string DocumentSerie,
    int DocumentOrderNo,
    string InvoiceId,
    DateTime DocumentDate,
    string SentDocumentNo,
    bool IsSent,
    string CustomerCode,
    string CustomerTitle,
    string CustomerTcknVkn,
    string TargetAlias,
    string InvoiceProfileId,
    string InvoiceTypeCode,
    InvoiceSendingScenario Scenario,
    decimal LineExtensionTotal,
    decimal TaxTotal,
    decimal ChargeTotal,
    decimal PayableTotal,
    string ShipmentDocumentNo,
    DateTime? ShipmentDocumentDate,
    string ReturnInvoiceNo,
    DateTime? ReturnInvoiceDate,
    string WarehouseName,
    string Description,
    int SourceLineCount,
    string SourceLineSummary,
    string TaxRateSummary);

public sealed record InvoiceSendingDetailDto(
    InvoiceSendingListItemDto Summary,
    InvoiceRenderedDocumentDto Document);

public sealed record InvoiceSendingPdfResult(
    string InvoiceId,
    byte[] Content);

public sealed record SendInvoiceDocumentsResponse(
    InvoiceSendingScenario Scenario,
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<SendInvoiceDocumentResultDto> Items);

public sealed record ValidateInvoiceDocumentsResponse(
    InvoiceSendingScenario Scenario,
    int RequestedCount,
    int ValidCount,
    int InvalidCount,
    IReadOnlyCollection<ValidateInvoiceDocumentResultDto> Items);

public sealed record SendInvoiceDocumentResultDto(
    string DocumentSerie,
    int DocumentOrderNo,
    string InvoiceId,
    string CustomerCode,
    string CustomerTitle,
    bool IsSucceeded,
    string? ServiceDocumentId,
    string? ServiceDocumentNumber,
    string Message);

public sealed record ValidateInvoiceDocumentResultDto(
    string DocumentSerie,
    int DocumentOrderNo,
    string InvoiceId,
    string CustomerCode,
    string CustomerTitle,
    bool IsValid,
    string Message);

public sealed record InvoiceReturnReferenceCandidatesResponse(
    InvoiceSendingListItemDto Invoice,
    InvoiceReturnReferenceDto? CurrentReference,
    InvoiceReturnReferenceCandidateDto? FallbackReference,
    IReadOnlyCollection<InvoiceReturnReferenceCandidateDto> Candidates);

public sealed record UpdateInvoiceReturnReferenceResponse(
    InvoiceSendingListItemDto Invoice,
    InvoiceReturnReferenceDto Reference);

public sealed record InvoiceReturnReferenceDto(
    string InvoiceNo,
    DateTime? InvoiceDate,
    string Source);

public sealed record InvoiceReturnReferenceCandidateDto(
    string SourceDocumentSerie,
    int SourceDocumentOrderNo,
    string InvoiceNo,
    DateTime? InvoiceDate,
    DateTime? DocumentDate,
    DateTime CreatedAt,
    string CustomerCode,
    string CustomerTitle,
    decimal LineExtensionTotal,
    decimal TaxTotal,
    decimal PayableTotal,
    bool IsFallbackCandidate,
    bool IsCurrentReference,
    bool IsGeneratedInvoiceNo);

public enum InvoiceSendingScenario
{
    EFatura = 0,
    EArsiv = 1
}
