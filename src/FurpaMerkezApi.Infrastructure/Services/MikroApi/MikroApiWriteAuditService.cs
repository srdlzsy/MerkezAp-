using System.Security.Cryptography;
using System.Text;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiWriteAuditService(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    IClock clock,
    IOptionsMonitor<MikroApiWriteAuditOptions> options,
    ILogger<MikroApiWriteAuditService> logger)
{
    public async Task<MikroApiWriteAuditHandle> BeginAsync(
        string endpoint,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid();

        if (!options.CurrentValue.Enabled)
        {
            return new MikroApiWriteAuditHandle(null, requestId);
        }

        var auditId = Guid.NewGuid();
        var correlationId = ResolveCorrelationId();
        var payloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson ?? string.Empty)));
        var audit = new MikroApiWriteAudit(
            auditId,
            requestId,
            correlationId,
            endpoint,
            payloadHash,
            clock.UtcNow);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            dbContext.MikroApiWriteAudits.Add(audit);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new MikroApiWriteAuditHandle(auditId, requestId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Mikro API write audit could not be started. RequestId={RequestId}, Endpoint={Endpoint}",
                requestId,
                endpoint);
            return new MikroApiWriteAuditHandle(null, requestId);
        }
    }

    public Task CompleteAsync<TResponse>(
        MikroApiWriteAuditHandle handle,
        MikroApiResult<TResponse> result,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            handle.AuditId,
            audit => audit.Complete(
                result.IsError,
                result.HttpStatusCode == 0,
                result.HttpStatusCode == 0 ? null : (int)result.HttpStatusCode,
                result.StatusCode == 0 ? null : result.StatusCode,
                Limit(result.RawResponse, options.CurrentValue.MaxResponseLength),
                result.ErrorMessage,
                result.AttemptCount,
                (long)result.Elapsed.TotalMilliseconds,
                clock.UtcNow),
            "completed",
            cancellationToken);

    public Task MarkRecoveredAsync(
        Guid? auditId,
        string? documentNo,
        Guid? recoveredGuid,
        Guid? documentFlowId,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            auditId,
            audit => audit.MarkRecovered(documentNo, recoveredGuid, documentFlowId, clock.UtcNow),
            "marked as recovered",
            cancellationToken);

    private async Task UpdateAsync(
        Guid? auditId,
        Action<MikroApiWriteAudit> update,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled || !auditId.HasValue)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var audit = await dbContext.MikroApiWriteAudits
                .SingleOrDefaultAsync(item => item.Id == auditId.Value, cancellationToken);

            if (audit is null)
            {
                logger.LogWarning("Mikro API write audit was not found. AuditId={AuditId}", auditId);
                return;
            }

            update(audit);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Mikro API write audit could not be {Operation}. AuditId={AuditId}",
                operation,
                auditId);
        }
    }

    private string ResolveCorrelationId()
    {
        var traceIdentifier = httpContextAccessor.HttpContext?.TraceIdentifier;
        return string.IsNullOrWhiteSpace(traceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : traceIdentifier;
    }

    private static string? Limit(string? value, int configuredMaxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var maxLength = Math.Clamp(configuredMaxLength, 1, 8000);
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

public readonly record struct MikroApiWriteAuditHandle(Guid? AuditId, Guid RequestId);
