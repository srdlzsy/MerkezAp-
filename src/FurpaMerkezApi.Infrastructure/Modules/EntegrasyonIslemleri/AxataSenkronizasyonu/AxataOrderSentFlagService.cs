using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataOrderSentFlagService(
    MikroWriteDbContext mikroWriteDbContext,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<AxataOrderSentFlagService> logger)
{
    private const short MikroUserNo = 39;
    private const string CompletedStatus = "1";
    private const string DepolarArasiSiparisDuzeltPath = "/Api/apiMethods/DepolarArasiSiparisDuzeltV2";
    private const string SiparisDuzeltPath = "/Api/apiMethods/SiparisDuzeltV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;

    public async Task<AxataOrderSentFlagMarkResult> MarkWarehouseOrderAsSentAsync(
        WarehouseOrderDetailRequest request,
        WarehouseOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        return mikroWriteRoutingOptions.CurrentValue.IssuedWarehouseOrder switch
        {
            MikroWriteMode.MikroApi => new AxataOrderSentFlagMarkResult(
                await MarkWarehouseOrderAsSentWithMikroApiAsync(request, direction, cancellationToken),
                "MikroApi:DepolarArasiSiparisDuzeltV2"),
            MikroWriteMode.Database => new AxataOrderSentFlagMarkResult(
                await MarkWarehouseOrderAsSentInDatabaseAsync(request, direction, cancellationToken),
                "Database"),
            MikroWriteMode.DualShadow => new AxataOrderSentFlagMarkResult(
                await MarkWarehouseOrderAsSentInDatabaseAsync(request, direction, cancellationToken),
                "Database:DualShadowFallback"),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:IssuedWarehouseOrder mode '{mode}'.")
        };
    }

    public async Task<AxataOrderSentFlagMarkResult> MarkCompanyOrderAsSentAsync(
        CompanyOrderDetailRequest request,
        CompanyOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        return mikroWriteRoutingOptions.CurrentValue.CompanyOrderSentFlag switch
        {
            MikroWriteMode.MikroApi => new AxataOrderSentFlagMarkResult(
                await MarkCompanyOrderAsSentWithMikroApiAsync(request, direction, cancellationToken),
                "MikroApi:SiparisDuzeltV2"),
            MikroWriteMode.Database => new AxataOrderSentFlagMarkResult(
                await MarkCompanyOrderAsSentInDatabaseAsync(request, direction, cancellationToken),
                "Database"),
            MikroWriteMode.DualShadow => new AxataOrderSentFlagMarkResult(
                await MarkCompanyOrderAsSentInDatabaseAsync(request, direction, cancellationToken),
                "Database:DualShadowFallback"),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:CompanyOrderSentFlag mode '{mode}'.")
        };
    }

    private async Task<int> MarkCompanyOrderAsSentInDatabaseAsync(
        CompanyOrderDetailRequest request,
        CompanyOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var orderType = direction == CompanyOrderListDirection.Issued ? (byte)1 : (byte)0;
        var now = DateTime.Now;

        return await mikroWriteDbContext.SIPARISLERs
            .Where(order =>
                order.sip_iptal != true &&
                order.sip_tip == orderType &&
                order.sip_cins == 0 &&
                order.sip_depono == request.WarehouseNo &&
                order.sip_evrakno_seri == documentSerie &&
                order.sip_evrakno_sira == request.DocumentOrderNo)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.sip_special1, CompletedStatus)
                .SetProperty(order => order.sip_lastup_user, MikroUserNo)
                .SetProperty(order => order.sip_lastup_date, now),
                cancellationToken);
    }

    private async Task<int> MarkCompanyOrderAsSentWithMikroApiAsync(
        CompanyOrderDetailRequest request,
        CompanyOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        var lineGuids = await GetCompanyOrderLineGuidsAsync(request, direction, cancellationToken);

        if (lineGuids.Length == 0)
        {
            return 0;
        }

        var payload = AxataCompanyOrderSentFlagMikroApiPayloadFactory.Create(
            lineGuids,
            CompletedStatus);

        logger.LogInformation(
            "AXATA company order sent flag is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, LineCount={LineCount}",
            SiparisDuzeltPath,
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.WarehouseNo,
            lineGuids.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<System.Text.Json.JsonElement>(
            SiparisDuzeltPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API company order sent flag update failed.");
        }

        var markedLineCount = await RecoverMikroApiCompanyOrderSentFlagAsync(
            lineGuids,
            cancellationToken);

        await mikroApiClient.MarkRecoveredAsync(
            result,
            $"{request.DocumentSerie.Trim()}/{request.DocumentOrderNo}",
            cancellationToken: cancellationToken);

        return markedLineCount;
    }

    private async Task<int> MarkWarehouseOrderAsSentInDatabaseAsync(
        WarehouseOrderDetailRequest request,
        WarehouseOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var now = DateTime.Now;
        var isInboundWarehouseOrder = direction == WarehouseOrderListDirection.Issued;

        return await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == request.DocumentOrderNo &&
                (isInboundWarehouseOrder
                    ? order.ssip_girdepo == request.WarehouseNo
                    : order.ssip_cikdepo == request.WarehouseNo))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.ssip_special1, CompletedStatus)
                .SetProperty(order => order.ssip_lastup_user, MikroUserNo)
                .SetProperty(order => order.ssip_lastup_date, now),
                cancellationToken);
    }

    private async Task<int> MarkWarehouseOrderAsSentWithMikroApiAsync(
        WarehouseOrderDetailRequest request,
        WarehouseOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        var lineGuids = await GetWarehouseOrderLineGuidsAsync(request, direction, cancellationToken);

        if (lineGuids.Length == 0)
        {
            return 0;
        }

        var payload = AxataWarehouseOrderSentFlagMikroApiPayloadFactory.Create(
            lineGuids,
            CompletedStatus);

        logger.LogInformation(
            "AXATA warehouse order sent flag is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, LineCount={LineCount}",
            DepolarArasiSiparisDuzeltPath,
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.WarehouseNo,
            lineGuids.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<System.Text.Json.JsonElement>(
            DepolarArasiSiparisDuzeltPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API warehouse order sent flag update failed.");
        }

        var markedLineCount = await RecoverMikroApiWarehouseOrderSentFlagAsync(
            lineGuids,
            cancellationToken);

        await mikroApiClient.MarkRecoveredAsync(
            result,
            $"{request.DocumentSerie.Trim()}/{request.DocumentOrderNo}",
            cancellationToken: cancellationToken);

        return markedLineCount;
    }

    private async Task<Guid[]> GetWarehouseOrderLineGuidsAsync(
        WarehouseOrderDetailRequest request,
        WarehouseOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var isInboundWarehouseOrder = direction == WarehouseOrderListDirection.Issued;

        return await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == request.DocumentOrderNo &&
                (isInboundWarehouseOrder
                    ? order.ssip_girdepo == request.WarehouseNo
                    : order.ssip_cikdepo == request.WarehouseNo))
            .Select(order => order.ssip_Guid)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<Guid[]> GetCompanyOrderLineGuidsAsync(
        CompanyOrderDetailRequest request,
        CompanyOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var orderType = direction == CompanyOrderListDirection.Issued ? (byte)1 : (byte)0;

        return await mikroWriteDbContext.SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.sip_iptal != true &&
                order.sip_tip == orderType &&
                order.sip_cins == 0 &&
                order.sip_depono == request.WarehouseNo &&
                order.sip_evrakno_seri == documentSerie &&
                order.sip_evrakno_sira == request.DocumentOrderNo)
            .Select(order => order.sip_Guid)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<int> RecoverMikroApiWarehouseOrderSentFlagAsync(
        IReadOnlyCollection<Guid> lineGuids,
        CancellationToken cancellationToken)
    {
        var lineGuidArray = lineGuids.ToArray();

        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var markedLineCount = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    lineGuidArray.Contains(order.ssip_Guid) &&
                    order.ssip_special1 == CompletedStatus)
                .CountAsync(cancellationToken);

            if (markedLineCount == lineGuids.Count)
            {
                return markedLineCount;
            }

            if (attempt < MikroApiRecoveryAttemptCount)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(MikroApiRecoveryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Mikro API warehouse order sent flag update succeeded, but ssip_special1=1 could not be verified for all order lines.");
    }

    private async Task<int> RecoverMikroApiCompanyOrderSentFlagAsync(
        IReadOnlyCollection<Guid> lineGuids,
        CancellationToken cancellationToken)
    {
        var lineGuidArray = lineGuids.ToArray();

        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var markedLineCount = await mikroWriteDbContext.SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    lineGuidArray.Contains(order.sip_Guid) &&
                    order.sip_special1 == CompletedStatus)
                .CountAsync(cancellationToken);

            if (markedLineCount == lineGuids.Count)
            {
                return markedLineCount;
            }

            if (attempt < MikroApiRecoveryAttemptCount)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(MikroApiRecoveryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Mikro API company order sent flag update succeeded, but sip_special1=1 could not be verified for all order lines.");
    }
}

internal sealed record AxataOrderSentFlagMarkResult(
    int LineCount,
    string WriteChannel);
