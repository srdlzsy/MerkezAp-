using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using UyumsoftDespatch = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Despatch;

namespace FurpaMerkezApi.Infrastructure.Tests.Services;

public sealed class EDespatchDurabilityTests
{
    private const string Number = "FRM2026600132160";
    private const string Uuid = "0d594419-f940-4f7f-acaf-36ee7735bc21";
    private const string Key = "InterWarehouseShipment:56:F56:88015";
    private static readonly SendEDespatchRequest Request = new(
        EDespatchDocumentType.InterWarehouseShipment, 56, "F56", 88015, "16ABC123", "Test Driver", "11111111111");

    [Fact]
    public void PartialMetadata_WithMatchingIdentity_CanBeCompleted()
    {
        var first = Row();
        first.sth_belge_no = Number;
        first.sth_aciklama = Uuid;
        first.sth_kilitli = true;
        var second = Row();
        Assert.False(EDespatchService.HasMatchingSentMarker([first, second], Number, Uuid));
        second.sth_belge_no = Number;
        second.sth_aciklama = Uuid;
        second.sth_kilitli = true;
        Assert.True(EDespatchService.HasMatchingSentMarker([first, second], Number, Uuid));
    }

    [Fact]
    public void MatchingIdentity_WithoutMikroLock_IsNotComplete()
    {
        var row = Row();
        row.sth_belge_no = Number;
        row.sth_aciklama = Uuid;
        row.sth_kilitli = false;
        Assert.False(EDespatchService.HasMatchingSentMarker([row], Number, Uuid));
    }

    [Fact]
    public void DifferentIdentity_CannotBeOverwritten()
    {
        var row = Row();
        row.sth_belge_no = "FRM2026600132999";
        row.sth_aciklama = Guid.NewGuid().ToString();
        Assert.Throws<EDespatchMetadataConflictException>(() => EDespatchService.HasMatchingSentMarker([row], Number, Uuid));
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("stock")]
    [InlineData("unit")]
    [InlineData("warehouse")]
    [InlineData("cancelled")]
    public void Snapshot_RejectsChangedContentEvenWhenGuidIsUnchanged(string change)
    {
        var row = Row();
        var snapshot = EDespatchMovementSnapshot.From(row);
        switch (change)
        {
            case "quantity": row.sth_miktar = 27; break;
            case "stock": row.sth_stok_kod = "008369"; break;
            case "unit": row.sth_birim_pntr = 2; break;
            case "warehouse": row.sth_nakliyedeposu = 120; break;
            case "cancelled": row.sth_iptal = true; break;
        }
        Assert.Throws<EDespatchMetadataConflictException>(() => EDespatchService.EnsureSnapshotMatches([snapshot], [row]));
    }

    [Fact]
    public void Snapshot_RejectsNewLineButAllowsItsOwnMetadataUpdates()
    {
        var row = Row();
        var snapshot = EDespatchMovementSnapshot.From(row);
        Assert.Throws<EDespatchMetadataConflictException>(() => EDespatchService.EnsureSnapshotMatches([snapshot], [row, Row()]));
        row.sth_belge_no = Number;
        row.sth_aciklama = Uuid;
        row.sth_kilitli = true;
        EDespatchService.EnsureSnapshotMatches([snapshot], [row]);
    }

    [Fact]
    public void CompanyReturnSnapshot_AllowsMikroTechnicalTargetWarehouseNormalization()
    {
        var row = Row();
        row.sth_giris_depo_no = 0;
        var snapshot = EDespatchMovementSnapshot.From(row);
        row.sth_giris_depo_no = 1;

        EDespatchService.EnsureSnapshotMatches([snapshot], [row], EDespatchDocumentType.CompanyReturn);
    }

    [Fact]
    public void CompanyReturnSnapshot_RejectsUnexpectedTargetWarehouse()
    {
        var row = Row();
        row.sth_giris_depo_no = 0;
        var snapshot = EDespatchMovementSnapshot.From(row);
        row.sth_giris_depo_no = 2;

        Assert.Throws<EDespatchMetadataConflictException>(() =>
            EDespatchService.EnsureSnapshotMatches([snapshot], [row], EDespatchDocumentType.CompanyReturn));
    }

    [Fact]
    public void Snapshot_AllowsEquivalentBinaryDecimalAmount()
    {
        var row = Row();
        row.sth_tutar = 239.4;
        var snapshot = EDespatchMovementSnapshot.From(row);
        row.sth_tutar = 239.39999999999998;

        EDespatchService.EnsureSnapshotMatches([snapshot], [row]);
    }

    [Fact]
    public void Snapshot_RejectsMeaningfulAmountChange()
    {
        var row = Row();
        row.sth_tutar = 239.4;
        var snapshot = EDespatchMovementSnapshot.From(row);
        row.sth_tutar = 239.41;

        Assert.Throws<EDespatchMetadataConflictException>(() =>
            EDespatchService.EnsureSnapshotMatches([snapshot], [row]));
    }

