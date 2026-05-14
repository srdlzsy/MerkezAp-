using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;

namespace FurpaMerkezApi.Infrastructure.Services;

internal static class UyumsoftConnectedServiceCatalog
{
    private static readonly IReadOnlyDictionary<UyumsoftConnectedServiceKind, UyumsoftServiceCatalogEntry> Entries =
        new Dictionary<UyumsoftConnectedServiceKind, UyumsoftServiceCatalogEntry>
        {
            [UyumsoftConnectedServiceKind.EInvoice] = new(
                "e-fatura",
                "Uyumsoft BasicIntegration",
                "http://efatura.uyumsoft.com.tr/Services/BasicIntegration",
                "http://efatura.uyumsoft.com.tr/Services/BasicIntegration?wsdl",
                "IBasicIntegration",
                new[]
                {
                    CreateOperation("GetSystemDate", "Sistem", "Parametre gerekmez."),
                    CreateOperation("GetSystemDateWithFormat", "Sistem", "Format icin scalar parameter kullanin: format."),
                    CreateOperation("GetAccessToken", "Sistem", "TokenRequest icin payloadXml kullanin: <request>...</request>."),
                    CreateOperation("GetEInvoiceUsers", "Kullanicilar", "Sorgu modeli icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetUserAliasses", "Kullanicilar", "Kimlik veya sorgu nesnesini payloadXml ile gonderin."),
                    CreateOperation("GetSystemUsersCompressedList", "Kullanicilar", "Genellikle ek parametre gerekmez; gerekiyorsa payloadXml kullanin."),
                    CreateOperation("GetSystemUsersCompressedListOld", "Kullanicilar", "Genellikle ek parametre gerekmez; gerekiyorsa payloadXml kullanin."),
                    CreateOperation("GetInboxInvoices", "Gelen Fatura", "Fatura sorgu modeli icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxInvoiceList", "Gelen Fatura", "Liste sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxInvoice", "Gelen Fatura", "Tekil belge icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoicesData", "Gelen Fatura", "Veri sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxInvoiceData", "Gelen Fatura", "Tekil belge verisi icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoiceView", "Gelen Fatura", "Gorunum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoicePdf", "Gelen Fatura", "PDF icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoiceStatusWithLogs", "Gelen Fatura", "Durum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoices", "Giden Fatura", "Fatura sorgu modeli icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetOutboxInvoiceList", "Giden Fatura", "Liste sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetOutboxInvoice", "Giden Fatura", "Tekil belge icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoicesData", "Giden Fatura", "Veri sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetOutboxInvoiceData", "Giden Fatura", "Tekil belge verisi icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoiceView", "Giden Fatura", "Gorunum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoicePdf", "Giden Fatura", "PDF icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoiceStatusWithLogs", "Giden Fatura", "Durum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoiceResponseView", "Giden Fatura", "Yanita ait gorunum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInvoiceEnvelope", "Dokuman", "Zarf icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetSummaryReport", "Raporlama", "Rapor sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetCustomerCreditInfo", "Raporlama", "Cari veya VKN/TCKN bilgisini payloadXml ile gonderin.")
                }),
            [UyumsoftConnectedServiceKind.EDespatch] = new(
                "e-irsaliye",
                "Uyumsoft BasicDespatchIntegration",
                "http://efatura.uyumsoft.com.tr/Services/BasicDespatchIntegration",
                "http://efatura.uyumsoft.com.tr/Services/BasicDespatchIntegration?wsdl",
                "IBasicDespatchIntegration",
                new[]
                {
                    CreateOperation("GetSystemDate", "Sistem", "Parametre gerekmez."),
                    CreateOperation("GetSystemDateWithFormat", "Sistem", "Format icin scalar parameter kullanin: format."),
                    CreateOperation("GetAccessToken", "Sistem", "TokenRequest icin payloadXml kullanin: <request>...</request>."),
                    CreateOperation("GetEDespatchUsers", "Kullanicilar", "Sorgu modeli icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetUserAliasses", "Kullanicilar", "Kimlik veya sorgu nesnesini payloadXml ile gonderin."),
                    CreateOperation("GetCustomerCreditInfo", "Kullanicilar", "Cari veya VKN/TCKN bilgisini payloadXml ile gonderin."),
                    CreateOperation("GetInboxDespatch", "Gelen Irsaliye", "Tekil belge icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxDespatches", "Gelen Irsaliye", "Sorgu modeli icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxDespatchList", "Gelen Irsaliye", "Liste sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxDespatchesData", "Gelen Irsaliye", "Veri sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxDespatchView", "Gelen Irsaliye", "Gorunum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxDespatchPdf", "Gelen Irsaliye", "PDF icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxDespatchStatusWithLogs", "Gelen Irsaliye", "Durum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatch", "Giden Irsaliye", "Tekil belge icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatches", "Giden Irsaliye", "Sorgu modeli icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetOutboxDespatchList", "Giden Irsaliye", "Liste sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetOutboxDespatchesData", "Giden Irsaliye", "Veri sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetOutboxDespatchView", "Giden Irsaliye", "Gorunum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatchPdf", "Giden Irsaliye", "PDF icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatchStatusWithLogs", "Giden Irsaliye", "Durum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetReceiptAdviceView", "Makbuz", "Makbuz gorunumu icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetReceiptAdvicePdf", "Makbuz", "Makbuz PDF icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxReceiptAdvicesList", "Makbuz", "Liste sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxReceiptAdvices", "Makbuz", "Makbuz sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetInboxReceiptAdvicesData", "Makbuz", "Makbuz veri sorgusu icin payloadXml kullanin: <query>...</query>."),
                    CreateOperation("GetDespatchEnvelope", "Dokuman", "Zarf icin scalar parameter veya payloadXml kullanin: despatchId + isInbox.")
                })
        };

    public static UyumsoftServiceCatalogEntry GetService(UyumsoftConnectedServiceKind serviceKind) =>
        Entries.TryGetValue(serviceKind, out var entry)
            ? entry
            : throw new ArgumentOutOfRangeException(nameof(serviceKind), serviceKind, "Unsupported Uyumsoft service.");

    public static UyumsoftOperationDefinitionDto GetGetOperation(
        UyumsoftConnectedServiceKind serviceKind,
        string operationName)
    {
        var entry = GetService(serviceKind);
        var operation = entry.Operations.FirstOrDefault(item =>
            string.Equals(item.OperationName, operationName, StringComparison.OrdinalIgnoreCase));

        return operation
            ?? throw new ArgumentException(
                $"{entry.ServiceName} servisi icin {operationName} operasyonu katalogda bulunmuyor.");
    }

    private static UyumsoftOperationDefinitionDto CreateOperation(
        string operationName,
        string groupName,
        string requestHint) =>
        new(
            operationName,
            groupName,
            string.Empty,
            requestHint);
}

internal sealed record UyumsoftServiceCatalogEntry(
    string ServiceKey,
    string ServiceName,
    string DefaultEndpointUrl,
    string DefaultWsdlUrl,
    string ContractName,
    IReadOnlyCollection<UyumsoftOperationDefinitionDto> Operations);
