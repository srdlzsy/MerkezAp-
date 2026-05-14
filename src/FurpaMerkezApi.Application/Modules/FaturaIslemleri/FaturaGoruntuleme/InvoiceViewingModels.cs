using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed record InvoiceViewingListRequest(
    DateTime StartDate,
    DateTime EndDate,
    bool? IsProcessed,
    bool? IsPrinted,
    InvoiceViewingSearchField? SearchField,
    string? SearchText,
    int PageNumber,
    int PageSize);

public sealed record InvoiceViewingSynchronizationRequest(
    DateTime StartDate,
    DateTime EndDate);

public sealed record InvoiceViewingDetailRequest(string DocumentId);

public sealed record InvoiceViewingRenderRequest(
    string DocumentId,
    InvoiceDocumentProfile Profile,
    bool? PreferEmbeddedXslt,
    bool FallbackToDefaultXslt);

public sealed record InvoiceViewingPrintedStateRequest(
    string DocumentId,
    bool IsPrinted,
    string Source);

public sealed record InvoiceViewingListResponse(
    int TotalCount,
    int PageNumber,
    int PageSize,
    IReadOnlyCollection<InvoiceViewingListItemDto> Items);

public sealed record InvoiceViewingListItemDto(
    string DocumentId,
    string InvoiceId,
    string CustomerTitle,
    string CustomerTcknVkn,
    DateTime? CreateDate,
    DateTime? InvoiceDate,
    string InvoiceType,
    decimal InvoiceTotal,
    string DespatchId,
    bool IsProcessed,
    bool IsPrinted,
    bool IsStandard,
    string StatusCode,
    string Status);

public sealed record InvoiceViewingDetailDto(
    InvoiceViewingListItemDto Summary,
    InvoiceRenderedDocumentDto Document);

public sealed record InvoiceViewingPrintedStateResponse(
    InvoiceViewingListItemDto Summary,
    string Source);

public enum InvoiceViewingSearchField
{
    InvoiceDate = 0,
    InvoiceId = 1,
    CustomerTitle = 2,
    CustomerTcknVkn = 3,
    InvoiceTotal = 4,
    DespatchId = 5
}
