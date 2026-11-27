using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationFailureBehaviorTests
{
    [Fact]
    public void CancelledC02Document_DoesNotRequireMikroAnalysis()
    {
        var document = new AxataOutboundDeliveryDocument(
            1912,
            "a.1912",
            "a",
            1912,
            "C02",
            "0",
            "3",
            "IPTAL",
            50,
            110,
            DateTime.Today,
            Array.Empty<AxataOutboundDeliveryLine>());

        Assert.False(AxataOutboundDeliveryImportService.RequiresC02MikroAnalysis(document));
        Assert.True(AxataOutboundDeliveryImportService.IsOutboundDeliveryReadyForProcessing(document));
    }

    [Fact]
    public void HeaderOnlyC02Document_IsNotReadyAndDoesNotBlockRealDeliveries()
    {
        var document = new AxataOutboundDeliveryDocument(
            262270,
            "A.1930",
            "A",
            1930,
            "C02",
            "0",
            string.Empty,
            string.Empty,
            12120063,
            12120063,
            DateTime.Today,
            Array.Empty<AxataOutboundDeliveryLine>());

        Assert.False(AxataOutboundDeliveryImportService.IsOutboundDeliveryReadyForProcessing(document));
    }

    [Fact]
    public void C02DocumentWithPositiveLine_IsReadyForProcessing()
    {
        var document = new AxataOutboundDeliveryDocument(
            262280,
            "A.1936",
            "A",
            1936,
            "C02",
            "0",
            string.Empty,
            string.Empty,
            12120063,
            12120063,
            DateTime.Today,
            [new AxataOutboundDeliveryLine(0, "007921", 757d)]);

        Assert.True(AxataOutboundDeliveryImportService.IsOutboundDeliveryReadyForProcessing(document));
    }

    [Fact]
    public void C01PreviewDateFilter_IsAppliedBeforeTakeSelection()
    {
        var selectedDate = new DateTime(2026, 11, 13);
        var documents = new[]
        {
            CreateC01Document(264000, selectedDate.AddDays(-1)),
            CreateC01Document(264001, selectedDate),
            CreateC01Document(264002, selectedDate.AddDays(1))
        };

        var filtered = AxataOutboundDeliveryImportService.ApplyAxataDateFilter(
            documents,
            selectedDate);

        var document = Assert.Single(filtered);
        Assert.Equal(264001, document.AxataSequenceNo);
    }

    [Fact]
    public void C01OverDelivery_ReportsOnlyQuantityAboveMikroRemaining()
    {
        var document = CreateC01Document(262929, new DateTime(2026, 10, 31));
        var orderLine = new DEPOLAR_ARASI_SIPARISLER
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_miktar = 380d,
            ssip_teslim_miktar = 0d
        };
        var matchedLines = new[]
        {
            new MatchedC01Line(
                document,
                new AxataOutboundDeliveryLine(3, "015550", 384d),
                orderLine)
        };

        var excessQuantity = AxataOutboundDeliveryImportService.GetC01OverDeliveryQuantity(matchedLines);

        Assert.Equal(4d, excessQuantity);
    }

    [Fact]
    public void DeletedC01Order_BuildsIdempotentShipmentWithoutCreatingReplacementOrder()
    {
        var document = CreateC01Document(265235, new DateTime(2026, 11, 14));
        var analysis = new C01DeliveryAnalysis(
            document,
            new AxataOutboundDeliveryImportDocumentDto(
                document.AxataSequenceNo,
                document.AxataDeliveryNo,
                document.DocumentSerie,
                document.DocumentOrderNo ?? 0,
                document.MovementType,
                document.Status,
                document.SourceWarehouseNo,
                document.TargetWarehouseNo,
                document.AxataDate,
                document.Lines.Count,
                document.Lines.Sum(line => line.Quantity),
                0,
                0d,
                0d,
                0,
                true,
                "Mikro siparisi silinmis.",
                "OrderMissingReadyForImport",
                "ImportWithoutOrder"),
            Array.Empty<MatchedC01Line>(),
            "OrderMissingReadyForImport",
            C01ExistingShipmentCheck.None);

        var request = AxataOutboundDeliveryImportService.BuildCreateShipmentRequest(
            analysis,
            Guid.NewGuid(),
            "axata");

        Assert.False(request.UpdateLinkedOrderDeliveredQuantities);
        Assert.True(request.DisableAutomaticWarehouseOrderCreation);
        Assert.False(request.AllowLinkedOrderOverDelivery);
        Assert.Equal("C01-265235", request.DocumentNo);
        Assert.Equal(
            AxataOutboundDeliveryImportService.BuildC01ClientRequestId(265235),
            request.ClientRequestId);
        var line = Assert.Single(request.Lines);
        Assert.Null(line.WarehouseOrderLineGuid);
        Assert.Equal("015681", line.StockCode);
        Assert.Equal(1d, line.Quantity);
    }

    [Fact]
    public void ExistingC01ShipmentFingerprint_MatchesCombinedStockQuantities()
    {
        var document = new AxataOutboundDeliveryDocument(
            265235,
            "F120.42",
            "F120",
            42,
            "C01",
            "0",
            string.Empty,
            string.Empty,
            50,
            120,
            new DateTime(2026, 11, 14),
            [
                new AxataOutboundDeliveryLine(0, "015681", 1d),
                new AxataOutboundDeliveryLine(1, "015681", 2d),
                new AxataOutboundDeliveryLine(2, "015682", 4d)
            ]);
        var candidate = new ExistingC01ShipmentCandidateDocument(
            "F120",
            100,
            "C01-265235",
            new DateTime(2026, 11, 14),
            "F120.42",
            50,
            120,
            2,
            7d,
            [
                new ExistingC01ShipmentCandidateLine("015681", 3d),
                new ExistingC01ShipmentCandidateLine("015682", 4d)
            ]);

        Assert.True(AxataOutboundDeliveryImportService.ShipmentFingerprintMatches(document, candidate));
    }

    [Fact]
    public void ProductFailureMessage_ContainsProductAndServiceReason()
    {
        var results = new[]
        {
            new AxataProductSynchronizationResultDto(
                "015681",
                false,
                12,
                "Barkod bilgisi gecersiz.",
                1,
                1)
        };

        var message = ProductMasterSyncTaskHandler.BuildProductFailureMessage(1, 10, results);

        Assert.Contains("Urun=015681", message);
        Assert.Contains("State=12", message);
        Assert.Contains("Barkod bilgisi gecersiz.", message);
    }

    [Fact]
    public void FormatExceptionMessage_IncludesInnerDatabaseReason()
    {
        var exception = new InvalidOperationException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("The INSERT statement conflicted with a constraint."));

        var message = AxataImportFailureClassifier.FormatExceptionMessage(exception);

        Assert.Contains("An error occurred while saving the entity changes.", message);
        Assert.Contains("The INSERT statement conflicted with a constraint.", message);
    }

    [Fact]
    public void IsDocumentDataFailure_RecognizesMissingDetailInExceptionChain()
    {
        var exception = new InvalidOperationException(
            "Document could not be prepared.",
            new KeyNotFoundException("Company order detail was not found."));

        Assert.True(AxataImportFailureClassifier.IsDocumentDataFailure(exception));
        Assert.False(AxataImportFailureClassifier.IsInfrastructureFailure(exception));
    }

    [Fact]
    public void ExistingC02Shipment_IsReconciledOnlyWhenOrderCustomerWarehouseAndQuantityMatch()
    {
        var orderGuid = Guid.NewGuid();
        var rows = new[]
        {
            new ExistingC02MovementRow(
                orderGuid,
                "A",
                311313672,
                0,
                10d,
                "3312384204",
                50,
                new DateTime(2026, 11, 14),
                new DateTime(2026, 11, 13),
                "A.1979")
        };

        var result = AxataOutboundDeliveryImportService.MatchExistingC02Shipment(
            rows,
            new Dictionary<Guid, double> { [orderGuid] = 10d },
            "3312384204",
            50);

        Assert.Equal("A", result.DocumentSerie);
        Assert.Equal(311313672, result.DocumentOrderNo);
        Assert.Equal(10d, result.TotalQuantity);
    }

    [Theory]
    [InlineData("4730004089", 50, 10d)]
    [InlineData("3312384204", 51, 10d)]
    [InlineData("3312384204", 50, 9d)]
    public void ExistingC02Shipment_IsNotReconciledForDifferentBusinessIdentity(
        string customerCode,
        int warehouseNo,
        double quantity)
    {
        var orderGuid = Guid.NewGuid();
        var rows = new[]
        {
            new ExistingC02MovementRow(
                orderGuid,
                "A",
                311313672,
                0,
                10d,
                customerCode,
                warehouseNo,
                DateTime.Today,
                DateTime.Today,
                "A.1979")
        };

        Assert.ThrowsAny<InvalidOperationException>(() =>
            AxataOutboundDeliveryImportService.MatchExistingC02Shipment(
                rows,
                new Dictionary<Guid, double> { [orderGuid] = quantity },
                "3312384204",
                50));
    }

    private static AxataOutboundDeliveryDocument CreateC01Document(long sequenceNo, DateTime date) =>
        new(
            sequenceNo,
            "F120.42",
            "F120",
            42,
            "C01",
            "0",
            string.Empty,
            string.Empty,
            50,
            120,
            date,
            [new AxataOutboundDeliveryLine(0, "015681", 1d)]);
}
