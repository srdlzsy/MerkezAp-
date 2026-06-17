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
                    CreateOperation("GetAccessToken", "Sistem", "TokenRequest alanlarini parameter olarak gonderin."),
                    CreateOperation("GetEInvoiceUsers", "Kullanicilar", "Sorgu modeli alanlarini parameter olarak gonderin."),
                    CreateOperation("GetUserAliasses", "Kullanicilar", "Kimlik veya sorgu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetSystemUsersCompressedList", "Kullanicilar", "Genellikle ek parametre gerekmez; gerekiyorsa parameter kullanin."),
                    CreateOperation("GetSystemUsersCompressedListOld", "Kullanicilar", "Genellikle ek parametre gerekmez; gerekiyorsa parameter kullanin."),
                    CreateOperation("GetInboxInvoices", "Gelen Fatura", "Fatura sorgu modeli alanlarini parameter olarak gonderin."),
                    CreateOperation("GetInboxInvoiceList", "Gelen Fatura", "Liste sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetInboxInvoice", "Gelen Fatura", "Tekil belge icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoicesData", "Gelen Fatura", "Veri sorgusu alanlarini parameters ile gonderin."),
                    CreateOperation("GetInboxInvoiceData", "Gelen Fatura", "Tekil belge verisi icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoiceView", "Gelen Fatura", "Gorunum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoicePdf", "Gelen Fatura", "PDF icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInboxInvoiceStatusWithLogs", "Gelen Fatura", "Durum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoices", "Giden Fatura", "Fatura sorgu modeli alanlarini parameter olarak gonderin."),
                    CreateOperation("GetOutboxInvoiceList", "Giden Fatura", "Liste sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetOutboxInvoice", "Giden Fatura", "Tekil belge icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoicesData", "Giden Fatura", "Veri sorgusu alanlarini parameters ile gonderin."),
                    CreateOperation("GetOutboxInvoiceData", "Giden Fatura", "Tekil belge verisi icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoiceView", "Giden Fatura", "Gorunum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoicePdf", "Giden Fatura", "PDF icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoiceStatusWithLogs", "Giden Fatura", "Durum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetOutboxInvoiceResponseView", "Giden Fatura", "Yanita ait gorunum icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetInvoiceEnvelope", "Dokuman", "Zarf icin scalar parameter kullanin: invoiceId."),
                    CreateOperation("GetSummaryReport", "Raporlama", "Rapor sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetCustomerCreditInfo", "Raporlama", "Cari veya VKN/TCKN bilgisini parameter olarak gonderin.")
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
                    CreateOperation("GetAccessToken", "Sistem", "TokenRequest alanlarini parameter olarak gonderin."),
                    CreateOperation("GetEDespatchUsers", "Kullanicilar", "Sorgu modeli alanlarini parameter olarak gonderin."),
                    CreateOperation("GetUserAliasses", "Kullanicilar", "Kimlik veya sorgu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetCustomerCreditInfo", "Kullanicilar", "Cari veya VKN/TCKN bilgisini parameter olarak gonderin."),
                    CreateOperation("GetInboxDespatch", "Gelen Irsaliye", "Tekil belge icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxDespatches", "Gelen Irsaliye", "Sorgu modeli alanlarini parameter olarak gonderin."),
                    CreateOperation("GetInboxDespatchList", "Gelen Irsaliye", "Liste sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetInboxDespatchesData", "Gelen Irsaliye", "Veri sorgusu alanlarini parameters ile gonderin."),
                    CreateOperation("GetInboxDespatchView", "Gelen Irsaliye", "Gorunum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxDespatchPdf", "Gelen Irsaliye", "PDF icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxDespatchStatusWithLogs", "Gelen Irsaliye", "Durum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatch", "Giden Irsaliye", "Tekil belge icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatches", "Giden Irsaliye", "Sorgu modeli alanlarini parameter olarak gonderin."),
                    CreateOperation("GetOutboxDespatchList", "Giden Irsaliye", "Liste sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetOutboxDespatchesData", "Giden Irsaliye", "Veri sorgusu alanlarini parameters ile gonderin."),
                    CreateOperation("GetOutboxDespatchView", "Giden Irsaliye", "Gorunum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatchPdf", "Giden Irsaliye", "PDF icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetOutboxDespatchStatusWithLogs", "Giden Irsaliye", "Durum icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetReceiptAdviceView", "Makbuz", "Makbuz gorunumu icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetReceiptAdvicePdf", "Makbuz", "Makbuz PDF icin scalar parameter kullanin: despatchId."),
                    CreateOperation("GetInboxReceiptAdvicesList", "Makbuz", "Liste sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetInboxReceiptAdvices", "Makbuz", "Makbuz sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetInboxReceiptAdvicesData", "Makbuz", "Makbuz veri sorgusu alanlarini parameter olarak gonderin."),
                    CreateOperation("GetDespatchEnvelope", "Dokuman", "Zarf icin scalar parameter kullanin: despatchId + isInbox.")
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

