using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.OfflineSync;

public sealed class SafeWriteClientContractTests
{
    [Theory]
    [InlineData(OperationConflictErrorCodes.MikroDocumentIdentityConflict, "NotStarted", "EditAndRetry")]
    [InlineData(OperationConflictErrorCodes.MikroOfficialDocumentAlreadyReceived, "ExistingDocument", "RefreshList")]
    public void Resolve_MapsCorrectableDocumentConflictsWithoutRetry(
        string errorCode,
        string expectedRecoveryState,
        string expectedAction)
    {
        var result = SafeWriteClientContract.Resolve("Failed", errorCode, retryable: false);

        Assert.Equal(expectedRecoveryState, result.RecoveryState);
        Assert.Equal(expectedAction, result.RecommendedAction);
    }
}
