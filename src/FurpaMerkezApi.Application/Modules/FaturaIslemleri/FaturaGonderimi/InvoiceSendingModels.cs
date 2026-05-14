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

public sealed record SendInvoiceDocumentSelection(
    string DocumentSerie,
    int DocumentOrderNo);

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
    string WarehouseName,
    string Description);

public sealed record InvoiceSendingDetailDto(
    InvoiceSendingListItemDto Summary,
    InvoiceRenderedDocumentDto Document);

public sealed record SendInvoiceDocumentsResponse(
    InvoiceSendingScenario Scenario,
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<SendInvoiceDocumentResultDto> Items);

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

public enum InvoiceSendingScenario
{
    EFatura = 0,
    EArsiv = 1
}
