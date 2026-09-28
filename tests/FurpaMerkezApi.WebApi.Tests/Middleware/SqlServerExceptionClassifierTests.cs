using FurpaMerkezApi.WebApi.Middleware;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Middleware;

public sealed class SqlServerExceptionClassifierTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(121)]
    [InlineData(233)]
    [InlineData(10053)]
    [InlineData(10054)]
    [InlineData(10060)]
    [InlineData(10061)]
    [InlineData(11001)]
    [InlineData(4060)]
    [InlineData(40197)]
    [InlineData(40501)]
    [InlineData(40613)]
    public void IsConnectivityErrorNumber_ReturnsTrueForUnavailableServerErrors(int errorNumber)
    {
        Assert.True(SqlServerExceptionClassifier.IsConnectivityErrorNumber(errorNumber));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(1205)]
    [InlineData(2601)]
    [InlineData(2627)]
    public void IsConnectivityErrorNumber_DoesNotMisclassifyTimeoutDeadlockOrConstraintErrors(int errorNumber)
    {
        Assert.False(SqlServerExceptionClassifier.IsConnectivityErrorNumber(errorNumber));
    }
}
