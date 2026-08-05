using System.Globalization;
using System.ServiceModel;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using Microsoft.Extensions.Options;
using AxataInbound = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main.WMSServiceCore.Models.Inbounds;
using AxataMain = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main;
using AxataModels = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main.WMSServiceCore.Models;
using AxataOutbound = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main.WMSServiceCore.Models.Outbound;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationLiveTransportService(
    IOptionsMonitor<AxataSynchronizationOptions> options)
{
    private const string DefaultBranchCode = "01";
    private const string DefaultExternalChannel = "01";
    private const string DefaultActionCode = "01";
    private const string DefaultFormType = "0";
    private const string DefaultAddressCode = "01";
    private const string DefaultOutboundOperationName = "addOutboundOrder";
    private const string DefaultInboundOperationName = "addInboundOrder";
    private const string DefaultOutboundMovementCode = "C01";
    private const string DefaultCustomerOutboundMovementCode = "C02";
    private const string DefaultInboundMovementCode = "G01";
    private const string DefaultWarehouseInboundMovementCode = "G02";
    private const string DefaultFirmMasterOperationName = "addFirmMaster";
    private const string DefaultFirmAddressOperationName = "addFirmAddress";

    public async Task<AxataLiveDispatchResult> DispatchFirmMasterAsync(
        AxataSynchronizationTaskExecutionContext context,
        FirmMasterPayloadItem item,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var masterOperationName = ResolveOperationName(context.Definition.Code, DefaultFirmMasterOperationName);
        var addressOperationName = DefaultFirmAddressOperationName;
        var payload = BuildFirmMasterPayload(item);
        var addressPayload = BuildFirmAddressPayload(item);
        var masterResponse = await AddFirmMasterAsync(
            configuration,
            masterOperationName,
            payload,
            cancellationToken);
        var masterServiceResponse = ToServiceResponse(masterResponse.State, masterResponse.Message, masterOperationName);

        if (!masterServiceResponse.IsSuccess)
        {
            return new AxataLiveDispatchResult(
                masterOperationName,
                configuration.MainEndpointUrl,
                false,
                masterServiceResponse.State,
                masterServiceResponse.Message,
                AxataSynchronizationPayloadFactory.Serialize(payload),
                AxataSynchronizationPayloadFactory.Serialize(payload),
                SerializeResponsePayload(masterOperationName, masterResponse.State, masterResponse.Message, masterResponse.ProcessResults),
                [$"Firma master kaydi `{masterOperationName}` ile gonderildi ancak AXATA basarisiz dondu."]);
        }

        var addressResponse = await AddFirmAddressAsync(
            configuration,
            addressOperationName,
            addressPayload,
            cancellationToken);
        var addressServiceResponse = ToServiceResponse(addressResponse.State, addressResponse.Message, addressOperationName);
        var isSuccess = masterServiceResponse.IsSuccess && addressServiceResponse.IsSuccess;

        return new AxataLiveDispatchResult(
            $"{masterOperationName}+{addressOperationName}",
            configuration.MainEndpointUrl,
            isSuccess,
            isSuccess ? addressServiceResponse.State : addressServiceResponse.State,
            isSuccess
                ? $"{masterServiceResponse.Message} / {addressServiceResponse.Message}"
                : addressServiceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(new { master = payload, address = addressPayload }),
            AxataSynchronizationPayloadFactory.Serialize(new { master = payload, address = addressPayload }),
            AxataSynchronizationPayloadFactory.Serialize(new
            {
                master = new
                {
                    operationName = masterOperationName,
                    masterResponse.State,
                    masterResponse.Message,
                    masterResponse.ProcessResults
                },
                address = new
                {
                    operationName = addressOperationName,
                    addressResponse.State,
                    addressResponse.Message,
                    addressResponse.ProcessResults
                }
            }),
            [
                $"Firma master `{masterOperationName}` ve adres `{addressOperationName}` WCF client ile gonderildi.",
                $"Cari kodu {item.CustomerCode} kullanildi."
            ]);
    }

    public async Task<AxataLiveDispatchResult> DispatchWarehouseOrderAsync(
        AxataSynchronizationTaskExecutionContext context,
        WarehouseOrderDetailDto detail,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var operationName = ResolveOperationName(context.Definition.Code, DefaultOutboundOperationName);
        var payload = BuildOutboundOrderPayload(detail);
        var response = await AddOutboundOrderAsync(
            configuration,
            operationName,
            payload,
            cancellationToken);
        var serviceResponse = ToServiceResponse(response.State, response.Message, operationName);

        return new AxataLiveDispatchResult(
            operationName,
            configuration.MainEndpointUrl,
            serviceResponse.IsSuccess,
            serviceResponse.State,
            serviceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(payload),
            AxataSynchronizationPayloadFactory.Serialize(payload),
            SerializeResponsePayload(operationName, response.State, response.Message, response.ProcessResults),
            [
                $"Task icin yapilandirilmis AXATA operasyonu `{operationName}` WCF client ile gonderildi.",
                $"Hareket kodu {payload.MovementCode} ve belge {payload.DocumentNumber} kullanildi."
            ]);
    }

    public async Task<AxataLiveDispatchResult> DispatchCustomerOutboundOrderAsync(
        AxataSynchronizationTaskExecutionContext context,
        CompanyOrderDetailDto detail,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var operationName = ResolveOperationName(context.Definition.Code, DefaultOutboundOperationName);
        var payload = BuildCustomerOutboundOrderPayload(detail);
        var response = await AddOutboundOrderAsync(
            configuration,
            operationName,
            payload,
            cancellationToken);
        var serviceResponse = ToServiceResponse(response.State, response.Message, operationName);

        return new AxataLiveDispatchResult(
            operationName,
            configuration.MainEndpointUrl,
            serviceResponse.IsSuccess,
            serviceResponse.State,
            serviceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(payload),
            AxataSynchronizationPayloadFactory.Serialize(payload),
            SerializeResponsePayload(operationName, response.State, response.Message, response.ProcessResults),
            [
                $"Task icin yapilandirilmis AXATA operasyonu `{operationName}` WCF client ile gonderildi.",
                $"Hareket kodu {payload.MovementCode} ve belge {payload.DocumentNumber} kullanildi.",
                $"C02 alici/musteri kodu {detail.Header.CustomerCode} olarak gonderildi."
            ]);
    }

    public async Task<AxataLiveDispatchResult> DispatchCompanyReceivingAsync(
        AxataSynchronizationTaskExecutionContext context,
        CompanyMovementDetailDto detail,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var operationName = ResolveOperationName(context.Definition.Code, DefaultInboundOperationName);
        var payload = BuildInboundOrderPayload(detail);
        var response = await AddInboundOrderAsync(
            configuration,
            operationName,
            payload,
            cancellationToken);
        var serviceResponse = ToServiceResponse(response.State, response.Message, operationName);

        return new AxataLiveDispatchResult(
            operationName,
            configuration.MainEndpointUrl,
            serviceResponse.IsSuccess,
            serviceResponse.State,
            serviceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(payload),
            AxataSynchronizationPayloadFactory.Serialize(payload),
            SerializeResponsePayload(operationName, response.State, response.Message, response.ProcessResults),
            [
                $"Task icin yapilandirilmis AXATA operasyonu `{operationName}` WCF client ile gonderildi.",
                $"Hareket kodu {payload.MovementCode} ve belge {payload.DocumentNumber} kullanildi."
            ]);
    }

    public async Task<AxataLiveDispatchResult> DispatchWarehouseInboundOrderAsync(
        AxataSynchronizationTaskExecutionContext context,
        WarehouseOrderDetailDto detail,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var operationName = ResolveOperationName(context.Definition.Code, DefaultInboundOperationName);
        var payload = BuildWarehouseInboundOrderPayload(detail);
        var response = await AddInboundOrderAsync(
            configuration,
            operationName,
            payload,
            cancellationToken);
        var serviceResponse = ToServiceResponse(response.State, response.Message, operationName);

        return new AxataLiveDispatchResult(
            operationName,
            configuration.MainEndpointUrl,
            serviceResponse.IsSuccess,
            serviceResponse.State,
            serviceResponse.Message,
            AxataSynchronizationPayloadFactory.Serialize(payload),
            AxataSynchronizationPayloadFactory.Serialize(payload),
            SerializeResponsePayload(operationName, response.State, response.Message, response.ProcessResults),
            [
                $"Task icin yapilandirilmis AXATA operasyonu `{operationName}` WCF client ile gonderildi.",
                $"Hareket kodu {payload.MovementCode} ve belge {payload.DocumentNumber} kullanildi.",
                $"G02 firma/depo kodu kaynak depo {detail.Header.OutWarehouseNo} olarak gonderildi."
            ]);
    }

    private AxataSynchronizationLiveTransportConfiguration GetRequiredConfiguration()
    {
        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.MainEndpointUrl))
        {
            throw new InvalidOperationException("AXATA main endpoint url is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username))
        {
            throw new InvalidOperationException("AXATA username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException(
                "AXATA password is not configured. Live dispatch requires AxataSynchronization:Password.");
        }

        return new AxataSynchronizationLiveTransportConfiguration(
            currentOptions.MainEndpointUrl,
            currentOptions.Username,
            currentOptions.Password);
    }

    private static AxataLegacyOutboundOrderPayload BuildOutboundOrderPayload(WarehouseOrderDetailDto detail)
    {
        var documentNumber = BuildDocumentNumber(detail.Header.DocumentSerie, detail.Header.DocumentOrderNo);
        var movementCode = DefaultOutboundMovementCode;
        var depotCode = detail.Header.OutWarehouseNo.ToString(CultureInfo.InvariantCulture);

        return new AxataLegacyOutboundOrderPayload(
            documentNumber,
            movementCode,
            new AxataLegacyOutboundOrderMaster(
                DefaultBranchCode,
                documentNumber,
                DefaultExternalChannel,
                detail.Header.OutWarehouseNo.ToString(CultureInfo.InvariantCulture),
                detail.Header.InWarehouseNo.ToString(CultureInfo.InvariantCulture),
                DefaultAddressCode,
                DefaultFormType,
                depotCode,
                movementCode,
                movementCode),
            detail.Items
                .OrderBy(item => item.LineNo)
                .Select(item => new AxataLegacyOutboundOrderLine(
                    DefaultBranchCode,
                    documentNumber,
                    item.LineNo,
                    item.StockCode,
                    item.RemainingQuantity > 0d ? item.RemainingQuantity : item.Quantity,
                    depotCode))
                .ToArray());
    }

    private static AxataLegacyOutboundOrderPayload BuildCustomerOutboundOrderPayload(CompanyOrderDetailDto detail)
    {
        var documentNumber = BuildDocumentNumber(detail.Header.DocumentSerie, detail.Header.DocumentOrderNo);
        var movementCode = DefaultCustomerOutboundMovementCode;
        var depotCode = DefaultBranchCode;

        return new AxataLegacyOutboundOrderPayload(
            documentNumber,
            movementCode,
            new AxataLegacyOutboundOrderMaster(
                DefaultBranchCode,
                documentNumber,
                DefaultExternalChannel,
                detail.Header.CustomerCode,
                detail.Header.CustomerCode,
                DefaultAddressCode,
                DefaultFormType,
                string.Empty,
                movementCode,
                movementCode),
            detail.Items
                .OrderBy(item => item.LineNo)
                .Where(item => item.RemainingQuantity > 0d || item.Quantity > 0d)
                .Select(item => new AxataLegacyOutboundOrderLine(
                    DefaultBranchCode,
                    documentNumber,
                    item.LineNo,
                    item.StockCode,
                    item.RemainingQuantity > 0d ? item.RemainingQuantity : item.Quantity,
                    depotCode))
                .ToArray());
    }

    private static AxataFirmMasterPayload BuildFirmMasterPayload(FirmMasterPayloadItem item) =>
        new(
            new AxataFirmMasterFields(
                DefaultBranchCode,
                item.CustomerCode,
                2m,
                Truncate(item.DisplayName, 20),
                Truncate(item.DisplayName, 100),
                Truncate(item.AddressLine1, 100),
                Truncate(item.AddressLine2, 100),
                string.Empty,
                Truncate(item.TaxOfficeNo, 25),
                Truncate(item.TaxNumber, 25),
                Truncate(item.Email, 100),
                Truncate(item.MobilePhone, 25)));

    private static AxataFirmAddressPayload BuildFirmAddressPayload(FirmMasterPayloadItem item) =>
        new(
            new AxataFirmAddressFields(
                DefaultBranchCode,
                DefaultAddressCode,
                item.CustomerCode,
                Truncate(item.AddressLine1, 100),
                Truncate(item.AddressLine2, 100),
                string.Empty));

    private static AxataLegacyInboundOrderPayload BuildInboundOrderPayload(CompanyMovementDetailDto detail)
    {
        var documentNumber = BuildDocumentNumber(detail.Header.DocumentSerie, detail.Header.DocumentOrderNo);
        var movementCode = DefaultInboundMovementCode;
        var orderDate = (detail.Header.DocumentDate ?? detail.Header.MovementCreateDate).Date;
        var deliveryDate = (detail.Header.MovementDate ?? detail.Header.DocumentDate ?? detail.Header.MovementCreateDate).Date;

        return new AxataLegacyInboundOrderPayload(
            documentNumber,
            movementCode,
            new AxataLegacyInboundOrderMaster(
                DefaultBranchCode,
                movementCode,
                documentNumber,
                DefaultActionCode,
                detail.Header.CustomerCode,
                orderDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                deliveryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            detail.Items
                .OrderBy(item => item.LineNo)
                .Select(item => new AxataLegacyInboundOrderLine(
                    DefaultBranchCode,
                    movementCode,
                    documentNumber,
                    DefaultActionCode,
                    item.LineNo,
                    item.StockCode,
                    detail.Header.CustomerCode,
                    item.Quantity,
                    orderDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    deliveryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)))
                .ToArray());
    }

    private static AxataLegacyInboundOrderPayload BuildWarehouseInboundOrderPayload(WarehouseOrderDetailDto detail)
    {
        var documentNumber = BuildDocumentNumber(detail.Header.DocumentSerie, detail.Header.DocumentOrderNo);
        var movementCode = DefaultWarehouseInboundMovementCode;
        var firmCode = detail.Header.OutWarehouseNo.ToString(CultureInfo.InvariantCulture);
        var orderDate = detail.Header.DocumentDate.Date;
        var deliveryDate = (detail.Header.DeliveryDate ?? detail.Header.DocumentDate).Date;

        return new AxataLegacyInboundOrderPayload(
            documentNumber,
            movementCode,
            new AxataLegacyInboundOrderMaster(
                DefaultBranchCode,
                movementCode,
                documentNumber,
                DefaultActionCode,
                firmCode,
                orderDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                deliveryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)),
            detail.Items
                .OrderBy(item => item.LineNo)
                .Select(item => new AxataLegacyInboundOrderLine(
                    DefaultBranchCode,
                    movementCode,
                    documentNumber,
                    DefaultActionCode,
                    item.LineNo,
                    item.StockCode,
                    firmCode,
                    item.RemainingQuantity > 0d ? item.RemainingQuantity : item.Quantity,
                    orderDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    deliveryDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)))
                .ToArray());
    }

    private static async Task<AxataWcfDispatchResponse> AddOutboundOrderAsync(
        AxataSynchronizationLiveTransportConfiguration configuration,
        string operationName,
        AxataLegacyOutboundOrderPayload payload,
        CancellationToken cancellationToken)
    {
        var client = CreateMainClient(configuration.MainEndpointUrl);
        var order = ToWcfOutboundOrder(payload);

        try
        {
            if (operationName.Equals("addOutboundOrderV2", StringComparison.OrdinalIgnoreCase))
            {
                var response = await client
                    .addOutboundOrderV2Async(
                        new AxataMain.addOutboundOrder_Req1(
                            configuration.Username,
                            configuration.Password,
                            [order]))
                    .WaitAsync(cancellationToken);

                CloseWcfClient(client);
                return new AxataWcfDispatchResponse(response.state, response.message, response.processResult);
            }

            if (operationName.Equals(DefaultOutboundOperationName, StringComparison.OrdinalIgnoreCase))
            {
                var response = await client
                    .addOutboundOrderAsync(
                        new AxataMain.addOutboundOrder_Req(
                            configuration.Username,
                            configuration.Password,
                            [order]))
                    .WaitAsync(cancellationToken);

                CloseWcfClient(client);
                return new AxataWcfDispatchResponse(response.state, response.message, response.processResult);
            }

            throw new NotSupportedException(
                $"AXATA WCF outbound dispatch operation '{operationName}' is not supported.");
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }
    }

    private static async Task<AxataWcfDispatchResponse> AddFirmMasterAsync(
        AxataSynchronizationLiveTransportConfiguration configuration,
        string operationName,
        AxataFirmMasterPayload payload,
        CancellationToken cancellationToken)
    {
        if (!operationName.Equals(DefaultFirmMasterOperationName, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"AXATA WCF firm master dispatch operation '{operationName}' is not supported.");
        }

        var client = CreateMainClient(configuration.MainEndpointUrl);
        var firmMaster = ToWcfFirmMaster(payload);

        try
        {
            var response = await client
                .addFirmMasterAsync(
                    new AxataMain.addFirmMaster_Req(
                        configuration.Username,
                        configuration.Password,
                        [firmMaster]))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
            return new AxataWcfDispatchResponse(response.state, response.message, response.processResult);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }
    }

    private static async Task<AxataWcfDispatchResponse> AddFirmAddressAsync(
        AxataSynchronizationLiveTransportConfiguration configuration,
        string operationName,
        AxataFirmAddressPayload payload,
        CancellationToken cancellationToken)
    {
        if (!operationName.Equals(DefaultFirmAddressOperationName, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"AXATA WCF firm address dispatch operation '{operationName}' is not supported.");
        }

        var client = CreateMainClient(configuration.MainEndpointUrl);
        var firmAddress = ToWcfFirmAddress(payload);

        try
        {
            var response = await client
                .addFirmAddressAsync(
                    new AxataMain.addFirmAddress_Req(
                        configuration.Username,
                        configuration.Password,
                        [firmAddress]))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
            return new AxataWcfDispatchResponse(response.state, response.message, response.processResult);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }
    }

    private static async Task<AxataWcfDispatchResponse> AddInboundOrderAsync(
        AxataSynchronizationLiveTransportConfiguration configuration,
        string operationName,
        AxataLegacyInboundOrderPayload payload,
        CancellationToken cancellationToken)
    {
        var client = CreateMainClient(configuration.MainEndpointUrl);
        var order = ToWcfInboundOrder(payload);

        try
        {
            if (operationName.Equals("addInboundOrderV2", StringComparison.OrdinalIgnoreCase))
            {
                var response = await client
                    .addInboundOrderV2Async(
                        new AxataMain.addInboundOrder_Req1(
                            configuration.Username,
                            configuration.Password,
                            [order]))
                    .WaitAsync(cancellationToken);

                CloseWcfClient(client);
                return new AxataWcfDispatchResponse(response.state, response.message, response.processResultList);
            }

            if (operationName.Equals(DefaultInboundOperationName, StringComparison.OrdinalIgnoreCase))
            {
                var response = await client
                    .addInboundOrderAsync(
                        new AxataMain.addInboundOrder_Req(
                            configuration.Username,
                            configuration.Password,
                            [order]))
                    .WaitAsync(cancellationToken);

                CloseWcfClient(client);
                return new AxataWcfDispatchResponse(response.state, response.message, response.processResult);
            }

            throw new NotSupportedException(
                $"AXATA WCF inbound dispatch operation '{operationName}' is not supported.");
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }
    }

    private static AxataOutbound.OutboundOrderV1 ToWcfOutboundOrder(AxataLegacyOutboundOrderPayload payload) =>
        new()
        {
            ENT000 = new AxataMain.ENT000
            {
                S00SKOD = payload.Master.S00SKOD,
                S00TESN = payload.Master.S00TESN,
                S00DKAN = payload.Master.S00DKAN,
                S00SMUS = payload.Master.S00SMUS,
                S00TMUS = payload.Master.S00TMUS,
                S00TADR = payload.Master.S00TADR,
                S00FDRM = payload.Master.S00FDRM,
                S00FBLK = payload.Master.S00FBLK,
                S00HTP1 = payload.Master.S00HTP1,
                S00HTP2 = payload.Master.S00HTP2
            },
            ENT001_List = payload.Lines
                .Select(line => new AxataMain.ENT001
                {
                    S01SKOD = line.S01SKOD,
                    S01TESL = line.S01TESL,
                    S01KALN = line.S01KALN.ToString(CultureInfo.InvariantCulture),
                    S01SKU = line.S01SKU,
                    S01MIKT = (decimal)line.S01MIKT,
                    S01DEPO = line.S01DEPO
                })
                .ToArray()
        };

    private static AxataModels.FirmMaster ToWcfFirmMaster(AxataFirmMasterPayload payload) =>
        new()
        {
            ENT002 = new AxataMain.ENT002
            {
                S02SKOD = payload.Fields.S02SKOD,
                S02BAYK = payload.Fields.S02BAYK,
                S02BAYT = payload.Fields.S02BAYT,
                S02MUSK = payload.Fields.S02MUSK,
                S02MUSA = payload.Fields.S02MUSA,
                S02ADR1 = payload.Fields.S02ADR1,
                S02ADR2 = payload.Fields.S02ADR2,
                S02ADR3 = payload.Fields.S02ADR3,
                S02VERD = payload.Fields.S02VERD,
                S02VERN = payload.Fields.S02VERN,
                S02EMAIL = payload.Fields.S02EMAIL,
                S02TEL1 = payload.Fields.S02TEL1
            }
        };

    private static AxataModels.FirmAddress ToWcfFirmAddress(AxataFirmAddressPayload payload) =>
        new()
        {
            ENT002_ADR = new AxataMain.ENT002_ADR
            {
                S02SKOD = payload.Fields.S02SKOD,
                S02SIRA = payload.Fields.S02SIRA,
                S02BAYK = payload.Fields.S02BAYK,
                S02ADR1 = payload.Fields.S02ADR1,
                S02ADR2 = payload.Fields.S02ADR2,
                S02ADR3 = payload.Fields.S02ADR3
            }
        };

    private static AxataInbound.InboundOrderV1 ToWcfInboundOrder(AxataLegacyInboundOrderPayload payload) =>
        new()
        {
            ENT013_MST = new AxataMain.ENT013_MST
            {
                S13SKOD = payload.Master.S13SKOD,
                S13HKOD = payload.Master.S13HKOD,
                S13BNUM = payload.Master.S13BNUM,
                S13AKOD = payload.Master.S13AKOD,
                S13FIRM = payload.Master.S13FIRM,
                S13SIPT = ToAxataDateNumber(payload.Master.S13SIPT),
                S13TEST = ToAxataDateNumber(payload.Master.S13TEST)
            },
            ENT013_List = payload.Lines
                .Select(line => new AxataMain.ENT013
                {
                    S13SKOD = line.S13SKOD,
                    S13HKOD = line.S13HKOD,
                    S13BNUM = line.S13BNUM,
                    S13AKOD = line.S13AKOD,
                    S13KALN = line.S13KALN.ToString(CultureInfo.InvariantCulture),
                    S13SKU = line.S13SKU,
                    S13FIRM = line.S13FIRM,
                    S13MIKT = (decimal)line.S13MIKT,
                    S13SIPT = ToAxataDateNumber(line.S13SIPT),
                    S13TEST = ToAxataDateNumber(line.S13TEST)
                })
                .ToArray()
        };

    private static AxataMain.AxataServicePoolClient CreateMainClient(string endpointUrl) =>
        new(
            AxataMain.AxataServicePoolClient.EndpointConfiguration.BasicHttpBinding_IAxataServicePool,
            endpointUrl);

    private static void CloseWcfClient(ICommunicationObject client)
    {
        if (client.State == CommunicationState.Faulted)
        {
            client.Abort();
            return;
        }

        client.Close();
    }

    private static void AbortWcfClient(ICommunicationObject client)
    {
        if (client.State != CommunicationState.Closed)
        {
            client.Abort();
        }
    }

    private static AxataServiceResponse ToServiceResponse(int? state, string? message, string operationName)
    {
        var isSuccess = !state.HasValue || state.Value == 0;

        return new AxataServiceResponse(
            isSuccess,
            state,
            string.IsNullOrWhiteSpace(message)
                ? $"{operationName} response received."
                : message.Trim());
    }

    private static string SerializeResponsePayload(
        string operationName,
        int? state,
        string? message,
        IReadOnlyCollection<AxataMain.ProcessResult>? processResults) =>
        JsonSerializer.Serialize(
            new
            {
                operationName,
                state,
                message,
                processResults
            },
            AxataSynchronizationJson.Options);

    private static decimal? ToAxataDateNumber(string value) =>
        decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string BuildDocumentNumber(string documentSerie, int documentOrderNo) =>
        $"{documentSerie.Trim()}.{documentOrderNo.ToString(CultureInfo.InvariantCulture)}";

    private static string Truncate(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private string ResolveOperationName(string taskCode, string fallbackOperationName)
    {
        if (options.CurrentValue.Tasks.TryGetValue(taskCode, out var taskOptions) &&
            !string.IsNullOrWhiteSpace(taskOptions.LiveOperationName))
        {
            return taskOptions.LiveOperationName.Trim();
        }

        return fallbackOperationName;
    }
}

