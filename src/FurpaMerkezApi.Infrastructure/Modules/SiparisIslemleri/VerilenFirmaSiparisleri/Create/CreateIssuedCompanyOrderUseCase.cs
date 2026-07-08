using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

public sealed class CreateIssuedCompanyOrderUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<CreateIssuedCompanyOrderUseCase> logger)
    : ICreateIssuedCompanyOrderUseCase
{
    private const short FileId = 21;
    private const short MikroUserNo = 39;
    private const int FirstDocumentOrderNo = 0;
    private const byte IssuedOrderType = 1;
    private const byte NormalOrderGenre = 0;
    private const string SiparisKaydetPath = "/api/APIMethods/SiparisKaydetV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;
    private static readonly DateTime MikroEmptyDate = new(1900, 1, 1);

    public async Task<CreateIssuedCompanyOrderResponse> ExecuteAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.IssuedCompanyOrder switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => await ExecuteDualShadowAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:IssuedCompanyOrder mode '{mode}'.")
        };
    }

    private async Task<CreateIssuedCompanyOrderResponse> ExecuteDatabaseAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = request.DeliveryDate.Date;
        var documentSerie = $"F{request.WarehouseNo}";
        var lines = request.Lines.ToArray();
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var customer = await GetCustomerInfoAsync(request.CustomerCode, cancellationToken);
                var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
                var entities = lines
                    .Select((line, rowNo) => CreateOrderLine(
                        request,
                        line,
                        customer,
                        rowNo,
                        now,
                        orderDate,
                        deliveryDate,
                        documentSerie,
                        documentOrderNo))
                    .ToArray();

                await mikroWriteDbContext.SIPARISLERs.AddRangeAsync(entities, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateIssuedCompanyOrderResponse(
                    documentSerie,
                    documentOrderNo,
                    orderDate,
                    deliveryDate,
                    request.WarehouseNo,
                    request.CustomerCode.Trim(),
                    entities.Length,
                    lines.Sum(line => line.Quantity),
                    entities.Sum(entity => entity.sip_tutar ?? 0d),
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<CreateIssuedCompanyOrderResponse> ExecuteMikroApiAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = request.DeliveryDate.Date;
        var documentSerie = $"F{request.WarehouseNo}";
        var lines = request.Lines.ToArray();
        var customer = await GetCustomerInfoAsync(request.CustomerCode, cancellationToken);
        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
        var payload = IssuedCompanyOrderMikroApiPayloadFactory.Create(
            request,
            lines,
            customer.PaymentPlanNo,
            customer.CanBeCalled,
            orderDate,
            deliveryDate,
            documentSerie,
            documentOrderNo);

        logger.LogInformation(
            "Issued company order create is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, CustomerCode={CustomerCode}, LineCount={LineCount}",
            SiparisKaydetPath,
            documentSerie,
            documentOrderNo,
            request.WarehouseNo,
            request.CustomerCode.Trim(),
            lines.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            SiparisKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API issued company order create failed.");
        }

        var recovered = await RecoverMikroApiCreateResponseAsync(
            documentSerie,
            documentOrderNo,
            request,
            orderDate,
            deliveryDate,
            options.ConnectionStringName,
            cancellationToken);

        await mikroApiClient.MarkRecoveredAsync(
            result,
            $"{recovered.DocumentSerie}/{recovered.DocumentOrderNo}",
            cancellationToken: cancellationToken);
        return recovered;
    }

    private async Task<CreateIssuedCompanyOrderResponse> ExecuteDualShadowAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:IssuedCompanyOrder is DualShadow. SiparisKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    private async Task<CreateIssuedCompanyOrderResponse> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedCompanyOrderRequest request,
        DateTime orderDate,
        DateTime deliveryDate,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverCompanyOrderResponseAsync(
                documentSerie,
                documentOrderNo,
                request,
                orderDate,
                deliveryDate,
                writeConnectionName,
                cancellationToken);

            if (response is not null)
            {
                return response;
            }

            if (attempt < MikroApiRecoveryAttemptCount)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(MikroApiRecoveryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Mikro API issued company order create succeeded, but created SIPARISLER rows could not be read back.");
    }

    private async Task<CreateIssuedCompanyOrderResponse?> TryRecoverCompanyOrderResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedCompanyOrderRequest request,
        DateTime orderDate,
        DateTime deliveryDate,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerCode = request.CustomerCode.Trim();
        var rows = await mikroWriteDbContext.SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.sip_tip == IssuedOrderType &&
                order.sip_cins == NormalOrderGenre &&
                order.sip_evrakno_seri == documentSerie &&
                order.sip_evrakno_sira == documentOrderNo &&
                order.sip_depono == request.WarehouseNo &&
                order.sip_musteri_kod == normalizedCustomerCode)
            .Select(order => new
            {
                order.sip_tarih,
                order.sip_teslim_tarih,
                order.sip_evrakno_seri,
                order.sip_evrakno_sira,
                order.sip_depono,
                order.sip_musteri_kod,
                order.sip_miktar,
                order.sip_tutar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sip_tarih,
                row.sip_evrakno_seri,
                row.sip_evrakno_sira,
                row.sip_depono,
                row.sip_musteri_kod
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one issued company order document matched the same serie and order number.");
        }

        var firstRow = rows[0];

        return new CreateIssuedCompanyOrderResponse(
            firstRow.sip_evrakno_seri ?? documentSerie,
            firstRow.sip_evrakno_sira ?? documentOrderNo,
            firstRow.sip_tarih?.Date ?? orderDate,
            rows.Max(row => row.sip_teslim_tarih)?.Date ?? deliveryDate,
            firstRow.sip_depono ?? request.WarehouseNo,
            firstRow.sip_musteri_kod ?? normalizedCustomerCode,
            rows.Count,
            rows.Sum(row => row.sip_miktar ?? 0d),
            rows.Sum(row => row.sip_tutar ?? 0d),
            writeConnectionName);
    }

    private async Task<CustomerOrderDefaults> GetCustomerInfoAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerCode = customerCode.Trim();
        var customer = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(item => item.cari_kod == normalizedCustomerCode)
            .Select(item => new CustomerOrderDefaults(
                item.cari_odemeplan_no ?? 0,
                item.cari_pasaport_no == "1"))
            .FirstOrDefaultAsync(cancellationToken);

        return customer ?? throw new KeyNotFoundException("Customer was not found in Mikro write database.");
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.SIPARISLERs
            .Where(order =>
                order.sip_tip == IssuedOrderType &&
                order.sip_cins == NormalOrderGenre &&
                order.sip_evrakno_seri == documentSerie)
            .MaxAsync(order => order.sip_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private static SIPARISLER CreateOrderLine(
        CreateIssuedCompanyOrderRequest request,
        CreateIssuedCompanyOrderLineRequest line,
        CustomerOrderDefaults customer,
        int rowNo,
        DateTime now,
        DateTime orderDate,
        DateTime deliveryDate,
        string documentSerie,
        int documentOrderNo)
    {
        var unitPrice = line.UnitPrice;
        var amount = line.Quantity * unitPrice;

        return new SIPARISLER
        {
            sip_Guid = Guid.NewGuid(),
            sip_DBCno = 0,
            sip_SpecRECno = 0,
            sip_iptal = false,
            sip_fileid = FileId,
            sip_hidden = false,
            sip_kilitli = false,
            sip_degisti = false,
            sip_checksum = 0,
            sip_create_user = MikroUserNo,
            sip_create_date = now,
            sip_lastup_user = MikroUserNo,
            sip_lastup_date = now,
            sip_special1 = "0",
            sip_special2 = string.Empty,
            sip_special3 = string.Empty,
            sip_firmano = 0,
            sip_subeno = 0,
            sip_tarih = orderDate,
            sip_teslim_tarih = deliveryDate,
            sip_tip = IssuedOrderType,
            sip_cins = NormalOrderGenre,
            sip_evrakno_seri = documentSerie,
            sip_evrakno_sira = documentOrderNo,
            sip_satirno = rowNo,
            sip_belgeno = string.Empty,
            sip_belge_tarih = orderDate,
            sip_satici_kod = string.Empty,
            sip_musteri_kod = request.CustomerCode.Trim(),
            sip_stok_kod = line.StockCode.Trim(),
            sip_b_fiyat = unitPrice,
            sip_miktar = line.Quantity,
            sip_birim_pntr = Convert.ToByte(line.UnitPointer),
            sip_teslim_miktar = 0d,
            sip_tutar = amount,
            sip_iskonto_1 = 0d,
            sip_iskonto_2 = 0d,
            sip_iskonto_3 = 0d,
            sip_iskonto_4 = 0d,
            sip_iskonto_5 = 0d,
            sip_iskonto_6 = 0d,
            sip_masraf_1 = 0d,
            sip_masraf_2 = 0d,
            sip_masraf_3 = 0d,
            sip_masraf_4 = 0d,
            sip_vergi_pntr = 2,
            sip_vergi = 0d,
            sip_masvergi_pntr = 0,
            sip_masvergi = 0d,
            sip_opno = customer.PaymentPlanNo,
            sip_aciklama = NormalizeText(line.Description1 ?? request.Description1),
            sip_aciklama2 = NormalizeText(line.Description2 ?? request.Description2),
            sip_depono = request.WarehouseNo,
            sip_OnaylayanKulNo = 0,
            sip_vergisiz_fl = false,
            sip_kapat_fl = false,
            sip_promosyon_fl = false,
            sip_cari_sormerk = NormalizeText(line.CustomerResponsibilityCenter),
            sip_stok_sormerk = NormalizeText(line.ProductResponsibilityCenter),
            sip_cari_grupno = 0,
            sip_doviz_cinsi = 0,
            sip_doviz_kuru = 1d,
            sip_alt_doviz_kuru = 1d,
            sip_adresno = 1,
            sip_teslimturu = string.Empty,
            sip_cagrilabilir_fl = customer.CanBeCalled,
            sip_prosip_uid = Guid.Empty,
            sip_isk1 = true,
            sip_isk2 = true,
            sip_isk3 = false,
            sip_isk4 = false,
            sip_isk5 = false,
            sip_isk6 = false,
            sip_mas1 = false,
            sip_mas2 = false,
            sip_mas3 = false,
            sip_mas4 = false,
            sip_iskonto1 = 0,
            sip_iskonto2 = 1,
            sip_iskonto3 = 1,
            sip_iskonto4 = 1,
            sip_iskonto5 = 1,
            sip_iskonto6 = 1,
            sip_masraf1 = 1,
            sip_masraf2 = 1,
            sip_masraf3 = 1,
            sip_masraf4 = 1,
            sip_Exp_Imp_Kodu = string.Empty,
            sip_kar_orani = 0d,
            sip_durumu = 0,
            sip_stal_uid = Guid.Empty,
            sip_planlananmiktar = 0d,
            sip_teklif_uid = Guid.Empty,
            sip_parti_kodu = string.Empty,
            sip_lot_no = 0,
            sip_projekodu = NormalizeText(line.ProjectCode),
            sip_fiyat_liste_no = -1,
            sip_Otv_Pntr = 0,
            sip_Otv_Vergi = 0d,
            sip_otvtutari = 0d,
            sip_OtvVergisiz_Fl = 0,
            sip_paket_kod = NormalizeText(line.PackageCode),
            sip_Rez_uid = Guid.Empty,
            sip_harekettipi = 0,
            sip_yetkili_uid = Guid.Empty,
            sip_kapatmanedenkod = string.Empty,
            sip_gecerlilik_tarihi = MikroEmptyDate,
            sip_onodeme_evrak_tip = 0,
            sip_onodeme_evrak_seri = string.Empty,
            sip_onodeme_evrak_sira = 0,
            sip_rezervasyon_miktari = line.RecommendedQuantity ?? 0d,
            sip_rezerveden_teslim_edilen = 0d,
            sip_HareketGrupKodu1 = string.Empty,
            sip_HareketGrupKodu2 = NormalizeText(request.Deliverer),
            sip_HareketGrupKodu3 = NormalizeText(request.Receiver),
            sip_Olcu1 = 0d,
            sip_Olcu2 = 0d,
            sip_Olcu3 = 0d,
            sip_Olcu4 = 0d,
            sip_Olcu5 = 0d,
            sip_FormulMiktar = 0d,
            sip_FormulMiktarNo = 0,
            sip_satis_fiyat_doviz_cinsi = 0,
            sip_satis_fiyat_doviz_kuru = 1d,
            sip_eticaret_kanal_kodu = string.Empty,
            sip_Tevkifat_turu = 0,
            sip_otv_tevkifat_turu = 0,
            sip_otv_tevkifat_tutari = 0d,
            sip_tevkifat_sifirlandi_fl = false
        };
    }

    private static void Validate(CreateIssuedCompanyOrderRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            throw new ArgumentException("Customer code is required.", nameof(request.CustomerCode));
        }

        if (request.DeliveryDate == default)
        {
            throw new ArgumentException("Delivery date is required.", nameof(request.DeliveryDate));
        }

        if (request.OrderDate.HasValue && request.DeliveryDate.Date < request.OrderDate.Value.Date)
        {
            throw new ArgumentException("Delivery date can not be earlier than order date.", nameof(request.DeliveryDate));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one order line is required.", nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required.", nameof(request.Lines));
            }

            if (line.Quantity <= 0)
            {
                throw new ArgumentException("Line quantity must be greater than zero.", nameof(request.Lines));
            }

            if (line.UnitPrice < 0)
            {
                throw new ArgumentException("Line unit price can not be negative.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }

            if (line.RecommendedQuantity is < 0)
            {
                throw new ArgumentException("Line recommended quantity can not be negative.", nameof(request.Lines));
            }
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private sealed record CustomerOrderDefaults(
        int PaymentPlanNo,
        bool CanBeCalled);
}
