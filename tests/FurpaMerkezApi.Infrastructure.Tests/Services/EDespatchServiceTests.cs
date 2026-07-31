using FurpaMerkezApi.Infrastructure.Services;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services;

public sealed class EDespatchServiceTests
{
    [Theory]
    [InlineData("ORHAN BAYRAM", "ORHAN", "BAYRAM")]
    [InlineData("ORHAN ALI BAYRAM", "ORHAN ALI", "BAYRAM")]
    [InlineData("ORHAN", "ORHAN", "ORHAN")]
    public void SplitPersonName_ReturnsSchemaSafeNameParts(
        string value,
        string expectedFirstName,
        string expectedFamilyName)
    {
        var result = EDespatchService.SplitPersonName(value);

        Assert.Equal(expectedFirstName, result.FirstName);
        Assert.Equal(expectedFamilyName, result.FamilyName);
    }
}
