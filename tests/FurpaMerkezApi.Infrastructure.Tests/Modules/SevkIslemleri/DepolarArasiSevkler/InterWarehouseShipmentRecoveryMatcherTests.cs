using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class InterWarehouseShipmentRecoveryMatcherTests
{
    [Fact]
    public void QueueBusyConflict_IsRetryableAndUsesStableErrorCode()
    {
        var exception = CreateInterWarehouseShipmentUseCase.CreateShipmentCreateQueueBusyConflict("F56");

        Assert.Equal(OperationConflictErrorCodes.MikroWriteQueueBusy, exception.ErrorCode);
        Assert.True(exception.Retryable);
        Assert.Contains("180 seconds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("F56", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedOrderOverDelivery_IsAllowedOnlyWhenExplicitlyRequested()
    {
        Assert.True(CreateInterWarehouseShipmentUseCase.IsLinkedOrderOverDeliveryBlocked(
            allowLinkedOrderOverDelivery: false,
            requestedQuantity: 384d,
            remainingQuantity: 380d));
        Assert.False(CreateInterWarehouseShipmentUseCase.IsLinkedOrderOverDeliveryBlocked(
            allowLinkedOrderOverDelivery: true,
            requestedQuantity: 384d,
            remainingQuantity: 380d));
    }

    [Theory]
    [InlineData(20d, 24d, 0)]
    [InlineData(20d, 24.001d, 1)]
    [InlineData(20d, 29.999d, 1)]
    [InlineData(20d, 30d, 2)]
    [InlineData(20d, 45d, 2)]
    public void GreenGrocerShipmentSeverity_UsesToleranceAndCriticalLimit(
        double expectedQuantity,
        double actualQuantity,
        int expectedSeverity)
    {
        var severity = CreateInterWarehouseShipmentUseCase.EvaluateGreenGrocerShipmentSeverity(
            expectedQuantity,
            actualQuantity,
            tolerancePercent: 20d,
            severeMultiplier: 1.5d);

        Assert.Equal(expectedSeverity, (int)severity);
    }

    [Fact]
    public void GreenGrocerCaseCount_IsSummedOnlyWhenEverySplitLineHasCaseCount()
    {
        Assert.Equal(
            3.75d,
            CreateInterWarehouseShipmentUseCase.SumCompleteGreenGrocerCaseCount([1.25d, 2.5d]));
        Assert.Null(
            CreateInterWarehouseShipmentUseCase.SumCompleteGreenGrocerCaseCount([1.25d, null]));
        Assert.Null(
            CreateInterWarehouseShipmentUseCase.SumCompleteGreenGrocerCaseCount([]));
    }

    [Fact]
    public void GreenGrocerOrderSnapshot_TracksPartialAndCompletedShipmentTotals()
    {
        var userId = Guid.Parse("619ce5f5-71f8-4499-9bc8-8ed07fdd558d");
        var snapshot = new GreenGrocerOrderLineSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "F56",
            10,
            0,
            new DateTime(2026, 11, 25),
            56,
            120,
            "015792",
            4d,
            "Case",
            "LabelAverageKgPerCase",
            5d,
            null,
            20d,
            "KG",
            "LabelHistory",
            10,
            40,
            0.1d,
            "High",
            false,
            false,
            20d,
            1.5d,
            userId,
            DateTime.UtcNow);

        snapshot.RecordShipment(8d, 1.6d, userId, DateTime.UtcNow);
        Assert.Equal("PartiallyShipped", snapshot.Status);
        Assert.Equal(8d, snapshot.ActualShippedQuantity);
        Assert.Equal(1.6d, snapshot.ActualShippedCaseCount);

        snapshot.RecordShipment(20d, 4d, userId, DateTime.UtcNow);
        Assert.Equal("Shipped", snapshot.Status);
    }

    [Fact]
    public void Matches_ReturnsTrueWhenDocumentLinesAndClientTraceMatch()
    {
        var clientRequestId = Guid.Parse("a94669b8-b916-474a-9830-bbf87cc9f408");
        var request = CreateRequest(clientRequestId);
        var rows = CreateMatchingRows(MobileOfflineSyncService.ToTraceKey(clientRequestId));

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_ReturnsFalseWhenSequenceBelongsToAnotherClientRequest()
    {
        var request = CreateRequest(Guid.Parse("a94669b8-b916-474a-9830-bbf87cc9f408"));
        var rows = CreateMatchingRows("FRDIFFERENTTRACE0000000");

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_ReturnsFalseWhenSequenceContainsDifferentMovementLines()
    {
        var request = CreateRequest(null);
        var rows = CreateMatchingRows(string.Empty);
        rows[1] = rows[1] with { Quantity = 99d };

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    [Fact]
    public void Evaluate_DistinguishesMissingTraceFromExistingMismatchedContent()
    {
        var request = CreateRequest(Guid.Parse("a94669b8-b916-474a-9830-bbf87cc9f408"));
        var rows = CreateMatchingRows(MobileOfflineSyncService.ToTraceKey(request.ClientRequestId!.Value));
        rows[0] = rows[0] with { Quantity = 99d };

        Assert.Equal(
            InterWarehouseShipmentRecoveryMatch.NotFound,
            InterWarehouseShipmentRecoveryMatcher.Evaluate(request, request.Lines.ToArray(), []));
        Assert.Equal(
            InterWarehouseShipmentRecoveryMatch.ContentMismatch,
            InterWarehouseShipmentRecoveryMatcher.Evaluate(request, request.Lines.ToArray(), rows));
        Assert.Contains(
            "line[0].quantity",
            InterWarehouseShipmentRecoveryMatcher.GetMismatchFields(request, request.Lines.ToArray(), rows));
        Assert.Equal(
            InterWarehouseShipmentRecoveryMatch.ExactMatch,
            InterWarehouseShipmentRecoveryMatcher.Evaluate(
                request,
                request.Lines.ToArray(),
                CreateMatchingRows(MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value))));
    }

    [Fact]
    public void Matches_ReturnsFalseWhenSequenceContainsExtraLines()
    {
        var request = CreateRequest(null);
        var rows = CreateMatchingRows(string.Empty).ToList();
        rows.Add(rows[1] with { RowNo = 2 });

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_AcceptsFieldsNormalizedByMikroWhenCoreIdentityMatches()
    {
        var clientRequestId = Guid.Parse("a94669b8-b916-474a-9830-bbf87cc9f408");
        var request = CreateRequest(clientRequestId) with
        {
            Description = null,
            Lines = CreateRequest(clientRequestId).Lines
                .Select(line => line with { Description = null })
                .ToArray()
        };
        var rows = CreateMatchingRows(MobileOfflineSyncService.ToTraceKey(clientRequestId));
        rows[0] = rows[0] with
        {
            Amount = 0d,
            Description = "bcddffbf-eb56-4701-877c-641eeba08855",
            PartyCode = "MIKRO-NORMALIZED",
            LotNo = 99,
            ProjectCode = "MIKRO",
            CustomerResponsibilityCenter = "AUTO",
            ProductResponsibilityCenter = "AUTO"
        };
        rows[1] = rows[1] with
        {
            Amount = 999d,
            Description = "9400e969-3098-4cf8-81e5-80817c15dc75"
        };

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_RejectsDifferentStockEvenWhenOptionalFieldsMatch()
    {
        var request = CreateRequest(null) with
        {
            Description = null,
            Lines = CreateRequest(null).Lines
                .Select(line => line with { Description = null })
                .ToArray()
        };
        var rows = CreateMatchingRows(string.Empty);
        rows[0] = rows[0] with { StockCode = "DIFFERENT" };

        var matches = InterWarehouseShipmentRecoveryMatcher.Matches(
            request,
            request.Lines.ToArray(),
            rows);

        Assert.False(matches);
    }

    private static CreateInterWarehouseShipmentRequest CreateRequest(Guid? clientRequestId) =>
        new(
            56,
            120,
            60,
            new DateTime(2026, 9, 28),
            new DateTime(2026, 9, 28),
            "TEST-1",
            "Sevk",
            [
                new CreateInterWarehouseShipmentLineRequest(
                    "001082",
                    12.5d,
                    UnitPrice: 20d,
                    UnitPointer: 1,
                    Description: "Satir 1",
                    PartyCode: "P1",
                    LotNo: 3,
                    ProjectCode: "PRJ",
                    CustomerResponsibilityCenter: "CSR",
                    ProductResponsibilityCenter: "SSR"),
                new CreateInterWarehouseShipmentLineRequest(
                    "016167",
                    25d,
                    UnitPrice: 2d,
                    UnitPointer: 2,
                    Description: "Satir 2")
            ],
            ClientRequestId: clientRequestId);

    private static InterWarehouseShipmentRecoveryLine[] CreateMatchingRows(string traceKey) =>
    [
        new InterWarehouseShipmentRecoveryLine(
            0,
            "001082",
            12.5d,
            1,
            250d,
            "Satir 1",
            "P1",
            3,
            "PRJ",
            "CSR",
            "SSR",
            traceKey),
        new InterWarehouseShipmentRecoveryLine(
            1,
            "016167",
            25d,
            2,
            50d,
            "Satir 2",
            null,
            0,
            null,
            null,
            null,
            traceKey)
    ];
}
