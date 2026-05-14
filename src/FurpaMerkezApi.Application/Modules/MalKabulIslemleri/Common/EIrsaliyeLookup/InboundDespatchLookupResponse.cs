namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;

public sealed record InboundDespatchLookupResponse(
    bool IsFound,
    int WarehouseNo,
    string ReceivingContext,
    string Ettn,
    string? DespatchNumber,
    DateTime? IssueDate,
    DateTime? ActualDespatchDate,
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
    IReadOnlyCollection<InboundDespatchLineDto> Lines);

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
    bool CanUseForGoodsAcceptance);
