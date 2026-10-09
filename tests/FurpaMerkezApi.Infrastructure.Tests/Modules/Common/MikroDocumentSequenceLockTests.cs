using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class MikroDocumentSequenceLockTests
{
    [Fact]
    public async Task AcquireAsync_SameResourceTimesOutWhileFirstLeaseIsHeld()
    {
        var operationCode = $"CompanyReceivingCreate:{Guid.NewGuid():N}";
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        await using var firstLease = await MikroDocumentSequenceLock.AcquireAsync(
            firstContext,
            operationCode,
            "Warehouse:110",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            MikroDocumentSequenceLock.AcquireAsync(
                secondContext,
                operationCode,
                "Warehouse:110",
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None));
    }

    [Fact]
    public async Task AcquireAsync_DifferentResourcesDoNotBlockEachOther()
    {
        var operationCode = $"CompanyReceivingCreate:{Guid.NewGuid():N}";
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        await using var firstLease = await MikroDocumentSequenceLock.AcquireAsync(
            firstContext,
            operationCode,
            "Warehouse:110",
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        await using var secondLease = await MikroDocumentSequenceLock.AcquireAsync(
            secondContext,
            operationCode,
            "Warehouse:113",
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.NotNull(secondLease);
    }

    private static MikroWriteDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseInMemoryDatabase($"mikro-document-lock-{Guid.NewGuid():N}")
            .Options;

        return new MikroWriteDbContext(options);
    }
}
