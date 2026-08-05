namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;

public sealed record InboundDespatchLookupResponse(
    bool IsFound,
    int WarehouseNo,
    string ReceivingContext,
    string Ettn,
    string? DespatchNumber,
    DateTime? IssueDate,
    DateTime? ActualDespatchDate,
    TimeOnly? ActualDespatchTime,
    string? Plaque,
    string? DriverNameSurname,
    string? DriverTckn,
    string? ProfileId,
    string? DespatchAdviceTypeCode,
    IReadOnlyCollection<string> Notes,
    InboundDespatchPartyDto? Sender,
    InboundDespatchPartyDto? Receiver,
    InboundDespatchCustomerSuggestionDto? PrimaryCustomerSuggestion,
    int TotalLineCount,
    int MatchedLineCount,
    int UnmatchedLineCount,
    IReadOnlyCollection<InboundDespatchCustomerSuggestionDto> SuggestedCustomers,
    IReadOnlyCollection<InboundDespatchLineDto> Lines)
{
    public string SourceDocumentKind { get; init; } = "e-despatch";

    public string SourceDocumentLabel { get; init; } = "E-Irsaliye";

    public string? SourceDocumentNumber { get; init; }

    public string? InvoiceNumber { get; init; }

    public DateTime? InvoiceDate { get; init; }

    public decimal? InvoiceTotal { get; init; }

    public decimal? TaxExclusiveAmount { get; init; }

    public decimal? TaxTotal { get; init; }

    public string? CurrencyCode { get; init; }

    public IReadOnlyCollection<string> DespatchReferences { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record InboundDespatchPartyDto(
    string? Title,
    string? TaxNoOrTckn,
    string? Alias,
    string? City);

public sealed record InboundDespatchCustomerSuggestionDto(
    string CustomerCode,
    string CustomerName,
    string? TaxNoOrTckn,
    string MatchReason,
    bool IsPrimarySuggestion);

public sealed record InboundDespatchLineDto(
    int? LineNo,
    string? ProductName,
    string? Description,
    double Quantity,
    string? UnitCode,
    string? BuyerItemCode,
    string? SellerItemCode,
    string? ManufacturerItemCode,
    string? Barcode,
    string? InternalStockCode,
    string? InternalStockName,
    string? MatchReason,
    bool IsMatched,
    bool IsGoodsAcceptanceBlocked,
    bool CanUseForGoodsAcceptance)
{
    public double? UnitPrice { get; init; }

    public decimal? LineAmount { get; init; }

    public string? QuantitySource { get; init; }
}
