using System.Data;
using System.Data.Common;
using System.Text;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UyumsoftDespatch = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Despatch;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class EDespatchService(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    IDocumentFlowService documentFlowService,
    IOptions<EDespatchOptions> options,
    ILogger<EDespatchService> logger)
    : IEDespatchService
{
    private const string DespatchNamespace = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";
    private const string AggregateNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string BasicNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const short MikroUserNo = 39;
    private const byte CompanyDispatchDocumentType = 1;
    private const byte ReceivingReceiptDocumentType = 13;
    private const byte OutgoingMovementType = 1;
    private const byte IncomingMovementType = 0;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const string CommonEDespatchDocumentPrefix = "FRM";
    private const string DocumentNumberLockResource = "FurpaMerkezApi:EDespatchDocumentNumber";
    private const int DocumentNumberLockTimeoutMilliseconds = 120_000;
    private static readonly SemaphoreSlim LocalDocumentNumberLock = new(1, 1);

    public async Task<SendEDespatchResponse> SendAsync(
        SendEDespatchRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var config = options.Value;
        ValidateConfiguration(config);

        try
        {
            await using var documentNumberLock = await AcquireDocumentNumberLockAsync(cancellationToken);
            var response = request.DocumentType switch
            {
                EDespatchDocumentType.OutgoingCompanyShipment => await SendCompanyMovementAsync(
                    request,
                    CompanyMovementKind.OutgoingShipment,
                    cancellationToken),

                EDespatchDocumentType.CompanyReturn => await SendCompanyMovementAsync(
                    request,
                    CompanyMovementKind.PurchaseReturn,
                    cancellationToken),

                EDespatchDocumentType.InterWarehouseShipment => await SendInterWarehouseDocumentAsync(
                    request,
                    false,
                    cancellationToken),

                EDespatchDocumentType.WarehouseReturn => await SendInterWarehouseDocumentAsync(
                    request,
                    true,
                    cancellationToken),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(request.DocumentType),
                    request.DocumentType,
                    "Unsupported e-despatch document type.")
            };

            await RecordEDespatchFlowAsync(
                request,
                DocumentFlowStatus.Succeeded,
                "E-irsaliye Uyumsoft'a basariyla gonderildi.",
                null,
                response,
                cancellationToken);

            return response;
        }
        catch (Exception exception)
        {
            await RecordEDespatchFlowAsync(
                request,
                DocumentFlowStatus.Failed,
                "E-irsaliye gonderimi basarisiz oldu.",
                exception.Message,
                null,
                CancellationToken.None);
            throw;
        }
    }

    private Task RecordEDespatchFlowAsync(
        SendEDespatchRequest request,
        DocumentFlowStatus status,
        string message,
        string? error,
        SendEDespatchResponse? response,
        CancellationToken cancellationToken)
    {
        var documentType = request.DocumentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => DocumentFlowType.CompanyShipment,
            EDespatchDocumentType.CompanyReturn => DocumentFlowType.CompanyReturn,
            EDespatchDocumentType.InterWarehouseShipment => DocumentFlowType.InterWarehouseShipment,
            EDespatchDocumentType.WarehouseReturn => DocumentFlowType.WarehouseReturn,
            _ => throw new ArgumentOutOfRangeException(nameof(request.DocumentType))
        };

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    documentType,
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo),
                documentType,
                request.WarehouseNo,
                null,
                request.DocumentSerie,
                request.DocumentOrderNo,
                DocumentFlowStep.EDespatchSubmission,
                status,
                message,
                error,
                ExternalDocumentNo: response?.EDespatchDocumentNo,
                ExternalUuid: response?.EDespatchUuid),
            cancellationToken);
    }

    public async Task<GetEDespatchPdfResponse> GetPdfAsync(
        GetEDespatchPdfRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var config = options.Value;
        ValidateConfiguration(config);

        var sentDespatch = request.DocumentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => ExtractSentDespatchInfo(
                (await ResolveCompanyMovementAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    CompanyMovementKind.OutgoingShipment,
                    cancellationToken)).TrackedMovements),

            EDespatchDocumentType.CompanyReturn => ExtractSentDespatchInfo(
                (await ResolveCompanyMovementAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    CompanyMovementKind.PurchaseReturn,
                    cancellationToken)).TrackedMovements),

            EDespatchDocumentType.InterWarehouseShipment => ExtractSentDespatchInfo(
                (await ResolveInterWarehouseDocumentAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    false,
                    cancellationToken)).TrackedMovements),

            EDespatchDocumentType.WarehouseReturn => ExtractSentDespatchInfo(
                (await ResolveInterWarehouseDocumentAsync(
                    request.WarehouseNo,
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    true,
                    cancellationToken)).TrackedMovements),

            _ => throw new ArgumentOutOfRangeException(
                nameof(request.DocumentType),
                request.DocumentType,
                "Unsupported e-despatch document type.")
        };

        var serviceDocumentId = await ResolveOutboxDespatchIdAsync(
            config,
            sentDespatch.EDespatchDocumentNo,
            cancellationToken);
        var pdfContent = await GetOutboxDespatchPdfAsync(
            config,
            serviceDocumentId,
            cancellationToken);

        return new GetEDespatchPdfResponse(
            $"{sentDespatch.EDespatchDocumentNo}.pdf",
            pdfContent);
    }

    private async Task<SendEDespatchResponse> SendCompanyMovementAsync(
        SendEDespatchRequest request,
        CompanyMovementKind movementKind,
        CancellationToken cancellationToken)
    {
        var document = await ResolveCompanyMovementAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            movementKind,
            cancellationToken);
        EnsureNotAlreadySent(document.TrackedMovements);
        var config = options.Value;
        var now = DateTime.Now;
        var eDespatchDocumentNo = await BuildEDespatchDocumentNoAsync(
            now.Year,
            cancellationToken);
        var eDespatchUuid = Guid.NewGuid().ToString();
        var sourceWarehouse = await LoadWarehouseAsync(
            document.Context,
            document.Detail.Header.WarehouseNo,
            cancellationToken);
        var supplierCustomer = await LoadCustomerAsync(
            document.Context,
            config.SupplierCustomerCode,
            null,
            cancellationToken);
        var deliveryCustomer = await LoadCustomerAsync(
            document.Context,
            document.Detail.Header.CustomerCode,
            document.Metadata.AddressNo,
            cancellationToken);
        var despatchInfo = BuildDespatchInfo(
            BuildCompanyMovementDespatchAdvice(
                document.Detail,
                sourceWarehouse,
                supplierCustomer,
                deliveryCustomer,
                request,
                now,
                eDespatchDocumentNo,
                eDespatchUuid,
                config),
            BuildLocalDocumentId(request),
            deliveryCustomer.Alias,
            deliveryCustomer.TaxNumber,
            deliveryCustomer.DisplayName);
        var serviceResult = await SendToUyumsoftAsync(
            despatchInfo,
            config,
            cancellationToken);

        await TryMarkAsSentAsync(
            document.Context,
            document.TrackedMovements,
            eDespatchDocumentNo,
            eDespatchUuid,
            new SentMovementMetadata(
                request.Plaque,
                null,
                request.DriverNameSurname,
                request.DriverTckn),
            cancellationToken);

        return new SendEDespatchResponse(
            request.DocumentType,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            eDespatchDocumentNo,
            eDespatchUuid,
            serviceResult.ServiceDocumentId,
            serviceResult.ServiceDocumentNumber,
            now,
            config.EndpointUrl);
    }

    private async Task<SendEDespatchResponse> SendInterWarehouseDocumentAsync(
        SendEDespatchRequest request,
        bool isReturn,
        CancellationToken cancellationToken)
    {
        var document = await ResolveInterWarehouseDocumentAsync(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo,
            isReturn,
            cancellationToken);
        EnsureNotAlreadySent(document.TrackedMovements);
        var config = options.Value;
        var now = DateTime.Now;
        var eDespatchDocumentNo = await BuildEDespatchDocumentNoAsync(
            now.Year,
            cancellationToken);
        var eDespatchUuid = Guid.NewGuid().ToString();
        var supplierCustomer = await LoadCustomerAsync(
            document.Context,
            config.SupplierCustomerCode,
            null,
            cancellationToken);
        var sourceWarehouse = await LoadWarehouseAsync(
            document.Context,
            document.Detail.Header.SourceWarehouseNo,
            cancellationToken);
        var targetWarehouse = await LoadWarehouseAsync(
            document.Context,
            document.Detail.Header.TargetWarehouseNo,
            cancellationToken);
        var despatchInfo = BuildDespatchInfo(
            BuildInterWarehouseDespatchAdvice(
                document.Detail,
                supplierCustomer,
                sourceWarehouse,
                targetWarehouse,
                request,
                now,
                eDespatchDocumentNo,
                eDespatchUuid,
                config),
            BuildLocalDocumentId(request),
            null,
            null,
            null);
        var serviceResult = await SendToUyumsoftAsync(
            despatchInfo,
            config,
            cancellationToken);

        await TryMarkAsSentAsync(
            document.Context,
            document.TrackedMovements,
            eDespatchDocumentNo,
            eDespatchUuid,
            new SentMovementMetadata(
                request.Plaque,
                null,
                request.DriverNameSurname,
                request.DriverTckn),
            cancellationToken);

        return new SendEDespatchResponse(
            request.DocumentType,
            document.Detail.Header.DocumentSerie,
            document.Detail.Header.DocumentOrderNo,
            eDespatchDocumentNo,
            eDespatchUuid,
            serviceResult.ServiceDocumentId,
            serviceResult.ServiceDocumentNumber,
            now,
            config.EndpointUrl);
    }

    private async Task<ResolvedCompanyMovementDocument> ResolveCompanyMovementAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CompanyMovementKind movementKind,
        CancellationToken cancellationToken)
    {
        var detailRequest = new CompanyMovementDetailRequest(
            warehouseNo,
            documentSerie.Trim(),
            documentOrderNo);
        var readExecutor = new CompanyMovementDetailQueryExecutor(mikroDbContext);

        try
        {
            var detail = await readExecutor.ExecuteAsync(detailRequest, movementKind, cancellationToken);
            var trackedMovements = await LoadCompanyMovementRowsAsync(
                mikroDbContext,
                detailRequest,
                movementKind,
                cancellationToken);

            return new ResolvedCompanyMovementDocument(
                detail,
                trackedMovements,
                mikroDbContext,
                BuildCompanyMovementMetadata(trackedMovements));
        }
        catch (KeyNotFoundException)
        {
            var writeExecutor = new CompanyMovementDetailQueryExecutor(mikroWriteDbContext);
            var detail = await writeExecutor.ExecuteAsync(detailRequest, movementKind, cancellationToken);
            var trackedMovements = await LoadCompanyMovementRowsAsync(
                mikroWriteDbContext,
                detailRequest,
                movementKind,
                cancellationToken);

            return new ResolvedCompanyMovementDocument(
                detail,
                trackedMovements,
                mikroWriteDbContext,
                BuildCompanyMovementMetadata(trackedMovements));
        }
    }

    private async Task<ResolvedInterWarehouseDocument> ResolveInterWarehouseDocumentAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        bool isReturn,
        CancellationToken cancellationToken)
    {
        var detailRequest = new WarehouseShippingDetailRequest(
            warehouseNo,
            documentSerie.Trim(),
            documentOrderNo);
        var readExecutor = new WarehouseShippingDetailQueryExecutor(mikroDbContext);

        try
        {
            var detail = await readExecutor.ExecuteAsync(
                detailRequest,
                WarehouseShippingDirection.Outgoing,
                isReturn,
                cancellationToken);
            var trackedMovements = await LoadInterWarehouseRowsAsync(
                mikroDbContext,
                detailRequest,
                isReturn,
                cancellationToken);

            return new ResolvedInterWarehouseDocument(
                detail,
                trackedMovements,
                mikroDbContext);
        }
        catch (KeyNotFoundException)
        {
            var writeExecutor = new WarehouseShippingDetailQueryExecutor(mikroWriteDbContext);
            var detail = await writeExecutor.ExecuteAsync(
                detailRequest,
                WarehouseShippingDirection.Outgoing,
                isReturn,
                cancellationToken);
            var trackedMovements = await LoadInterWarehouseRowsAsync(
                mikroWriteDbContext,
                detailRequest,
                isReturn,
                cancellationToken);

            return new ResolvedInterWarehouseDocument(
                detail,
                trackedMovements,
                mikroWriteDbContext);
        }
    }

    private static async Task<List<STOK_HAREKETLERI>> LoadCompanyMovementRowsAsync(
        MikroDbContext dbContext,
        CompanyMovementDetailRequest request,
        CompanyMovementKind movementKind,
        CancellationToken cancellationToken)
    {
        var query = dbContext.STOK_HAREKETLERIs.Where(movement =>
            movement.sth_evrakno_seri == request.DocumentSerie &&
            movement.sth_evrakno_sira == request.DocumentOrderNo);

        query = movementKind switch
        {
            CompanyMovementKind.OutgoingShipment => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),

            CompanyMovementKind.PurchaseReturn => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == request.WarehouseNo),

            CompanyMovementKind.IncomingShipment => query.Where(movement =>
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_giris_depo_no == request.WarehouseNo),

            _ => throw new ArgumentOutOfRangeException(
                nameof(movementKind),
                movementKind,
                "Unsupported company movement kind.")
        };

        var movements = await query
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            throw new KeyNotFoundException("Company movement detail was not found.");
        }

        return movements;
    }

    private static async Task<List<STOK_HAREKETLERI>> LoadInterWarehouseRowsAsync(
        MikroDbContext dbContext,
        WarehouseShippingDetailRequest request,
        bool isReturn,
        CancellationToken cancellationToken)
    {
        var returnType = isReturn ? ReturnMovement : NormalMovement;
        var movements = await dbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == returnType &&
                movement.sth_evrakno_seri == request.DocumentSerie &&
                movement.sth_evrakno_sira == request.DocumentOrderNo &&
                movement.sth_cikis_depo_no == request.WarehouseNo)
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            throw new KeyNotFoundException(
                isReturn
                    ? "Warehouse return detail was not found."
                    : "Inter warehouse shipment detail was not found.");
        }

        return movements;
    }

    private static CompanyMovementMetadata BuildCompanyMovementMetadata(
        IReadOnlyList<STOK_HAREKETLERI> trackedMovements)
    {
        var firstMovement = trackedMovements[0];

        return new CompanyMovementMetadata(
            firstMovement.sth_adres_no ?? 1);
    }

    private async Task<EDespatchCustomerInfo> LoadCustomerAsync(
        MikroDbContext dbContext,
        string customerCode,
        int? preferredAddressNo,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.cari_kod == customerCode,
                cancellationToken);

        if (customer is null)
        {
            throw new KeyNotFoundException($"Customer was not found for e-despatch: {customerCode}");
        }

        var addressNo = preferredAddressNo
            ?? customer.cari_sevk_adres_no
            ?? customer.cari_fatura_adres_no
            ?? 1;
        var address = await dbContext.CARI_HESAP_ADRESLERIs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.adr_cari_kod == customerCode &&
                    item.adr_adres_no == addressNo,
                cancellationToken);
        var taxNumber = NormalizeText(customer.cari_VergiKimlikNo, customer.cari_vdaire_no);

        if (string.IsNullOrWhiteSpace(taxNumber))
        {
            throw new InvalidOperationException(
                $"Customer tax number is required for e-despatch: {customerCode}");
        }

        return new EDespatchCustomerInfo(
            customerCode,
            JoinNonEmpty(customer.cari_unvan1, customer.cari_unvan2),
            taxNumber,
            ResolveTaxSchemeId(taxNumber),
            NormalizeText(customer.cari_vdaire_adi),
            BuildStreet(
                address?.adr_cadde,
                address?.adr_mahalle,
                address?.adr_sokak,
                address?.adr_Apt_No,
                address?.adr_Daire_No),
            NormalizeText(address?.adr_ilce),
            NormalizeText(address?.adr_il),
            NormalizeText(address?.adr_posta_kodu),
            NormalizeText(address?.adr_ulke),
            NormalizeText(address?.adr_tel_no1, customer.cari_CepTel),
            NormalizeText(address?.adr_tel_faxno),
            NormalizeText(customer.cari_EMail),
            NormalizeText(customer.cari_wwwadresi),
            NormalizeText(address?.adr_eirsaliye_alias));
    }

    private static async Task<EDespatchWarehouseInfo> LoadWarehouseAsync(
        MikroDbContext dbContext,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var warehouse = await dbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.dep_no == warehouseNo, cancellationToken);

        if (warehouse is null)
        {
            throw new KeyNotFoundException($"Warehouse was not found for e-despatch: {warehouseNo}");
        }

        return new EDespatchWarehouseInfo(
            warehouseNo,
            NormalizeText(warehouse.dep_adi),
            BuildStreet(
                warehouse.dep_cadde,
                warehouse.dep_mahalle,
                warehouse.dep_sokak,
                warehouse.dep_Apt_No,
                warehouse.dep_Daire_No),
            NormalizeText(warehouse.dep_Ilce),
            NormalizeText(warehouse.dep_Il),
            NormalizeText(warehouse.dep_posta_Kodu),
            NormalizeText(warehouse.dep_Ulke),
            NormalizeText(warehouse.dep_tel_no1),
            NormalizeText(warehouse.dep_tel_faxno),
            NormalizeText(warehouse.dep_yetkili_email));
    }

    private static UyumsoftDespatch.DespatchInfo BuildDespatchInfo(
        XElement despatchAdvice,
        string localDocumentId,
        string? targetCustomerAlias,
        string? targetCustomerTaxNumber,
        string? targetCustomerTitle)
    {
        var despatchInfo = new UyumsoftDespatch.DespatchInfo
        {
            DespatchAdvice = UyumsoftWcfClientHelper.DeserializeUbl<UyumsoftDespatch.DespatchAdviceType>(
                despatchAdvice.ToString(SaveOptions.DisableFormatting),
                "DespatchAdvice",
                DespatchNamespace),
            LocalDocumentId = localDocumentId,
            ExtraInformation = string.Empty
        };

        if (!string.IsNullOrWhiteSpace(targetCustomerTaxNumber) ||
            !string.IsNullOrWhiteSpace(targetCustomerAlias) ||
            !string.IsNullOrWhiteSpace(targetCustomerTitle))
        {
            despatchInfo.TargetCustomer = new UyumsoftDespatch.CustomerInfo
            {
                VknTckn = targetCustomerTaxNumber?.Trim(),
                Alias = targetCustomerAlias?.Trim(),
                Title = targetCustomerTitle?.Trim()
            };
        }

        return despatchInfo;
    }

    private static XElement BuildCompanyMovementDespatchAdvice(
        CompanyMovementDetailDto detail,
        EDespatchWarehouseInfo warehouse,
        EDespatchCustomerInfo supplierCustomer,
        EDespatchCustomerInfo deliveryCustomer,
        SendEDespatchRequest request,
        DateTime issueDateTime,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        EDespatchOptions config)
    {
        var sellerName = JoinNonEmpty(supplierCustomer.DisplayName, warehouse.Name);

        return BuildDespatchAdviceCore(
            issueDateTime,
            eDespatchDocumentNo,
            eDespatchUuid,
            config,
            $"{detail.Header.WarehouseName} / {detail.Header.DocumentSerie} / {detail.Header.DocumentOrderNo}",
            detail.Items.Count,
            BuildSupplierPartyElement(
                "DespatchSupplierParty",
                supplierCustomer,
                null),
            BuildCustomerPartyElement(
                "DeliveryCustomerParty",
                deliveryCustomer,
                null),
            BuildCustomerPartyElement(
                "BuyerCustomerParty",
                deliveryCustomer,
                null),
            BuildSupplierPartyElement(
                "SellerSupplierParty",
                supplierCustomer with { DisplayName = sellerName },
                null),
            BuildCustomerPartyElement(
                "OriginatorCustomerParty",
                deliveryCustomer,
                null),
            BuildShipmentElement(
                issueDateTime,
                deliveryCustomer.ToAddressInfo(config),
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn),
            detail.Items.Select(
                item => BuildDespatchLineElement(
                    item.LineNo + 1,
                    item.StockCode,
                    item.StockName,
                    item.UnitName,
                    item.Quantity))
                .ToArray());
    }

    private static XElement BuildInterWarehouseDespatchAdvice(
        WarehouseShippingDetailDto detail,
        EDespatchCustomerInfo supplierCustomer,
        EDespatchWarehouseInfo sourceWarehouse,
        EDespatchWarehouseInfo targetWarehouse,
        SendEDespatchRequest request,
        DateTime issueDateTime,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        EDespatchOptions config)
    {
        var sourceParty = supplierCustomer with
        {
            DisplayName = JoinNonEmpty(supplierCustomer.DisplayName, sourceWarehouse.Name),
            Street = sourceWarehouse.Street,
            District = sourceWarehouse.District,
            Province = sourceWarehouse.Province,
            PostalCode = sourceWarehouse.PostalCode,
            CountryName = sourceWarehouse.CountryName,
            Telephone = sourceWarehouse.Telephone,
            Fax = sourceWarehouse.Fax,
            Email = sourceWarehouse.Email
        };
        var targetParty = supplierCustomer with
        {
            DisplayName = JoinNonEmpty(supplierCustomer.DisplayName, targetWarehouse.Name),
            Street = targetWarehouse.Street,
            District = targetWarehouse.District,
            Province = targetWarehouse.Province,
            PostalCode = targetWarehouse.PostalCode,
            CountryName = targetWarehouse.CountryName,
            Telephone = targetWarehouse.Telephone,
            Fax = targetWarehouse.Fax,
            Email = targetWarehouse.Email
        };

        return BuildDespatchAdviceCore(
            issueDateTime,
            eDespatchDocumentNo,
            eDespatchUuid,
            config,
            $"{detail.Header.SourceWarehouse} / {detail.Header.DocumentSerie} / {detail.Header.DocumentOrderNo}",
            detail.Items.Count,
            BuildSupplierPartyElement(
                "DespatchSupplierParty",
                sourceParty,
                null),
            BuildCustomerPartyElement(
                "DeliveryCustomerParty",
                targetParty,
                null),
            BuildCustomerPartyElement(
                "BuyerCustomerParty",
                targetParty,
                null),
            BuildSupplierPartyElement(
                "SellerSupplierParty",
                sourceParty with { DisplayName = sourceWarehouse.Name },
                null),
            BuildCustomerPartyElement(
                "OriginatorCustomerParty",
                targetParty with { DisplayName = targetWarehouse.Name },
                null),
            BuildShipmentElement(
                issueDateTime,
                targetWarehouse.ToAddressInfo(config),
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn),
            detail.Items.Select(
                item => BuildDespatchLineElement(
                    item.LineNo + 1,
                    item.StockCode,
                    item.StockName,
                    item.UnitName,
                    item.Quantity))
                .ToArray());
    }

    private static XElement BuildDespatchAdviceCore(
        DateTime issueDateTime,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        EDespatchOptions config,
        string note,
        int lineCount,
        XElement despatchSupplierParty,
        XElement deliveryCustomerParty,
        XElement buyerCustomerParty,
        XElement sellerSupplierParty,
        XElement originatorCustomerParty,
        XElement shipment,
        IReadOnlyCollection<XElement> despatchLines)
    {
        var despatch = XNamespace.Get(DespatchNamespace);
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);

        return new XElement(
            despatch + "DespatchAdvice",
            new XAttribute(XNamespace.Xmlns + "cac", aggregate.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", basic.NamespaceName),
            new XElement(basic + "ProfileID", config.ProfileId),
            new XElement(basic + "ID", eDespatchDocumentNo),
            new XElement(basic + "CopyIndicator", "false"),
            new XElement(basic + "UUID", eDespatchUuid),
            new XElement(basic + "IssueDate", issueDateTime.ToString("yyyy-MM-dd")),
            new XElement(basic + "IssueTime", issueDateTime.ToString("HH:mm:ss")),
            new XElement(basic + "DespatchAdviceTypeCode", config.DespatchAdviceTypeCode),
            new XElement(basic + "Note", note),
            new XElement(basic + "LineCountNumeric", lineCount),
            new XElement(
                aggregate + "OrderReference",
                new XElement(basic + "ID", eDespatchDocumentNo),
                new XElement(basic + "IssueDate", issueDateTime.ToString("yyyy-MM-dd"))),
            despatchSupplierParty,
            deliveryCustomerParty,
            buyerCustomerParty,
            sellerSupplierParty,
            originatorCustomerParty,
            shipment,
            despatchLines);
    }

    private static XElement BuildSupplierPartyElement(
        string elementName,
        EDespatchCustomerInfo partyInfo,
        string? contactName)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var elements = new List<object>
        {
            BuildPartyElement(partyInfo)
        };

        if (!string.IsNullOrWhiteSpace(contactName))
        {
            var contactElement = BuildContactElement("DespatchContact", contactName, null, null, null, null);
            if (contactElement is not null)
            {
                elements.Add(contactElement!);
            }
        }

        return new XElement(aggregate + elementName, elements);
    }

    private static XElement BuildCustomerPartyElement(
        string elementName,
        EDespatchCustomerInfo partyInfo,
        string? contactName)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var elements = new List<object>
        {
            BuildPartyElement(partyInfo)
        };

        if (!string.IsNullOrWhiteSpace(contactName))
        {
            var contactElement = BuildContactElement("DeliveryContact", contactName, null, null, null, null);
            if (contactElement is not null)
            {
                elements.Add(contactElement!);
            }
        }

        return new XElement(aggregate + elementName, elements);
    }

    private static XElement BuildPartyElement(EDespatchCustomerInfo partyInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var partyElements = new List<object>();

        if (!string.IsNullOrWhiteSpace(partyInfo.Website))
        {
            partyElements.Add(new XElement(basic + "WebsiteURI", partyInfo.Website));
        }

        partyElements.Add(
            new XElement(
                aggregate + "PartyIdentification",
                new XElement(
                    basic + "ID",
                    new XAttribute("schemeID", partyInfo.TaxSchemeId),
                    partyInfo.TaxNumber)));
        partyElements.Add(
            new XElement(
                aggregate + "PartyName",
                new XElement(basic + "Name", partyInfo.DisplayName)));

        partyElements.Add(BuildAddressElement("PostalAddress", partyInfo.ToAddressInfo()));

        if (!string.IsNullOrWhiteSpace(partyInfo.TaxOffice))
        {
            partyElements.Add(
                new XElement(
                    aggregate + "PartyTaxScheme",
                    new XElement(
                        aggregate + "TaxScheme",
                        new XElement(basic + "Name", partyInfo.TaxOffice))));
        }

        var contactElement = BuildContactElement(
            "Contact",
            null,
            partyInfo.Telephone,
            partyInfo.Fax,
            partyInfo.Email,
            null);
        if (contactElement is not null)
        {
            partyElements.Add(contactElement!);
        }

        return new XElement(aggregate + "Party", partyElements);
    }

    private static XElement BuildShipmentElement(
        DateTime issueDateTime,
        EDespatchAddressInfo deliveryAddress,
        string? plaque,
        string? driverNameSurname,
        string? driverTckn)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var shipmentElements = new List<object>
        {
            new XElement(basic + "ID", "1")
        };
        var shipmentStage = BuildShipmentStageElement(plaque, driverNameSurname, driverTckn);
        if (shipmentStage is not null)
        {
            shipmentElements.Add(shipmentStage!);
        }

        shipmentElements.Add(
            new XElement(
                aggregate + "Delivery",
                new XElement(basic + "ID", "1"),
                BuildAddressElement("DeliveryAddress", deliveryAddress),
                new XElement(
                    aggregate + "Despatch",
                    new XElement(basic + "ID", "1"),
                    new XElement(basic + "ActualDespatchDate", issueDateTime.ToString("yyyy-MM-dd")),
                    new XElement(basic + "ActualDespatchTime", issueDateTime.ToString("HH:mm:ss")))));

        return new XElement(aggregate + "Shipment", shipmentElements);
    }

    private static XElement? BuildShipmentStageElement(
        string? plaque,
        string? driverNameSurname,
        string? driverTckn)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var hasPlaque = !string.IsNullOrWhiteSpace(plaque);
        var hasDriver = !string.IsNullOrWhiteSpace(driverNameSurname);

        if (!hasPlaque && !hasDriver)
        {
            return null;
        }

        var elements = new List<object>();

        if (hasPlaque)
        {
            elements.Add(
                new XElement(
                    aggregate + "TransportMeans",
                    new XElement(
                        aggregate + "RoadTransport",
                        new XElement(
                            basic + "LicensePlateID",
                            new XAttribute("schemeID", "PLAKA"),
                            plaque!.Trim()))));
        }

        if (hasDriver)
        {
            var (firstName, familyName) = SplitPersonName(driverNameSurname!);
            var driverElements = new List<object>
            {
                new XElement(basic + "FirstName", firstName)
            };

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                driverElements.Add(new XElement(basic + "FamilyName", familyName));
            }

            if (!string.IsNullOrWhiteSpace(driverTckn))
            {
                driverElements.Add(
                    new XElement(
                        basic + "NationalityID",
                        new XAttribute("schemeID", "TCKN"),
                        driverTckn!.Trim()));
            }

            elements.Add(new XElement(aggregate + "DriverPerson", driverElements));
        }

        return new XElement(aggregate + "ShipmentStage", elements);
    }

    private static XElement BuildDespatchLineElement(
        int lineNo,
        string stockCode,
        string stockName,
        string unitName,
        double quantity)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var unitCode = ResolveUnitCode(unitName);

        return new XElement(
            aggregate + "DespatchLine",
            new XElement(basic + "ID", lineNo),
            new XElement(basic + "Note", lineNo.ToString()),
            new XElement(
                basic + "DeliveredQuantity",
                new XAttribute("unitCode", unitCode),
                quantity),
            new XElement(
                basic + "OutstandingQuantity",
                new XAttribute("unitCode", unitCode),
                quantity),
            new XElement(basic + "OutstandingReason", "Stok Yok"),
            new XElement(
                basic + "OversupplyQuantity",
                new XAttribute("unitCode", unitCode),
                quantity),
            new XElement(
                aggregate + "OrderLineReference",
                new XElement(basic + "LineID", lineNo)),
            new XElement(
                aggregate + "Item",
                new XElement(basic + "Description", stockCode),
                new XElement(basic + "Name", stockName)));
    }

    private static XElement BuildAddressElement(
        string elementName,
        EDespatchAddressInfo addressInfo)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var elements = new List<object>();

        if (!string.IsNullOrWhiteSpace(addressInfo.Street))
        {
            elements.Add(new XElement(basic + "StreetName", addressInfo.Street));
        }

        if (!string.IsNullOrWhiteSpace(addressInfo.District))
        {
            elements.Add(new XElement(basic + "CitySubdivisionName", addressInfo.District));
        }

        if (!string.IsNullOrWhiteSpace(addressInfo.Province))
        {
            elements.Add(new XElement(basic + "CityName", addressInfo.Province));
        }

        if (!string.IsNullOrWhiteSpace(addressInfo.PostalCode))
        {
            elements.Add(new XElement(basic + "PostalZone", addressInfo.PostalCode));
        }

        elements.Add(
            new XElement(
                aggregate + "Country",
                new XElement(basic + "IdentificationCode", addressInfo.CountryCode),
                new XElement(basic + "Name", addressInfo.CountryName)));

        return new XElement(aggregate + elementName, elements);
    }

    private static XElement? BuildContactElement(
        string elementName,
        string? name,
        string? telephone,
        string? telefax,
        string? email,
        string? note)
    {
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);
        var elements = new List<object>();

        if (!string.IsNullOrWhiteSpace(name))
        {
            elements.Add(new XElement(basic + "Name", name.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(telephone))
        {
            elements.Add(new XElement(basic + "Telephone", telephone.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(telefax))
        {
            elements.Add(new XElement(basic + "Telefax", telefax.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            elements.Add(new XElement(basic + "ElectronicMail", email.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            elements.Add(new XElement(basic + "Note", note.Trim()));
        }

        return elements.Count == 0 ? null : new XElement(aggregate + elementName, elements);
    }

    private static async Task<string> ResolveOutboxDespatchIdAsync(
        EDespatchOptions config,
        string eDespatchDocumentNo,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(config.EndpointUrl);

        try
        {
            var response = await client.GetOutboxDespatchListAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                new UyumsoftDespatch.OutboxDespatchListQueryModel
                {
                    PageIndex = 0,
                    PageSize = 10,
                    IsOnlyNewReceiptAdvice = false,
                    DespatchNumbers = [eDespatchDocumentNo]
                });

            EnsureSucceeded(response, "e-despatch outbox list");

            var items = response.Value?.Items ?? [];
            var matchedItem = items.FirstOrDefault(item =>
                string.Equals(
                    item.DespatchNumber?.Trim(),
                    eDespatchDocumentNo,
                    StringComparison.OrdinalIgnoreCase));

            if (matchedItem is null && items.Length == 1)
            {
                matchedItem = items[0];
            }

            if (matchedItem is null)
            {
                throw new InvalidOperationException(
                    $"Could not find the sent e-despatch in Uyumsoft for document number {eDespatchDocumentNo}.");
            }

            if (string.IsNullOrWhiteSpace(matchedItem.DespatchId))
            {
                throw new InvalidOperationException(
                    $"Uyumsoft e-despatch list response does not contain a document id for {eDespatchDocumentNo}.");
            }

            return matchedItem.DespatchId.Trim();
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<ServiceSendResult> SendToUyumsoftAsync(
        UyumsoftDespatch.DespatchInfo despatchInfo,
        EDespatchOptions config,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(config.EndpointUrl);

        try
        {
            var response = await client.SendDespatchAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                [despatchInfo]);

            EnsureSucceeded(response, "e-despatch");

            var identity = response.Value?.FirstOrDefault()
                           ?? throw new InvalidOperationException(
                               "Uyumsoft e-despatch response does not contain a document identity.");

            return new ServiceSendResult(
                identity.Id?.Trim() ?? string.Empty,
                identity.Number?.Trim() ?? string.Empty);
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<byte[]> GetOutboxDespatchPdfAsync(
        EDespatchOptions config,
        string despatchId,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(config.EndpointUrl);

        try
        {
            var response = await client.GetOutboxDespatchPdfAsync(
                UyumsoftWcfClientHelper.CreateDespatchUserInfo(ToEndpointOptions(config)),
                despatchId);

            EnsureSucceeded(response, "e-despatch PDF");

            var matchedItem = response.Value?.Items?.FirstOrDefault(item =>
            string.Equals(
                item.DespatchId?.Trim(),
                despatchId,
                StringComparison.OrdinalIgnoreCase));

            if (matchedItem is null && response.Value?.Items?.Length == 1)
            {
                matchedItem = response.Value.Items[0];
            }

            if (matchedItem?.Data is null || matchedItem.Data.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Uyumsoft e-despatch PDF response returned empty data for document id {despatchId}.");
            }

            return matchedItem.Data;
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static void EnsureSucceeded(UyumsoftDespatch.Response response, string operationName)
    {
        if (!response.IsSucceded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Message)
                    ? $"Uyumsoft {operationName} service rejected the request."
                    : response.Message);
        }
    }

    private static UyumsoftServiceEndpointOptions ToEndpointOptions(EDespatchOptions config) =>
        new(
            config.EndpointUrl,
            string.Empty,
            config.Username,
            config.Password,
            "IBasicDespatchIntegration");

    private async Task TryMarkAsSentAsync(
        MikroDbContext context,
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements,
        string eDespatchDocumentNo,
        string eDespatchUuid,
        SentMovementMetadata metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.Now;

            foreach (var movement in trackedMovements)
            {
                movement.sth_kilitli = true;
                movement.sth_lastup_user = MikroUserNo;
                movement.sth_lastup_date = now;
                movement.sth_belge_no = Truncate(eDespatchDocumentNo, 50);
                movement.sth_aciklama = Truncate(eDespatchUuid, 50);

                if (!string.IsNullOrWhiteSpace(metadata.Plaque))
                {
                    movement.sth_HareketGrupKodu1 = Truncate(metadata.Plaque.Trim(), 25);
                }

                if (!string.IsNullOrWhiteSpace(metadata.Deliverer))
                {
                    movement.sth_HareketGrupKodu2 = Truncate(metadata.Deliverer.Trim(), 25);
                }

                if (!string.IsNullOrWhiteSpace(metadata.Receiver))
                {
                    movement.sth_HareketGrupKodu3 = Truncate(metadata.Receiver.Trim(), 25);
                }

                if (!string.IsNullOrWhiteSpace(metadata.DriverTckn))
                {
                    movement.sth_ismerkezi_kodu = Truncate(metadata.DriverTckn.Trim(), 25);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "E-despatch was sent but local Mikro metadata could not be updated.");
        }
    }

    private async Task<string> BuildEDespatchDocumentNoAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var documentNo = await GetLatestDocumentNoAsync(year, cancellationToken);

        return $"{CommonEDespatchDocumentPrefix}{year}{documentNo}";
    }

    private async Task<IAsyncDisposable> AcquireDocumentNumberLockAsync(
        CancellationToken cancellationToken)
    {
        await LocalDocumentNumberLock.WaitAsync(cancellationToken);

        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        try
        {
            if (closeConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = @lockTimeout;
                SELECT @result;
                """;
            command.CommandTimeout = (DocumentNumberLockTimeoutMilliseconds / 1000) + 10;
            AddParameter(command, "@resource", DbType.String, DocumentNumberLockResource);
            AddParameter(
                command,
                "@lockTimeout",
                DbType.Int32,
                DocumentNumberLockTimeoutMilliseconds);

            var result = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken));

            if (result < 0)
            {
                throw new TimeoutException(
                    $"E-despatch document number lock could not be acquired. SQL result: {result}.");
            }

            return new DocumentNumberLockLease(
                connection,
                closeConnection,
                LocalDocumentNumberLock,
                logger);
        }
        catch
        {
            if (closeConnection && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }

            LocalDocumentNumberLock.Release();
            throw;
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void EnsureNotAlreadySent(
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements)
    {
        var sentMovement = trackedMovements.FirstOrDefault(movement =>
        {
            var documentNo = movement.sth_belge_no;

            return movement.sth_kilitli == true &&
                   !string.IsNullOrWhiteSpace(documentNo) &&
                   documentNo.StartsWith(
                       CommonEDespatchDocumentPrefix,
                       StringComparison.OrdinalIgnoreCase) &&
                   Guid.TryParse(movement.sth_aciklama, out _);
        });

        if (sentMovement is not null)
        {
            throw new InvalidOperationException(
                $"E-despatch has already been sent with document number {sentMovement.sth_belge_no}.");
        }
    }

    private async Task<string> GetLatestDocumentNoAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var prefixWithYear = $"{CommonEDespatchDocumentPrefix}{year}";
        var latestDocumentNo = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_belge_no != null &&
                movement.sth_belge_no.StartsWith(prefixWithYear) &&
                movement.sth_belge_no.Length == prefixWithYear.Length + 9)
            .OrderByDescending(movement => movement.sth_belge_no)
            .Select(movement => movement.sth_belge_no!)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;

        if (!string.IsNullOrWhiteSpace(latestDocumentNo))
        {
            var latestSequenceText = latestDocumentNo[prefixWithYear.Length..];

            if (!int.TryParse(latestSequenceText, out var latestSequence))
            {
                throw new InvalidOperationException(
                    $"Could not parse the latest e-despatch document number: {latestDocumentNo}");
            }

            nextSequence = latestSequence + 1;
        }

        if (nextSequence > 999_999_999)
        {
            throw new InvalidOperationException(
                $"E-despatch document sequence exceeded the supported limit for {prefixWithYear}.");
        }

        return nextSequence.ToString("D9");
    }

    private static string BuildLocalDocumentId(SendEDespatchRequest request) =>
        $"{request.DocumentType}:{request.WarehouseNo}:{request.DocumentSerie.Trim()}:{request.DocumentOrderNo}";

    private static void Validate(SendEDespatchRequest request)
    {
        ValidateDocumentIdentity(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

        if (string.IsNullOrWhiteSpace(request.Plaque))
        {
            throw new ArgumentException(
                "Plaque is required for e-despatch.",
                nameof(request.Plaque));
        }

        if (string.IsNullOrWhiteSpace(request.DriverNameSurname))
        {
            throw new ArgumentException(
                "Driver name surname is required for e-despatch.",
                nameof(request.DriverNameSurname));
        }

        if (string.IsNullOrWhiteSpace(request.DriverTckn))
        {
            throw new ArgumentException(
                "Driver TCKN is required for e-despatch.",
                nameof(request.DriverTckn));
        }
    }

    private static void Validate(GetEDespatchPdfRequest request) =>
        ValidateDocumentIdentity(
            request.WarehouseNo,
            request.DocumentSerie,
            request.DocumentOrderNo);

    private static void ValidateDocumentIdentity(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException(
                "Warehouse no must be greater than zero.",
                nameof(warehouseNo));
        }

        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            throw new ArgumentException(
                "Document serie is required.",
                nameof(documentSerie));
        }

        if (documentOrderNo < 0)
        {
            throw new ArgumentException(
                "Document order no can not be negative.",
                nameof(documentOrderNo));
        }
    }

    private static void ValidateConfiguration(EDespatchOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EndpointUrl))
        {
            throw new InvalidOperationException(
                "EDespatch:EndpointUrl configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new InvalidOperationException(
                "EDespatch:Username configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "EDespatch:Password configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SupplierCustomerCode))
        {
            throw new InvalidOperationException(
                "EDespatch:SupplierCustomerCode configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ProfileId))
        {
            throw new InvalidOperationException(
                "EDespatch:ProfileId configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.DespatchAdviceTypeCode))
        {
            throw new InvalidOperationException(
                "EDespatch:DespatchAdviceTypeCode configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.CountryCode))
        {
            throw new InvalidOperationException(
                "EDespatch:CountryCode configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(options.CountryName))
        {
            throw new InvalidOperationException(
                "EDespatch:CountryName configuration is required.");
        }
    }

    private static string ResolveTaxSchemeId(string taxNumber) =>
        taxNumber.Length == 11 ? "TCKN" : "VKN";

    private static string ResolveUnitCode(string unitName)
    {
        var normalizedUnitName = unitName.Trim().ToUpperInvariant();

        return normalizedUnitName switch
        {
            "ADET" => "NIU",
            "AD" => "NIU",
            "KG" => "KGM",
            "KILOGRAM" => "KGM",
            "PAKET" => "PA",
            "PK" => "PA",
            "LT" => "LTR",
            "LITRE" => "LTR",
            _ => "NIU"
        };
    }

    private static (string FirstName, string? FamilyName) SplitPersonName(string value)
    {
        var parts = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => (string.Empty, null),
            1 => (parts[0], null),
            _ => (string.Join(" ", parts[..^1]), parts[^1])
        };
    }

    private static string BuildStreet(params string?[] values) =>
        JoinNonEmpty(values);

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

    private static string NormalizeText(params string?[] values) =>
        values
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim()
        ?? string.Empty;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static SentDespatchInfo ExtractSentDespatchInfo(
        IReadOnlyCollection<STOK_HAREKETLERI> trackedMovements)
    {
        var firstMovement = trackedMovements.First();
        var eDespatchDocumentNo = firstMovement.sth_belge_no?.Trim() ?? string.Empty;
        var eDespatchUuid = firstMovement.sth_aciklama?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eDespatchDocumentNo) ||
            !Guid.TryParse(eDespatchUuid, out _))
        {
            throw new InvalidOperationException(
                "E-despatch PDF is not available because the document has not been sent yet.");
        }

        return new SentDespatchInfo(
            eDespatchDocumentNo,
            eDespatchUuid);
    }

    private sealed record CompanyMovementMetadata(int AddressNo);

    private sealed record ResolvedCompanyMovementDocument(
        CompanyMovementDetailDto Detail,
        IReadOnlyCollection<STOK_HAREKETLERI> TrackedMovements,
        MikroDbContext Context,
        CompanyMovementMetadata Metadata);

    private sealed record ResolvedInterWarehouseDocument(
        WarehouseShippingDetailDto Detail,
        IReadOnlyCollection<STOK_HAREKETLERI> TrackedMovements,
        MikroDbContext Context);

    private sealed record EDespatchAddressInfo(
        string Street,
        string District,
        string Province,
        string PostalCode,
        string CountryCode,
        string CountryName);

    private sealed record EDespatchCustomerInfo(
        string CustomerCode,
        string DisplayName,
        string TaxNumber,
        string TaxSchemeId,
        string TaxOffice,
        string Street,
        string District,
        string Province,
        string PostalCode,
        string CountryName,
        string Telephone,
        string Fax,
        string Email,
        string Website,
        string Alias)
    {
        public EDespatchAddressInfo ToAddressInfo() =>
            new(
                Street,
                District,
                Province,
                PostalCode,
                "TR",
                string.IsNullOrWhiteSpace(CountryName) ? "TURKIYE" : CountryName);

        public EDespatchAddressInfo ToAddressInfo(EDespatchOptions options) =>
            new(
                Street,
                District,
                Province,
                PostalCode,
                options.CountryCode,
                string.IsNullOrWhiteSpace(CountryName) ? options.CountryName : CountryName);
    }

    private sealed record EDespatchWarehouseInfo(
        int WarehouseNo,
        string Name,
        string Street,
        string District,
        string Province,
        string PostalCode,
        string CountryName,
        string Telephone,
        string Fax,
        string Email)
    {
        public EDespatchAddressInfo ToAddressInfo(EDespatchOptions options) =>
            new(
                Street,
                District,
                Province,
                PostalCode,
                options.CountryCode,
                string.IsNullOrWhiteSpace(CountryName) ? options.CountryName : CountryName);
    }

    private sealed record ServiceSendResult(
        string ServiceDocumentId,
        string ServiceDocumentNumber);

    private sealed record SentDespatchInfo(
        string EDespatchDocumentNo,
        string EDespatchUuid);

    private sealed record SentMovementMetadata(
        string? Plaque,
        string? Deliverer,
        string? Receiver,
        string? DriverTckn);

    private sealed class DocumentNumberLockLease(
        DbConnection connection,
        bool closeConnection,
        SemaphoreSlim localLock,
        ILogger<EDespatchService> leaseLogger)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (connection.State == ConnectionState.Open)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = """
                        EXEC sys.sp_releaseapplock
                            @Resource = @resource,
                            @LockOwner = 'Session';
                        """;
                    AddParameter(command, "@resource", DbType.String, DocumentNumberLockResource);
                    await command.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            catch (Exception exception)
            {
                leaseLogger.LogWarning(
                    exception,
                    "E-despatch document number SQL application lock could not be released explicitly.");
            }
            finally
            {
                if (closeConnection && connection.State != ConnectionState.Closed)
                {
                    await connection.CloseAsync();
                }

                localLock.Release();
            }
        }
    }
}
