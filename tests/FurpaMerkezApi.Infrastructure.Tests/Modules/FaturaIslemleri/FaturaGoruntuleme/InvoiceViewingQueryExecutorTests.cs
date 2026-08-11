using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class InvoiceViewingQueryExecutorTests
{
    [Fact]
    public async Task ListAsync_WhenSearchTextIsBlank_AppliesDateRange()
    {
        await using var authDbContext = CreateAuthDbContext();
        var now = new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc);

        authDbContext.UyumsoftInboxInvoices.Add(CreateInvoice(
            "DOC-IN-DATE",
            "S902026000000001",
            new DateTime(2026, 7, 29),
            now));
        authDbContext.UyumsoftInboxInvoices.Add(CreateInvoice(
            "DOC-OUT-DATE",
            "S902026000236986",
            new DateTime(2026, 7, 20),
            now));
        await authDbContext.SaveChangesAsync();

        var executor = new InvoiceViewingQueryExecutor(authDbContext, new FixedClock(now));

        var result = await executor.ListAsync(
            CreateRequest(
                searchField: null,
                searchText: null),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("S902026000000001", result.Items.Single().InvoiceId);
    }

    [Fact]
    public async Task ListAsync_WhenSearchTextIsProvided_SearchesOutsideSelectedDateRange()
    {
        await using var authDbContext = CreateAuthDbContext();
        var now = new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc);

        authDbContext.UyumsoftInboxInvoices.Add(CreateInvoice(
            "DOC-IN-DATE",
            "S902026000000001",
            new DateTime(2026, 7, 29),
            now));
        authDbContext.UyumsoftInboxInvoices.Add(CreateInvoice(
            "DOC-OUT-DATE",
            "S902026000236986",
            new DateTime(2026, 7, 20),
            now));
        await authDbContext.SaveChangesAsync();

        var executor = new InvoiceViewingQueryExecutor(authDbContext, new FixedClock(now));

        var result = await executor.ListAsync(
            CreateRequest(
                InvoiceViewingSearchField.InvoiceId,
                "S902026000236986"),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("S902026000236986", result.Items.Single().InvoiceId);
    }

    [Fact]
    public async Task ListAsync_WhenSearchTextAndDateFilterFlagAreProvided_AppliesDateRange()
    {
        await using var authDbContext = CreateAuthDbContext();
        var now = new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc);

        authDbContext.UyumsoftInboxInvoices.Add(CreateInvoice(
            "DOC-IN-DATE",
            "S902026000236986",
            new DateTime(2026, 7, 29),
            now));
        authDbContext.UyumsoftInboxInvoices.Add(CreateInvoice(
            "DOC-OUT-DATE",
            "S902026000236986",
            new DateTime(2026, 7, 20),
            now));
        await authDbContext.SaveChangesAsync();

        var executor = new InvoiceViewingQueryExecutor(authDbContext, new FixedClock(now));

        var result = await executor.ListAsync(
            CreateRequest(
                InvoiceViewingSearchField.InvoiceId,
                "S902026000236986",
                applyDateFilterWithSearch: true),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("DOC-IN-DATE", result.Items.Single().DocumentId);
    }

    private static InvoiceViewingListRequest CreateRequest(
        InvoiceViewingSearchField? searchField,
        string? searchText,
        bool applyDateFilterWithSearch = false) =>
        new(
            new DateTime(2026, 7, 29),
            new DateTime(2026, 7, 29),
            IsProcessed: null,
            IsPrinted: null,
            InvoiceId: null,
            DespatchId: null,
            CustomerTitle: null,
            CustomerTcknVkn: null,
            DocumentId: null,
            OrderDocumentId: null,
            Status: null,
            InvoiceType: null,
            MinInvoiceTotal: null,
            MaxInvoiceTotal: null,
            HasDespatchId: null,
            searchField,
            searchText,
            PageNumber: 1,
            PageSize: 50,
            ApplyDateFilterWithSearch: applyDateFilterWithSearch);

    private static UyumsoftInboxInvoice CreateInvoice(
        string documentId,
        string invoiceId,
        DateTime invoiceDate,
        DateTime synchronizedAtUtc) =>
        new(
            Guid.NewGuid(),
            documentId,
            invoiceId,
            serviceDocumentId: null,
            localDocumentId: null,
            customerTitle: "Ornek Tedarikci",
            customerTcknVkn: "1234567890",
            createDate: invoiceDate.AddHours(9),
            invoiceDate,
            invoiceType: "SATIS",
            invoiceTotal: 100m,
            despatchId: string.Empty,
            isProcessed: false,
            isPrinted: false,
            isStandard: true,
            statusCode: "1000",
            status: "Onaylandi",
            envelopeStatusCode: null,
            envelopeIdentifier: string.Empty,
            message: string.Empty,
            taxTotal: 20m,
            taxExclusiveAmount: 80m,
            documentCurrencyCode: "TRY",
            exchangeRate: 1m,
            orderDocumentId: string.Empty,
            isArchived: false,
            invoiceTipType: "Temel",
            invoiceTipTypeCode: 0,
            isSeen: true,
            synchronizedAtUtc);

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"invoice-viewing-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
