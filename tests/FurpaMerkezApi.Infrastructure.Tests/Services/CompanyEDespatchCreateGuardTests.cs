using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Services;
using FurpaMerkezApi.Infrastructure.Tests.Modules.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services;

public sealed class CompanyEDespatchCreateGuardTests
{
    [Theory]
    [InlineData("complete")]
    [InlineData("return-complete")]
    [InlineData("return-partial")]
    [InlineData("processing")]
    [InlineData("partial")]
    [InlineData("old-partial-success")]
    [InlineData("changed-stock")]
    [InlineData("mixed-trace")]
    [InlineData("missing-request")]
    [InlineData("wrong-document")]
    [InlineData("review")]
    [InlineData("malformed")]
    public async Task Guard_VerifiesStoredRequestBeforeSending(string scenario)
    {
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var original = StockMovementRecoveryMatcherTests.CompanyRequest;
        var rows = new[] { StockMovementRecoveryMatcherTests.Row(), StockMovementRecoveryMatcherTests.Row(1) };
        var isReturn = scenario.StartsWith("return-", StringComparison.Ordinal);
        if (isReturn)
            foreach (var row in rows) { row.sth_cins = 0; row.sth_normal_iade = 1; }
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var record = new MobileOfflineSyncRequest(Guid.NewGuid(), isReturn ? "iade-islemleri.firma-iadeleri.create" : "sevk-islemleri.giden-firma-sevkleri.create",
            Guid.NewGuid(), 56, original.ClientRequestId!.Value.ToString("D"), "hash",
            scenario == "missing-request" ? null : scenario == "malformed" ? "{" : JsonSerializer.Serialize(original, options), DateTime.UtcNow);
        var response = new CreateCompanyMovementResponse("F56", scenario == "wrong-document" ? 124 : 123,
            DateTime.Today, DateTime.Today, "", 56, "320.01", scenario == "old-partial-success" ? 1 : 2, 5, 50, "Test");
        if (scenario == "review")
            record.MarkFailed("Manual review", DateTime.UtcNow, OperationConflictErrorCodes.MikroDocumentContentMismatch, false);
        else if (scenario != "processing")
            record.MarkCompleted(JsonSerializer.Serialize(response, options), DateTime.UtcNow);
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        if (scenario is "partial" or "old-partial-success" or "return-partial") rows = [rows[0]];
        if (scenario == "changed-stock") rows[0].sth_stok_kod = "OTHER";
        if (scenario == "mixed-trace") rows[0].sth_eticaret_kanal_kodu = "";
        var send = new SendEDespatchRequest(isReturn ? EDespatchDocumentType.CompanyReturn : EDespatchDocumentType.OutgoingCompanyShipment, 56, "F56", 123, "", "", "");

        if (scenario is "complete" or "return-complete")
            await CompanyEDespatchCreateGuard.EnsureCompleteAsync(db, send, rows, CancellationToken.None);
        else
            await Assert.ThrowsAsync<OperationConflictException>(() =>
                CompanyEDespatchCreateGuard.EnsureCompleteAsync(db, send, rows, CancellationToken.None));
    }

    [Fact]
    public async Task LegacyWithoutTrace_DoesNotRequireALocalCreateRecord()
    {
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var row = StockMovementRecoveryMatcherTests.Row();
        row.sth_eticaret_kanal_kodu = "";
        await CompanyEDespatchCreateGuard.EnsureCompleteAsync(db,
            new(EDespatchDocumentType.CompanyReturn, 56, "F56", 123, "", "", "", ExpectedLineCount: 1),
            [row], CancellationToken.None);
    }
}