internal sealed record AxataLiveDispatchResult(
    string OperationName,
    string EndpointUrl,
    bool IsSuccess,
    int? ServiceState,
    string ServiceMessage,
    string PayloadJson,
    string RequestPayloadJson,
    string ResponsePayloadJson,
    IReadOnlyCollection<string> Notes);

internal sealed record AxataSynchronizationLiveTransportConfiguration(
    string MainEndpointUrl,
    string Username,
    string Password);

internal sealed record AxataServiceResponse(
    bool IsSuccess,
    int? State,
    string Message);

internal sealed record AxataWcfDispatchResponse(
    int State,
    string Message,
    IReadOnlyCollection<AxataMain.ProcessResult>? ProcessResults);

internal sealed record AxataLegacyOutboundOrderPayload(
    string DocumentNumber,
    string MovementCode,
    AxataLegacyOutboundOrderMaster Master,
    IReadOnlyCollection<AxataLegacyOutboundOrderLine> Lines);

internal sealed record AxataLegacyOutboundOrderMaster(
    string S00SKOD,
    string S00TESN,
    string S00DKAN,
    string S00SMUS,
    string S00TMUS,
    string S00TADR,
    string S00FDRM,
    string S00FBLK,
    string S00HTP1,
    string S00HTP2);

internal sealed record AxataLegacyOutboundOrderLine(
    string S01SKOD,
    string S01TESL,
    int S01KALN,
    string S01SKU,
    double S01MIKT,
    string S01DEPO);

