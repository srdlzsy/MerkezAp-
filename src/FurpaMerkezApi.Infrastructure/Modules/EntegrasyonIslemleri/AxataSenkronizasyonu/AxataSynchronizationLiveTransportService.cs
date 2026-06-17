using System.Globalization;
using System.ServiceModel;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using Microsoft.Extensions.Options;
using AxataInbound = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main.WMSServiceCore.Models.Inbounds;
using AxataMain = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main;
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
    private const string DefaultInboundMovementCode = "G01";

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
