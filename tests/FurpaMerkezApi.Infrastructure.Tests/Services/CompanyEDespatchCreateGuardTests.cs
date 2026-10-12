using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
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

    [Theory]
    [InlineData("complete")]
    [InlineData("processing")]
    [InlineData("wrong-document")]
    [InlineData("changed-quantity")]
    [InlineData("changed-guid")]
    [InlineData("wrong-source-line")]
    public async Task Guard_VerifiesAutomaticReturnCreatedByCompanyReceiving(string scenario)
    {
        await using var db = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnMovementGuid = Guid.NewGuid();
        var original = new CreateCompanyReceivingRequest(
            56,
            userId,
            requestId,
            "320.01",
            DateTime.Today,
            DateTime.Today,
            "CSD",
            1757,
            null,
            null,
            null,
            null,
            false,
            true,
            [new CreateCompanyReceivingLineRequest("A", 5, 5, 2, 10, 1)]);
        var result = new CreateCompanyReceivingLineResultDto(
            Guid.NewGuid(),
            scenario == "wrong-source-line" ? 2 : 0,
            0,
            "A",
            null,
            false,
            "orderless",
            5,
            2,
            0,
            2,
            0,
            0,
            5,
            2,
            3,
            "IadeOlusturuldu",
            returnMovementGuid,
            "F56",
            scenario == "wrong-document" ? 124 : 123,
            "GonderimBekliyor");
        var response = new CreateCompanyReceivingResponse(
            "CSD",
            1757,
            DateTime.Today,
            DateTime.Today,
            string.Empty,
            56,
            "320.01",
            1,
            2,
            0,
            2,
            0,
            20,
            "Test",
            [result],
            TotalDispatchQuantity: 5,
            TotalNetAcceptedQuantity: 2,
            TotalReturnedQuantity: 3,
            AutoCreatedReturnLineCount: 1,
            AutoCreatedReturnDocumentSerie: "F56",
            AutoCreatedReturnDocumentOrderNo: scenario == "wrong-document" ? 124 : 123,
            ReturnEDespatchStatus: "GonderimBekliyor");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var record = new MobileOfflineSyncRequest(
            Guid.NewGuid(),
            "mal-kabul-islemleri.firma-mal-kabulleri.create",
            userId,
            56,
            requestId.ToString("D"),
            "hash",
            JsonSerializer.Serialize(original, options),
            DateTime.UtcNow);
        if (scenario != "processing")
            record.MarkCompleted(JsonSerializer.Serialize(response, options), DateTime.UtcNow);
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();

        var row = new STOK_HAREKETLERI
        {
            sth_Guid = scenario == "changed-guid" ? Guid.NewGuid() : returnMovementGuid,
            sth_iptal = false,
            sth_evraktip = 1,
            sth_tip = 1,
            sth_cins = 0,
            sth_normal_iade = 1,
            sth_evrakno_seri = "F56",
            sth_evrakno_sira = 123,
            sth_satirno = 0,
            sth_stok_kod = "A",
            sth_cari_kodu = "320.01",
            sth_miktar = scenario == "changed-quantity" ? 2 : 3,
            sth_birim_pntr = 1,
            sth_tutar = 30,
            sth_cikis_depo_no = 56,
            sth_eticaret_kanal_kodu = MobileOfflineSyncService.ToTraceKey(requestId)
        };
        var send = new SendEDespatchRequest(
            EDespatchDocumentType.CompanyReturn, 56, "F56", 123, string.Empty, string.Empty, string.Empty);

        if (scenario == "complete")
            await CompanyEDespatchCreateGuard.EnsureCompleteAsync(db, send, [row], CancellationToken.None);
        else
            await Assert.ThrowsAsync<OperationConflictException>(() =>
                CompanyEDespatchCreateGuard.EnsureCompleteAsync(db, send, [row], CancellationToken.None));
    }
}