internal sealed record AxataLegacyInboundOrderPayload(
    string DocumentNumber,
    string MovementCode,
    AxataLegacyInboundOrderMaster Master,
    IReadOnlyCollection<AxataLegacyInboundOrderLine> Lines);

internal sealed record AxataLegacyInboundOrderMaster(
    string S13SKOD,
    string S13HKOD,
    string S13BNUM,
    string S13AKOD,
    string S13FIRM,
    string S13SIPT,
    string S13TEST);

internal sealed record AxataLegacyInboundOrderLine(
    string S13SKOD,
    string S13HKOD,
    string S13BNUM,
    string S13AKOD,
    int S13KALN,
    string S13SKU,
    string S13FIRM,
    double S13MIKT,
    string S13SIPT,
    string S13TEST);

internal sealed record AxataFirmMasterPayload(
    AxataFirmMasterFields Fields);

internal sealed record AxataFirmMasterFields(
    string S02SKOD,
    string S02BAYK,
    decimal S02BAYT,
    string S02MUSK,
    string S02MUSA,
    string S02ADR1,
    string S02ADR2,
    string S02ADR3,
    string S02VERD,
    string S02VERN,
    string S02EMAIL,
    string S02TEL1);

internal sealed record AxataFirmAddressPayload(
    AxataFirmAddressFields Fields);

internal sealed record AxataFirmAddressFields(
    string S02SKOD,
    string S02SIRA,
    string S02BAYK,
    string S02ADR1,
    string S02ADR2,
    string S02ADR3);
