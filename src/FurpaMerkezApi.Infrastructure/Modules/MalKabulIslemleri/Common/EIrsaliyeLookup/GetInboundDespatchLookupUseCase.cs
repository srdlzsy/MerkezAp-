using System.Globalization;
using System.Net;
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
    private const string AutoDocumentKind = "auto";
    private const string EDespatchDocumentKind = "e-despatch";
    private const string EInvoiceDocumentKind = "e-invoice";
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

        return NormalizeDocumentKind(request.DocumentKind) switch
        {
            EDespatchDocumentKind => await ResolveDespatchAsync(request.WarehouseNo, receivingContext, ettn, cancellationToken)
                                     ?? CreateNotFoundResponse(
                                         request.WarehouseNo,
                                         receivingContext,
                                         ettn,
                                         EDespatchDocumentKind,
                                         ["Uyumsoft gelen e-irsaliye kutusunda belge bulunamadi."]),
            EInvoiceDocumentKind => await ResolveInvoiceAsync(request.WarehouseNo, receivingContext, ettn, cancellationToken)
                                    ?? CreateNotFoundResponse(
                                        request.WarehouseNo,
                                        receivingContext,
                                        ettn,
                                        EInvoiceDocumentKind,
                                        ["Uyumsoft gelen e-fatura kutusunda belge bulunamadi."]),
            _ => await ResolveDespatchAsync(request.WarehouseNo, receivingContext, ettn, cancellationToken)
                 ?? await ResolveInvoiceAsync(request.WarehouseNo, receivingContext, ettn, cancellationToken)
                 ?? CreateNotFoundResponse(
                     request.WarehouseNo,
                     receivingContext,
                     ettn,
                     AutoDocumentKind,
                     ["Uyumsoft gelen e-irsaliye ve e-fatura kutusunda belge bulunamadi."])
        };
    }

    private async Task<InboundDespatchLookupResponse?> ResolveDespatchAsync(
        int warehouseNo,
        string receivingContext,
        string ettn,
        CancellationToken cancellationToken)
    {
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
            return null;
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
        var actualDespatchDate = ParseDateOrNull(GetFirstPathValue(
            despatchAdvice,
            ["Shipment", "ActualDespatchDate"],
            ["Shipment", "Delivery", "Despatch", "ActualDespatchDate"]));
        var actualDespatchTime = ParseTimeOrNull(GetFirstPathValue(
            despatchAdvice,
            ["Shipment", "ActualDespatchTime"],
            ["Shipment", "Delivery", "Despatch", "ActualDespatchTime"]));
        var plaque = NormalizeOrNull(GetPathValue(
            despatchAdvice,
            "Shipment",
            "ShipmentStage",
            "TransportMeans",
            "RoadTransport",
            "LicensePlateID"));
        var driverNameSurname = JoinNonEmpty(
            GetPathValue(despatchAdvice, "Shipment", "ShipmentStage", "DriverPerson", "FirstName"),
            GetPathValue(despatchAdvice, "Shipment", "ShipmentStage", "DriverPerson", "FamilyName"));
        var driverTcknRaw = GetPathValue(
            despatchAdvice,
            "Shipment",
            "ShipmentStage",
            "DriverPerson",
            "NationalityID");
        var despatchNumber = NormalizeOrNull(GetPathValue(despatchAdvice, "ID"));

        return new InboundDespatchLookupResponse(
            true,
            warehouseNo,
            receivingContext,
            NormalizeOrNull(GetPathValue(despatchAdvice, "UUID")) ?? ettn,
            despatchNumber,
            ParseDateOrNull(GetPathValue(despatchAdvice, "IssueDate")),
            actualDespatchDate,
            actualDespatchTime,
            plaque,
            driverNameSurname,
            NormalizeDigits(driverTcknRaw) ?? NormalizeOrNull(driverTcknRaw),
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
            resolvedLines)
        {
            SourceDocumentKind = EDespatchDocumentKind,
            SourceDocumentLabel = "E-Irsaliye",
            SourceDocumentNumber = despatchNumber
        };
    }

    private async Task<InboundDespatchLookupResponse?> ResolveInvoiceAsync(
        int warehouseNo,
        string receivingContext,
        string ettn,
        CancellationToken cancellationToken)
    {
        var invoice = await TryGetInvoiceRootAsync(ettn, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var sender = ParseParty(FindChild(invoice, "AccountingSupplierParty"));
        var receiver = ParseParty(FindChild(invoice, "AccountingCustomerParty"));
        var notes = invoice.Elements()
            .Where(element => element.Name.LocalName == "Note")
            .Select(element => NormalizeOrNull(element.Value))
            .Where(note => note is not null)
            .Cast<string>()
            .ToArray();
        var lineDrafts = invoice.Elements()
            .Where(element => element.Name.LocalName == "InvoiceLine")
            .Select(ParseInvoiceLineDraft)
            .ToArray();
        var resolvedLines = await ResolveLinesAsync(lineDrafts, cancellationToken);
        var customerSuggestions = await ResolveCustomerSuggestionsAsync(sender, cancellationToken);
        var primaryCustomerSuggestion = customerSuggestions.FirstOrDefault();
        var matchedLineCount = resolvedLines.Count(line => line.IsMatched);
        var invoiceNumber = NormalizeOrNull(GetPathValue(invoice, "ID"));
        var issueDate = ParseDateOrNull(GetPathValue(invoice, "IssueDate"));
        var despatchReferences = invoice.Elements()
            .Where(element => element.Name.LocalName == "DespatchDocumentReference")
            .Select(element => NormalizeOrNull(GetPathValue(element, "ID")))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warnings = despatchReferences.Length == 0
            ? new[] { "Belge e-fatura olarak bulundu; e-faturada irsaliye referansi yok." }
            : new[]
            {
                "Belge e-fatura olarak bulundu.",
                $"E-fatura irsaliye referansi iceriyor: {string.Join(", ", despatchReferences)}"
            };

        return new InboundDespatchLookupResponse(
            true,
            warehouseNo,
            receivingContext,
            NormalizeOrNull(GetPathValue(invoice, "UUID")) ?? ettn,
            invoiceNumber,
            issueDate,
            null,
            null,
            null,
            null,
            null,
            NormalizeOrNull(GetPathValue(invoice, "ProfileID")),
            NormalizeOrNull(GetPathValue(invoice, "InvoiceTypeCode")),
            notes,
            sender,
            receiver,
            primaryCustomerSuggestion,
            resolvedLines.Length,
            matchedLineCount,
            resolvedLines.Length - matchedLineCount,
            customerSuggestions,
            resolvedLines)
        {
            SourceDocumentKind = EInvoiceDocumentKind,
            SourceDocumentLabel = "E-Fatura",
            SourceDocumentNumber = invoiceNumber,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = issueDate,
            InvoiceTotal = ParseDecimalOrNull(GetPathValue(invoice, "LegalMonetaryTotal", "PayableAmount")),
            TaxExclusiveAmount = ParseDecimalOrNull(GetPathValue(invoice, "LegalMonetaryTotal", "TaxExclusiveAmount")),
            TaxTotal = invoice.Elements()
                .Where(element => element.Name.LocalName == "TaxTotal")
                .Select(element => ParseDecimalOrNull(GetPathValue(element, "TaxAmount")))
                .Where(value => value.HasValue)
                .Sum(value => value!.Value),
            CurrencyCode = NormalizeOrNull(GetPathValue(invoice, "DocumentCurrencyCode")) ??
                           FindChild(FindChild(invoice, "LegalMonetaryTotal"), "PayableAmount")
                               ?.Attribute("currencyID")
                               ?.Value,
            DespatchReferences = despatchReferences,
            Warnings = warnings
        };
    }

    private async Task<XElement?> TryGetInvoiceRootAsync(
        string ettn,
        CancellationToken cancellationToken)
    {
        var directInvoice = await TryGetInvoiceRootByIdAsync(ettn, swallowNotFound: true, cancellationToken);
        if (directInvoice is not null)
        {
            return directInvoice;
        }

        var invoiceLookupIds = await ResolveInboxInvoiceLookupIdsAsync(ettn, cancellationToken);
        foreach (var invoiceLookupId in invoiceLookupIds)
        {
            var invoice = await TryGetInvoiceRootByIdAsync(invoiceLookupId, swallowNotFound: false, cancellationToken);
            if (invoice is not null)
            {
                return invoice;
            }
        }

        return null;
    }

    private async Task<XElement?> TryGetInvoiceRootByIdAsync(
        string invoiceId,
        bool swallowNotFound,
        CancellationToken cancellationToken)
    {
        UyumsoftOperationResponseDto uyumsoftResponse;

        try
        {
            uyumsoftResponse = await uyumsoftConnectedQueryService.InvokeGetOperationAsync(
                UyumsoftConnectedServiceKind.EInvoice,
                new UyumsoftOperationInvocationRequest(
                    "GetInboxInvoice",
                    [new UyumsoftOperationParameterRequest("invoiceId", invoiceId)]),
                cancellationToken);
        }
        catch (InvalidOperationException) when (swallowNotFound)
        {
            return null;
        }

        return TryFindInvoiceXml(uyumsoftResponse, out var invoiceXml)
            ? XDocument.Parse(invoiceXml, LoadOptions.PreserveWhitespace).Root
            : null;
    }

    private async Task<IReadOnlyCollection<string>> ResolveInboxInvoiceLookupIdsAsync(
        string ettn,
        CancellationToken cancellationToken)
    {
        var invoiceLookupIds = new List<string>();

        foreach (var operation in BuildInboxInvoiceListLookupOperations(ettn))
        {
            var uyumsoftResponse = await uyumsoftConnectedQueryService.InvokeGetOperationAsync(
                UyumsoftConnectedServiceKind.EInvoice,
                operation,
                cancellationToken);

            invoiceLookupIds.AddRange(
                uyumsoftResponse.InvoiceList?.Items
                    .Select(item => NormalizeOrNull(item.InvoiceUuid))
                    .Where(value => value is not null)
                    .Cast<string>() ?? []);
        }

        return invoiceLookupIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static InboundDespatchLookupResponse CreateNotFoundResponse(
        int warehouseNo,
        string receivingContext,
        string ettn,
        string documentKind,
        IReadOnlyCollection<string> warnings) =>
        new(
            false,
            warehouseNo,
            receivingContext,
            ettn,
            null,
            null,
            null,
            null,
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
            Array.Empty<InboundDespatchLineDto>())
        {
            SourceDocumentKind = documentKind,
            SourceDocumentLabel = ResolveDocumentLabel(documentKind),
            Warnings = warnings
        };

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
            matchedStock is not null && !isGoodsAcceptanceBlocked)
        {
            UnitPrice = line.UnitPrice,
            LineAmount = line.LineAmount,
            NetUnitPrice = CalculateNetUnitPrice(line.LineAmount, line.Quantity) ?? line.UnitPrice,
            PriceSource = line.LineAmount is not null && line.Quantity > 0d
                ? "line-extension-amount"
                : line.UnitPrice is not null
                    ? "price-amount"
                    : null,
            QuantitySource = line.QuantitySource
        };
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
            GetPathValue(itemElement, "StandardItemIdentification", "ID"))
        {
            QuantitySource = "despatch"
        };
    }

    private static LineDraft ParseInvoiceLineDraft(XElement lineElement)
    {
        var itemElement = FindChild(lineElement, "Item");
        var quantityElement = FindChild(lineElement, "InvoicedQuantity") ??
                              FindChild(lineElement, "CreditedQuantity") ??
                              FindChild(lineElement, "BaseQuantity");
        var itemDescriptions = string.Join(
            " | ",
            itemElement?.Elements()
                .Where(element => element.Name.LocalName == "Description")
                .Select(element => NormalizeOrNull(element.Value))
                .Where(value => value is not null)
                .Cast<string>() ?? Array.Empty<string>());
        var lineNotes = string.Join(
            " | ",
            lineElement.Elements()
                .Where(element => element.Name.LocalName == "Note")
                .Select(element => NormalizeOrNull(element.Value))
                .Where(value => value is not null)
                .Cast<string>());

        return new LineDraft(
            ParseIntOrNull(GetPathValue(lineElement, "ID")),
            GetPathValue(itemElement, "Name"),
            JoinNonEmpty(itemDescriptions, lineNotes),
            ParseDoubleOrDefault(quantityElement?.Value),
            quantityElement?.Attribute("unitCode")?.Value,
            GetPathValue(itemElement, "BuyersItemIdentification", "ID"),
            GetPathValue(itemElement, "SellersItemIdentification", "ID"),
            GetPathValue(itemElement, "ManufacturersItemIdentification", "ID"),
            GetPathValue(itemElement, "StandardItemIdentification", "ID"))
        {
            UnitPrice = ParseDoubleOrNull(GetPathValue(lineElement, "Price", "PriceAmount")),
            LineAmount = ParseDecimalOrNull(GetPathValue(lineElement, "LineExtensionAmount")),
            QuantitySource = "invoice"
        };
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

    private static IEnumerable<UyumsoftOperationInvocationRequest> BuildInboxInvoiceListLookupOperations(string ettn)
    {
        yield return new UyumsoftOperationInvocationRequest(
            "GetInboxInvoices",
            [
                new UyumsoftOperationParameterRequest("PageIndex", "0"),
                new UyumsoftOperationParameterRequest("PageSize", "5"),
                new UyumsoftOperationParameterRequest("OnlyNewestInvoices", "false"),
                new UyumsoftOperationParameterRequest("InvoiceIds", ettn)
            ]);
        yield return new UyumsoftOperationInvocationRequest(
            "GetInboxInvoices",
            [
                new UyumsoftOperationParameterRequest("PageIndex", "0"),
                new UyumsoftOperationParameterRequest("PageSize", "5"),
                new UyumsoftOperationParameterRequest("OnlyNewestInvoices", "false"),
                new UyumsoftOperationParameterRequest("InvoiceNumbers", ettn)
            ]);
    }

    private static bool TryFindDespatchAdviceXml(
        UyumsoftOperationResponseDto response,
        out string despatchAdviceXml)
    {
        foreach (var value in response.Nodes.SelectMany(FlattenNodeValues))
        {
            if (TryFindXmlDocument(value, "DespatchAdvice", out despatchAdviceXml))
            {
                return true;
            }
        }

        despatchAdviceXml = string.Empty;
        return false;
    }

    private static bool TryFindInvoiceXml(
        UyumsoftOperationResponseDto response,
        out string invoiceXml)
    {
        foreach (var value in response.Nodes.SelectMany(FlattenNodeValues))
        {
            if (TryFindXmlDocument(value, "Invoice", out invoiceXml))
            {
                return true;
            }
        }

        invoiceXml = string.Empty;
        return false;
    }

    private static bool TryFindXmlDocument(
        string? value,
        string rootLocalName,
        out string documentXml)
    {
        foreach (var candidate in EnumerateXmlCandidates(value))
        {
            if (!candidate.Contains($"<{rootLocalName}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryExtractXmlRoot(candidate, rootLocalName, out documentXml))
            {
                return true;
            }
        }

        documentXml = string.Empty;
        return false;
    }

    private static bool TryExtractXmlRoot(
        string xmlCandidate,
        string rootLocalName,
        out string documentXml)
    {
        try
        {
            var document = XDocument.Parse(xmlCandidate, LoadOptions.PreserveWhitespace);
            var root = document.Root?.Name.LocalName == rootLocalName
                ? document.Root
                : document.Descendants().FirstOrDefault(element => element.Name.LocalName == rootLocalName);

            if (root is not null)
            {
                documentXml = root.ToString(SaveOptions.DisableFormatting);
                return true;
            }
        }
        catch (System.Xml.XmlException)
        {
            if (TrySliceXmlDocument(xmlCandidate, rootLocalName, out documentXml))
            {
                return true;
            }
        }

        documentXml = string.Empty;
        return false;
    }

    private static bool TrySliceXmlDocument(
        string xmlCandidate,
        string rootLocalName,
        out string documentXml)
    {
        var startIndex = xmlCandidate.IndexOf($"<{rootLocalName}", StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            documentXml = string.Empty;
            return false;
        }

        var closeTag = $"</{rootLocalName}>";
        var endIndex = xmlCandidate.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex < startIndex)
        {
            documentXml = string.Empty;
            return false;
        }

        documentXml = xmlCandidate[startIndex..(endIndex + closeTag.Length)].Trim();
        return true;
    }

    private static IEnumerable<string> EnumerateXmlCandidates(string? value)
    {
        var normalized = NormalizeOrNull(value);
        if (normalized is null)
        {
            yield break;
        }

        yield return normalized;

        var decoded = WebUtility.HtmlDecode(normalized);
        if (!string.Equals(decoded, normalized, StringComparison.Ordinal))
        {
            yield return decoded;
        }
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

    private static string NormalizeDocumentKind(string? documentKind)
    {
        var normalized = NormalizeOrNull(documentKind)
            ?.Replace("_", "-", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        return normalized switch
        {
            null or "" or "auto" or "e-belge" or "ebelge" or "resmi-belge" or "official-document" => AutoDocumentKind,
            "e-irsaliye" or "eirsaliye" or "irsaliye" or "e-despatch" or "edespatch" or "despatch" => EDespatchDocumentKind,
            "e-fatura" or "efatura" or "fatura" or "e-invoice" or "einvoice" or "invoice" => EInvoiceDocumentKind,
            _ => throw new ArgumentException("DocumentKind must be auto, e-despatch or e-invoice.", nameof(documentKind))
        };
    }

    private static string ResolveDocumentLabel(string documentKind) =>
        documentKind switch
        {
            EDespatchDocumentKind => "E-Irsaliye",
            EInvoiceDocumentKind => "E-Fatura",
            _ => "E-Belge"
        };

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

    private static string? GetFirstPathValue(XElement? parent, params string[][] localNamePaths)
    {
        foreach (var localNamePath in localNamePaths)
        {
            var value = GetPathValue(parent, localNamePath);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
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

    private static TimeOnly? ParseTimeOrNull(string? value)
    {
        var normalized = NormalizeOrNull(value);
        if (normalized is null)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTimeOffset))
        {
            return TimeOnly.FromDateTime(parsedDateTimeOffset.DateTime);
        }

        if (DateTime.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime))
        {
            return TimeOnly.FromDateTime(parsedDateTime);
        }

        var timePart = normalized;
        var timeSeparatorIndex = timePart.IndexOf('T', StringComparison.OrdinalIgnoreCase);
        if (timeSeparatorIndex >= 0 && timeSeparatorIndex + 1 < timePart.Length)
        {
            timePart = timePart[(timeSeparatorIndex + 1)..];
        }

        if (timePart.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            timePart = timePart[..^1];
        }

        var offsetSeparatorIndex = timePart.IndexOfAny(['+', '-']);
        if (offsetSeparatorIndex > 0)
        {
            timePart = timePart[..offsetSeparatorIndex];
        }

        return TimeOnly.TryParse(
            timePart,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsedTime)
            ? parsedTime
            : null;
    }

    private static int? ParseIntOrNull(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static double ParseDoubleOrDefault(string? value) =>
        double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;

    private static double? ParseDoubleOrNull(string? value) =>
        double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static double? CalculateNetUnitPrice(decimal? lineAmount, double quantity)
    {
        if (lineAmount is null || quantity <= 0d)
        {
            return null;
        }

        return Math.Round((double)(lineAmount.Value / (decimal)quantity), 4, MidpointRounding.AwayFromZero);
    }

    private static decimal? ParseDecimalOrNull(string? value) =>
        decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

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

    private static string? JoinNonEmpty(params string?[] values)
    {
        var joined = string.Join(
            ' ',
            values
                .Select(NormalizeOrNull)
                .Where(value => value is not null)
                .Cast<string>());

        return NormalizeOrNull(joined);
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
        string? Barcode)
    {
        public double? UnitPrice { get; init; }

        public decimal? LineAmount { get; init; }

        public string? QuantitySource { get; init; }
    }
}
