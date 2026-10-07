using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services.MikroApi;

public sealed class MikroApiWriteAuditTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkRecovered_UsesVerifiedAfterSuccessfulResponse()
    {
        var audit = CreateAudit();
        audit.Complete(false, false, 200, 200, "{}", null, 1, 1200, Now.AddSeconds(2));

        audit.MarkRecovered("F56/123", Guid.NewGuid(), null, Now.AddSeconds(3));

        Assert.Equal(MikroApiWriteAuditStatus.Verified, audit.Status);
        Assert.Equal(MikroApiWriteAuditResultSource.MikroDatabaseReadback, audit.ResultSource);
    }

    [Fact]
    public void MarkRecovered_UsesRecoveredAfterUnknownAfterTimeout()
    {
        var audit = CreateAudit();
        audit.Complete(true, true, null, null, null, "timeout", 1, 60_000, Now.AddMinutes(1));

        audit.MarkRecovered("F56/124", Guid.NewGuid(), null, Now.AddMinutes(2));

        Assert.Equal(MikroApiWriteAuditStatus.RecoveredAfterUnknown, audit.Status);
        Assert.Equal(MikroApiWriteAuditResultSource.MikroDatabaseReadback, audit.ResultSource);
    }

    [Theory]
    [InlineData(200, MikroApiWriteAuditResultSource.MikroApiResponse)]
    [InlineData(null, MikroApiWriteAuditResultSource.TransportFailure)]
    public void Complete_RecordsResultSource(
        int? httpStatusCode,
        MikroApiWriteAuditResultSource expectedSource)
    {
        var audit = CreateAudit();

        audit.Complete(
            isError: true,
            isUnknown: true,
            httpStatusCode,
            mikroStatusCode: null,
            response: null,
            error: "timeout",
            attemptCount: 1,
            elapsedMilliseconds: 60_000,
            completedAtUtc: Now.AddMinutes(1));

        Assert.Equal(expectedSource, audit.ResultSource);
    }

    [Fact]
    public void MarkOutcomeUnknown_DoesNotOverwriteVerifiedTerminalStatuses()
    {
        var verified = CreateAudit();
        verified.Complete(false, false, 200, 200, "{}", null, 1, 1200, Now.AddSeconds(2));
        verified.MarkRecovered("F56/125", null, null, Now.AddSeconds(3));
        var recoveredAfterUnknown = CreateAudit();
        recoveredAfterUnknown.MarkRecovered("F56/126", null, null, Now.AddSeconds(3));

        verified.MarkOutcomeUnknown("late reconciliation", Now.AddMinutes(20));
        recoveredAfterUnknown.MarkOutcomeUnknown("late reconciliation", Now.AddMinutes(20));

        Assert.Equal(MikroApiWriteAuditStatus.Verified, verified.Status);
        Assert.Equal(MikroApiWriteAuditStatus.RecoveredAfterUnknown, recoveredAfterUnknown.Status);
    }

    [Fact]
    public void ContextExtractor_ReadsStockMovementDocumentContext()
    {
        const string payload = """
            {
              "evraklar": [{
                "satirlar": [
                  { "sth_evrakno_seri": "F56", "sth_evrakno_sira": 88242, "sth_cikis_depo_no": 56 },
                  { "sth_evrakno_seri": "F56", "sth_evrakno_sira": 88242, "sth_cikis_depo_no": 56 }
                ]
              }]
            }
            """;

        var context = MikroApiWriteAuditContextExtractor.Extract(payload);

        Assert.Equal("F56", context.DocumentSerie);
        Assert.Equal(88242, context.DocumentOrderNo);
        Assert.Equal(56, context.WarehouseNo);
        Assert.Equal(2, context.LineCount);
    }

    [Fact]
    public void ContextExtractor_ReturnsEmptyContextForInvalidJson()
    {
        var context = MikroApiWriteAuditContextExtractor.Extract("not-json");

        Assert.Null(context.DocumentSerie);
        Assert.Null(context.DocumentOrderNo);
        Assert.Null(context.WarehouseNo);
        Assert.Null(context.LineCount);
    }

    private static MikroApiWriteAudit CreateAudit() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            new string('A', 64),
            Now);
}
