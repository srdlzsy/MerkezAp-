using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.OfflineSync;

public sealed class WriteTimeoutClassifierTests
{
    [Theory]
    [MemberData(nameof(TimeoutExceptions))]
    public void IsTimeout_RecognizesTimeoutExceptionChains(Exception exception)
    {
        Assert.True(WriteTimeoutClassifier.IsTimeout(exception));
    }

    [Fact]
    public void IsTimeout_DoesNotClassifyOrdinaryBusinessFailure()
    {
        var exception = new InvalidOperationException(
            "Business validation failed.",
            new ArgumentException("Invalid quantity."));

        Assert.False(WriteTimeoutClassifier.IsTimeout(exception));
    }

    [Theory]
    [InlineData(-2, true)]
    [InlineData(1205, false)]
    [InlineData(2601, false)]
    [InlineData(2627, false)]
    public void IsSqlCommandTimeoutNumber_OnlyAcceptsSqlCommandTimeout(int number, bool expected)
    {
        Assert.Equal(expected, WriteTimeoutClassifier.IsSqlCommandTimeoutNumber(number));
    }

    [Theory]
    [InlineData("MikroAPI - TimeOut")]
    [InlineData("Execution Timeout Expired.")]
    [InlineData("The operation timed out.")]
    [InlineData("The request reached its time out limit.")]
    public void ContainsTimeoutMarker_RecognizesSupportedTimeoutMessages(string message)
    {
        Assert.True(WriteTimeoutClassifier.ContainsTimeoutMarker(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Gider muhasebe kodu bos.")]
    public void ContainsTimeoutMarker_LeavesOtherMessagesUnclassified(string? message)
    {
        Assert.False(WriteTimeoutClassifier.ContainsTimeoutMarker(message));
    }

    [Fact]
    public void OfflineFailureClassifier_MapsTimeoutToRetryableUnconfirmedOutcome()
    {
        var failure = OfflineWriteFailureClassifier.Classify(
            new InvalidOperationException("Write failed.", new TimeoutException("SQL command timed out.")));

        Assert.Equal(OfflineWriteFailureClassifier.UnconfirmedMessage, failure.Message);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, failure.ErrorCode);
        Assert.True(failure.Retryable);
    }

    [Fact]
    public void OfflineFailureClassifier_PreservesTypedConflictDecision()
    {
        var failure = OfflineWriteFailureClassifier.Classify(
            new OperationConflictException(
                OperationConflictErrorCodes.MikroWriteQueueBusy,
                "The write queue is busy.",
                retryable: true));

        Assert.Equal("The write queue is busy.", failure.Message);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteQueueBusy, failure.ErrorCode);
        Assert.True(failure.Retryable);
    }

    [Fact]
    public void OfflineFailureClassifier_MapsMissingReferenceToEditableFailure()
    {
        var failure = OfflineWriteFailureClassifier.Classify(
            new KeyNotFoundException("Customer was not found."));

        Assert.Equal(OperationConflictErrorCodes.CreateReferenceNotFound, failure.ErrorCode);
        Assert.False(failure.Retryable);
    }

    [Fact]
    public void OfflineFailureClassifier_MapsMikroBusinessRuleWithoutManualReview()
    {
        var failure = OfflineWriteFailureClassifier.Classify(
            new InvalidOperationException("U17 Uyari Kodu: miktar gecersiz."));

        Assert.Equal(OperationConflictErrorCodes.MikroBusinessRuleRejected, failure.ErrorCode);
        Assert.False(failure.Retryable);
    }

    [Fact]
    public void OfflineFailureClassifier_TreatsDatabaseUpdateFailureAsUnconfirmed()
    {
        var failure = OfflineWriteFailureClassifier.Classify(
            new DbUpdateException("Save failed."));

        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, failure.ErrorCode);
        Assert.True(failure.Retryable);
    }

    public static TheoryData<Exception> TimeoutExceptions => new()
    {
        new TimeoutException("SQL command timed out."),
        new OperationCanceledException("Operation canceled by an internal timeout."),
        new TaskCanceledException("HTTP request timed out."),
        new InvalidOperationException("Outer failure.", new TimeoutException("Inner timeout.")),
        new AggregateException(
            new InvalidOperationException("First failure."),
            new TimeoutException("Second failure timed out."))
    };
}
