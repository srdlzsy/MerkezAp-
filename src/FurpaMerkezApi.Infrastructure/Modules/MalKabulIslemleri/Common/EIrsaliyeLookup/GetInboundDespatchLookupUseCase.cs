using System.Globalization;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;

public sealed class GetInboundDespatchLookupUseCase(
    MikroDbContext mikroDbContext,
    IUyumsoftConnectedQueryService uyumsoftConnectedQueryService)
    : IGetInboundDespatchLookupUseCase
{
    private const int MaxCustomerSuggestionCount = 10;

    public async Task<InboundDespatchLookupResponse> ExecuteAsync(
        InboundDespatchLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var ettn = NormalizeOrNull(request.Ettn)
            ?? throw new ArgumentException("ETTN is required.", nameof(request.Ettn));
        var receivingContext = NormalizeOrNull(request.ReceivingContext) ?? "mal-kabulu";
        var uyumsoftResponse = await uyumsoftConnectedQueryService.InvokeGetOperationAsync(
            UyumsoftConnectedServiceKind.EDespatch,
            new UyumsoftOperationInvocationRequest(
                "GetInboxDespatches",
                BuildInboxDespatchLookupParameters(ettn)),
            cancellationToken);

        var despatchAdvice = TryFindDespatchAdviceXml(uyumsoftResponse, out var despatchAdviceXml)
            ? XDocument.Parse(despatchAdviceXml, LoadOptions.PreserveWhitespace).Root
            : null;

        if (despatchAdvice is null)
        {
            return new InboundDespatchLookupResponse(
                false,
                request.WarehouseNo,
                receivingContext,
                ettn,
                null,
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                null,
                null,
                null,
                0,
                0,
                0,
                Array.Empty<InboundDespatchCustomerSuggestionDto>(),
                Array.Empty<InboundDespatchLineDto>());
        }

        var sender = ParseParty(
            FindChild(despatchAdvice, "DespatchSupplierParty") ??
            FindChild(despatchAdvice, "SellerSupplierParty"));
        var receiver = ParseParty(
            FindChild(despatchAdvice, "DeliveryCustomerParty") ??
            FindChild(despatchAdvice, "BuyerCustomerParty"));
        var notes = despatchAdvice.Elements()
            .Where(element => element.Name.LocalName == "Note")
            .Select(element => NormalizeOrNull(element.Value))
            .Where(note => note is not null)
            .Cast<string>()
            .ToArray();
        var lineDrafts = despatchAdvice.Elements()
            .Where(element => element.Name.LocalName == "DespatchLine")
            .Select(ParseLineDraft)
            .ToArray();
        var resolvedLines = await ResolveLinesAsync(lineDrafts, cancellationToken);
        var customerSuggestions = await ResolveCustomerSuggestionsAsync(sender, cancellationToken);
        var primaryCustomerSuggestion = customerSuggestions.FirstOrDefault();
        var matchedLineCount = resolvedLines.Count(line => line.IsMatched);

        return new InboundDespatchLookupResponse(
            true,
            request.WarehouseNo,
            receivingContext,
            NormalizeOrNull(GetPathValue(despatchAdvice, "UUID")) ?? ettn,
            NormalizeOrNull(GetPathValue(despatchAdvice, "ID")),
            ParseDateOrNull(GetPathValue(despatchAdvice, "IssueDate")),
            ParseDateOrNull(GetPathValue(despatchAdvice, "Shipment", "ActualDespatchDate")),
            NormalizeOrNull(GetPathValue(despatchAdvice, "ProfileID")),
            NormalizeOrNull(GetPathValue(despatchAdvice, "DespatchAdviceTypeCode")),
            notes,
            sender,
            receiver,
            primaryCustomerSuggestion,
            resolvedLines.Length,
            matchedLineCount,
            resolvedLines.Length - matchedLineCount,
            customerSuggestions,
            resolvedLines);
    }

    private async Task<InboundDespatchLineDto[]> ResolveLinesAsync(
        IReadOnlyCollection<LineDraft> lines,
        CancellationToken cancellationToken)
    {
        var barcodeCandidates = lines
            .Select(line => NormalizeOrNull(line.Barcode))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stockCodeCandidates = lines
            .SelectMany(line => new[]
            {
                NormalizeOrNull(line.BuyerItemCode),
                NormalizeOrNull(line.SellerItemCode),
                NormalizeOrNull(line.ManufacturerItemCode)
            })
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchingBarcodes = barcodeCandidates.Length == 0
            ? Array.Empty<BarcodeLookup>()
            : await mikroDbContext.BARKOD_TANIMLARIs
                .AsNoTracking()
                .Where(row => row.bar_kodu != null && barcodeCandidates.Contains(row.bar_kodu))
                .Select(row => new BarcodeLookup(
                    row.bar_kodu ?? string.Empty,
                    row.bar_stokkodu))
                .ToArrayAsync(cancellationToken);

        var allStockCodes = stockCodeCandidates
            .Concat(matchingBarcodes
                .Select(row => NormalizeOrNull(row.StockCode))
                .Where(value => value is not null)
                .Cast<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stocks = allStockCodes.Length == 0 && barcodeCandidates.Length == 0
            ? Array.Empty<StockLookup>()
            : await mikroDbContext.STOKLARs
                .AsNoTracking()
                .Where(stock =>
                    allStockCodes.Contains(stock.sto_kod) ||
                    (stock.sto_kuresel_urun_numarasi != null && barcodeCandidates.Contains(stock.sto_kuresel_urun_numarasi)))
                .Select(stock => new StockLookup(
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_kuresel_urun_numarasi,
                    stock.sto_malkabul_dursun))
                .ToArrayAsync(cancellationToken);

        var stocksByCode = stocks.ToDictionary(stock => stock.StockCode, StringComparer.OrdinalIgnoreCase);
        var stocksByGlobalTradeItemNo = stocks
            .Where(stock => !string.IsNullOrWhiteSpace(stock.GlobalTradeItemNo))
            .GroupBy(stock => stock.GlobalTradeItemNo!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var barcodeRowsByBarcode = matchingBarcodes
            .GroupBy(row => row.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return lines
            .Select(line => BuildResolvedLine(
                line,
                stocksByCode,
                stocksByGlobalTradeItemNo,
                barcodeRowsByBarcode))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<InboundDespatchCustomerSuggestionDto>> ResolveCustomerSuggestionsAsync(
        InboundDespatchPartyDto? sender,
        CancellationToken cancellationToken)
    {
        var taxNoOrTckn = NormalizeDigits(sender?.TaxNoOrTckn);
        var title = NormalizeOrNull(sender?.Title);

        if (taxNoOrTckn is null && title is null)
        {
            return Array.Empty<InboundDespatchCustomerSuggestionDto>();
        }

        var titlePattern = title is null ? null : $"%{title}%";
        var customers = await mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer =>
                customer.cari_kod != null &&
                customer.cari_kod != string.Empty &&
                ((taxNoOrTckn != null && customer.cari_VergiKimlikNo == taxNoOrTckn) ||
                 (titlePattern != null &&
                  (EF.Functions.Like(customer.cari_unvan1 ?? string.Empty, titlePattern) ||
                   EF.Functions.Like(customer.cari_unvan2 ?? string.Empty, titlePattern)))))
            .Select(customer => new CustomerLookup(
                customer.cari_kod ?? string.Empty,
                customer.cari_unvan1,
                customer.cari_VergiKimlikNo))
            .ToListAsync(cancellationToken);

        var orderedSuggestions = customers
            .Select(customer => new
            {
                Customer = customer,
                MatchReason = DetermineCustomerMatchReason(customer, taxNoOrTckn, title),
                Rank = DetermineCustomerMatchRank(customer, taxNoOrTckn, title)
            })
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Customer.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .Take(MaxCustomerSuggestionCount)
            .ToArray();

        var suggestions = orderedSuggestions
            .Select((item, index) => new InboundDespatchCustomerSuggestionDto(
                item.Customer.CustomerCode,
                NormalizeOrNull(item.Customer.CustomerName) ?? item.Customer.CustomerCode,
                NormalizeOrNull(item.Customer.TaxNoOrTckn),
                item.MatchReason,
                index == 0))
            .ToArray();

        return suggestions;
    }

    private static InboundDespatchLineDto BuildResolvedLine(
        LineDraft line,
        IReadOnlyDictionary<string, StockLookup> stocksByCode,
        IReadOnlyDictionary<string, StockLookup> stocksByGlobalTradeItemNo,
        IReadOnlyDictionary<string, BarcodeLookup> barcodeRowsByBarcode)
    {
        StockLookup? matchedStock = null;
        string? matchReason = null;
        var buyerItemCode = NormalizeOrNull(line.BuyerItemCode);
        var sellerItemCode = NormalizeOrNull(line.SellerItemCode);
        var manufacturerItemCode = NormalizeOrNull(line.ManufacturerItemCode);
        var barcode = NormalizeOrNull(line.Barcode);

        if (buyerItemCode is not null && stocksByCode.TryGetValue(buyerItemCode, out matchedStock))
        {
            matchReason = "buyer-item-code";
        }
        else if (sellerItemCode is not null && stocksByCode.TryGetValue(sellerItemCode, out matchedStock))
        {
            matchReason = "seller-item-code";
        }
        else if (manufacturerItemCode is not null && stocksByCode.TryGetValue(manufacturerItemCode, out matchedStock))
        {
            matchReason = "manufacturer-item-code";
        }
        else if (barcode is not null &&
                 barcodeRowsByBarcode.TryGetValue(barcode, out var barcodeRow) &&
                 NormalizeOrNull(barcodeRow.StockCode) is { } barcodeStockCode &&
                 stocksByCode.TryGetValue(barcodeStockCode, out matchedStock))
        {
            matchReason = "barcode";
        }
        else if (barcode is not null && stocksByGlobalTradeItemNo.TryGetValue(barcode, out matchedStock))
        {
            matchReason = "gtin";
        }

        var isGoodsAcceptanceBlocked = matchedStock?.GoodsAcceptanceBlockCode.GetValueOrDefault() != 0;

        return new InboundDespatchLineDto(
            line.LineNo,
            NormalizeOrNull(line.ProductName),
            NormalizeOrNull(line.Description),
            line.Quantity,
            NormalizeOrNull(line.UnitCode),
            buyerItemCode,
            sellerItemCode,
            manufacturerItemCode,
            barcode,
            matchedStock?.StockCode,
            NormalizeOrNull(matchedStock?.StockName),
            matchReason,
            matchedStock is not null,
            isGoodsAcceptanceBlocked,
            matchedStock is not null && !isGoodsAcceptanceBlocked);
    }

    private static LineDraft ParseLineDraft(XElement lineElement)
    {
        var itemElement = FindChild(lineElement, "Item");
        var quantityElement = FindChild(lineElement, "DeliveredQuantity") ??
                              FindChild(lineElement, "OutstandingQuantity");

        return new LineDraft(
            ParseIntOrNull(GetPathValue(lineElement, "ID")),
            GetPathValue(itemElement, "Name"),
            string.Join(
                " | ",
                itemElement?.Elements()
                    .Where(element => element.Name.LocalName == "Description")
                    .Select(element => NormalizeOrNull(element.Value))
                    .Where(value => value is not null)
                    .Cast<string>() ?? Array.Empty<string>()),
            ParseDoubleOrDefault(quantityElement?.Value),
            quantityElement?.Attribute("unitCode")?.Value,
            GetPathValue(itemElement, "BuyersItemIdentification", "ID"),
            GetPathValue(itemElement, "SellersItemIdentification", "ID"),
            GetPathValue(itemElement, "ManufacturersItemIdentification", "ID"),
            GetPathValue(itemElement, "StandardItemIdentification", "ID"));
    }

    private static InboundDespatchPartyDto? ParseParty(XElement? wrapperElement)
    {
        if (wrapperElement is null)
        {
            return null;
        }

        var partyElement = FindChild(wrapperElement, "Party") ?? wrapperElement;
        var title = GetPathValue(partyElement, "PartyName", "Name") ??
                    GetPathValue(partyElement, "PartyLegalEntity", "RegistrationName");

        if (title is null)
        {
            var firstName = GetPathValue(partyElement, "Person", "FirstName");
            var familyName = GetPathValue(partyElement, "Person", "FamilyName");
            title = NormalizeOrNull($"{firstName} {familyName}");
        }

        var taxNoOrTckn = GetPathValue(partyElement, "PartyIdentification", "ID") ??
                          GetPathValue(partyElement, "PartyTaxScheme", "CompanyID");

        return new InboundDespatchPartyDto(
            NormalizeOrNull(title),
            NormalizeDigits(taxNoOrTckn) ?? NormalizeOrNull(taxNoOrTckn),
            NormalizeOrNull(GetPathValue(partyElement, "EndpointID")),
            NormalizeOrNull(GetPathValue(partyElement, "PostalAddress", "CityName")));
    }

    private static IReadOnlyCollection<UyumsoftOperationParameterRequest> BuildInboxDespatchLookupParameters(string ettn) =>
    [
        new("PageIndex", "0"),
        new("PageSize", "1"),
        new("SetTaken", "false"),
        new("OnlyNewestDespatches", "true"),
        new("DespatchIds", ettn)
    ];

    private static bool TryFindDespatchAdviceXml(
        UyumsoftOperationResponseDto response,
        out string despatchAdviceXml)
    {
        foreach (var value in response.Nodes.SelectMany(FlattenNodeValues))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.Trim();
            if (trimmed.Contains("<DespatchAdvice", StringComparison.OrdinalIgnoreCase))
            {
                despatchAdviceXml = trimmed;
                return true;
            }
        }

        despatchAdviceXml = string.Empty;
        return false;
    }

    private static IEnumerable<string?> FlattenNodeValues(UyumsoftResponseNodeDto node)
    {
        yield return node.Value;

        foreach (var child in node.Children)
        {
            foreach (var value in FlattenNodeValues(child))
            {
                yield return value;
            }
        }
    }

    private static string DetermineCustomerMatchReason(
        CustomerLookup customer,
        string? taxNoOrTckn,
        string? title)
    {
        if (taxNoOrTckn is not null && string.Equals(customer.TaxNoOrTckn, taxNoOrTckn, StringComparison.OrdinalIgnoreCase))
        {
            return "vkn-tckn";
        }

        if (title is not null && string.Equals(customer.CustomerName, title, StringComparison.OrdinalIgnoreCase))
        {
            return "unvan-tam";
        }

        return "unvan-benzer";
    }

    private static int DetermineCustomerMatchRank(
        CustomerLookup customer,
        string? taxNoOrTckn,
        string? title)
    {
        if (taxNoOrTckn is not null && string.Equals(customer.TaxNoOrTckn, taxNoOrTckn, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (title is not null && string.Equals(customer.CustomerName, title, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static XElement? FindChild(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string? GetPathValue(XElement? parent, params string[] localNames)
    {
        var current = parent;

        foreach (var localName in localNames)
        {
            current = FindChild(current, localName);
            if (current is null)
            {
                return null;
            }
        }

        return NormalizeOrNull(current!.Value);
    }

    private static DateTime? ParseDateOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? ParseIntOrNull(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static double ParseDoubleOrDefault(string? value) =>
        double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;

    private static string? NormalizeDigits(string? value)
    {
        var normalized = NormalizeOrNull(value);
        if (normalized is null)
        {
            return null;
        }

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record BarcodeLookup(
        string Barcode,
        string? StockCode);

    private sealed record StockLookup(
        string StockCode,
        string? StockName,
        string? GlobalTradeItemNo,
        byte? GoodsAcceptanceBlockCode);

    private sealed record CustomerLookup(
        string CustomerCode,
        string? CustomerName,
        string? TaxNoOrTckn);

    private sealed record LineDraft(
        int? LineNo,
        string? ProductName,
        string? Description,
        double Quantity,
        string? UnitCode,
        string? BuyerItemCode,
        string? SellerItemCode,
        string? ManufacturerItemCode,
        string? Barcode);
}