    [Fact]
    public void NeedsReview_CanBeReopenedOnlyOnceForMetadataRecovery()
    {
        var submission = Submission();
        submission.ConfirmSubmission(DateTime.UtcNow);
        submission.RequireReview("Legacy target warehouse normalization conflict.");

        submission.ReopenMetadataReview(DateTime.UtcNow);

        Assert.Equal(EDespatchSubmissionStatus.PendingMetadata, submission.Status);
        Assert.Equal(1, submission.AttemptCount);
        Assert.Null(submission.LastError);
    }

    [Fact]
    public async Task PdfLookup_WorksBeforeMikroMetadataAndAfterRestart()
    {
        var options = Options();
        await using (var writer = new AuthDbContext(options))
        {
            var submission = Submission();
            submission.ConfirmSubmission(DateTime.UtcNow);
            writer.EDespatchSubmissions.Add(submission);
            await writer.SaveChangesAsync();
        }
        await using var reader = new AuthDbContext(options);
        var result = await EDespatchService.FindConfirmedSubmissionAsync(reader, Request, CancellationToken.None);
        Assert.Equal(Number, result?.DocumentNo);
        Assert.Equal(Uuid, result?.Uuid);
        Assert.Empty(reader.DocumentFlows);
    }

    [Theory]
    [InlineData("008368", 29, true)]
    [InlineData("008369", 29, false)]
    [InlineData("008368", 27, false)]
    public void PreparedUbl_MustMatchSnapshotStockAndQuantity(string stockCode, double quantity, bool matches)
    {
        var xml = EDespatchService.BuildDespatchLineElement(1, stockCode, "Test stock", "8690000000000", "ADET", quantity);
        var line = UyumsoftWcfClientHelper.DeserializeUbl<UyumsoftDespatch.DespatchLineType>(xml.ToString(),
            "DespatchLine", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        var snapshot = EDespatchMovementSnapshot.From(Row());
        if (matches) EDespatchService.EnsureUyumsoftLinesMatch([line], [snapshot]);
        else Assert.Throws<EDespatchMetadataConflictException>(() => EDespatchService.EnsureUyumsoftLinesMatch([line], [snapshot]));
    }

    [Fact]
    public void OutboxLine_CanMatchWhenUyumsoftOmitsSellerStockCode()
    {
        var xml = EDespatchService.BuildDespatchLineElement(1, "008368", "Test stock", "8690000000000", "ADET", 29);
        var line = UyumsoftWcfClientHelper.DeserializeUbl<UyumsoftDespatch.DespatchLineType>(xml.ToString(),
            "DespatchLine", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        line.Item!.SellersItemIdentification = null;

        EDespatchService.EnsureUyumsoftLinesMatch([line], [EDespatchMovementSnapshot.From(Row())]);
    }

    [Fact]
    public void UnknownConflict_RequiresReviewInsteadOfRetryingForever()
    {
        var submission = Submission();

        EDespatchService.ApplyMetadataProcessingFailure(
            submission,
            new EDespatchMetadataConflictException("Uyumsoft line count differs."),
            DateTime.UtcNow);

        Assert.Equal(EDespatchSubmissionStatus.NeedsReview, submission.Status);
        Assert.Equal("Uyumsoft line count differs.", submission.LastError);
        Assert.Equal(0, submission.AttemptCount);
    }

    [Fact]
    public void UnknownTransientFailure_RemainsRetryable()
    {
        var submission = Submission();
        var now = DateTime.UtcNow;

        EDespatchService.ApplyMetadataProcessingFailure(
            submission,
            new TimeoutException("Uyumsoft timeout."),
            now);

        Assert.Equal(EDespatchSubmissionStatus.Unknown, submission.Status);
        Assert.Equal(1, submission.AttemptCount);
        Assert.True(submission.NextAttemptAtUtc > now);
    }

    [Fact]
    public async Task PdfLookup_UsesLegacySuccessWhenMikroIsUnmarked()
    {
        await using var db = new AuthDbContext(Options());
        var flow = new DocumentFlow(Guid.NewGuid(), Key, DocumentFlowType.InterWarehouseShipment,
            56, 132, "F56", 88015, DateTime.UtcNow);
        flow.Record(DocumentFlowStep.EDespatchSubmission, DocumentFlowStatus.Succeeded, "Sent", null, null,
            DateTime.UtcNow, externalDocumentNo: Number, externalUuid: Uuid);
        db.DocumentFlows.Add(flow);
        await db.SaveChangesAsync();
        var result = await EDespatchService.FindConfirmedSubmissionAsync(db, Request, CancellationToken.None);
        Assert.Equal(Number, result?.DocumentNo);
    }

    [Fact]
    public async Task UnknownSubmission_IsNotPresentedAsSuccessfulPdf()
    {
        await using var db = new AuthDbContext(Options());
        db.EDespatchSubmissions.Add(Submission());
        await db.SaveChangesAsync();
        Assert.Null(await EDespatchService.FindConfirmedSubmissionAsync(db, Request, CancellationToken.None));
    }

    [Fact]
    public async Task PdfLookup_DoesNotCrossWarehouseBoundary()
    {
        await using var db = new AuthDbContext(Options());
        var submission = Submission();
        submission.ConfirmSubmission(DateTime.UtcNow);
        db.EDespatchSubmissions.Add(submission);
        await db.SaveChangesAsync();
        Assert.Null(await EDespatchService.FindConfirmedSubmissionAsync(db, Request with { WarehouseNo = 57 }, CancellationToken.None));
    }

    [Fact]
    public void Retry_PreservesUnknownStateAndNeverDropsWorkAfterFiveAttempts()
    {
        var submission = Submission();
        var now = DateTime.UtcNow;
        for (var i = 0; i < 12; i++) submission.ScheduleRetry("Connection lost", now);
        Assert.Equal(EDespatchSubmissionStatus.Unknown, submission.Status);
        Assert.Equal(12, submission.AttemptCount);
        Assert.InRange(submission.NextAttemptAtUtc, now.AddSeconds(1), now.AddMinutes(15));
        submission.ConfirmSubmission(now);
        submission.ScheduleRetry("Mikro unavailable", now);
        Assert.Equal(EDespatchSubmissionStatus.PendingMetadata, submission.Status);
    }

    [Fact]
    public void DurablePayload_PreservesDriverContactsAndSnapshot()
    {
        var work = new EDespatchMetadataUpdateWorkItem(Request with { Deliverer = "Sender", Receiver = "Receiver" },
            Number, Uuid, [EDespatchMovementSnapshot.From(Row())]);
        var restored = JsonSerializer.Deserialize<EDespatchMetadataUpdateWorkItem>(JsonSerializer.Serialize(work))!;
        Assert.Equal(work.Request, restored.Request);
        Assert.Equal(work.Movements, restored.Movements);
    }

    [Fact]
    public async Task Worker_ProcessesOldPersistedWorkAndDrainsBeyondOneBatch()
    {
        var databaseName = Guid.NewGuid().ToString();
        var processed = new HashSet<Guid>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddDbContext<AuthDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IEDespatchMetadataUpdateProcessor>(sp => new CompletingProcessor(
            sp.GetRequiredService<AuthDbContext>(), processed, finished));
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            for (var i = 0; i < 27; i++)
            {
                var entry = new EDespatchSubmission($"doc-{i}", $"FRM2026{i:D9}", Guid.NewGuid().ToString(), "{}", DateTime.UtcNow.AddDays(-10));
                entry.ConfirmSubmission(DateTime.UtcNow.AddDays(-10));
                db.EDespatchSubmissions.Add(entry);
            }
            var completed = new EDespatchSubmission("completed", "FRM2026999999999", Guid.NewGuid().ToString(), "{}", DateTime.UtcNow);
            completed.Complete(DateTime.UtcNow);
            db.EDespatchSubmissions.Add(completed);
            await db.SaveChangesAsync();
        }
        using var worker = new EDespatchMetadataUpdateWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EDespatchMetadataUpdateWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try { await finished.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Equal(27, processed.Count);
    }

    private sealed class CompletingProcessor(AuthDbContext db, HashSet<Guid> processed, TaskCompletionSource finished)
        : IEDespatchMetadataUpdateProcessor
    {
        public async Task ProcessPendingAsync(Guid id, CancellationToken cancellationToken)
        {
            var entry = await db.EDespatchSubmissions.SingleAsync(x => x.Id == id, cancellationToken);
            entry.Complete(DateTime.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            processed.Add(id);
            if (processed.Count == 27) finished.TrySetResult();
        }
        public Task ImportLegacyAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static EDespatchSubmission Submission() => new(Key, Number, Uuid,
        JsonSerializer.Serialize(new EDespatchMetadataUpdateWorkItem(Request, Number, Uuid, [EDespatchMovementSnapshot.From(Row())])), DateTime.UtcNow);
    private static DbContextOptions<AuthDbContext> Options() => new DbContextOptionsBuilder<AuthDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static STOK_HAREKETLERI Row() => new()
    {
        sth_Guid = Guid.NewGuid(), sth_satirno = 0, sth_stok_kod = "008368", sth_miktar = 29,
        sth_birim_pntr = 1, sth_cikis_depo_no = 56, sth_nakliyedeposu = 132
    };
}
