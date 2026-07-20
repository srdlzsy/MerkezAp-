using System.Net;
using System.Net.Http.Json;
using FurpaMerkezApi.WebApi.Tests.Infrastructure;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Security;

public sealed class AuthorizationPipelineTests(FurpaWebApplicationFactory factory)
    : IClassFixture<FurpaWebApplicationFactory>
{
    [Fact]
    public async Task ManavKunyeDetailedLabels_AllowsAnonymousRequests()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/kasa-islemleri/manav-kunye-etiket-yazdirma/detayli-etiketler?warehouseNo=101");

        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("STK-001", content);
    }

    [Fact]
    public async Task FallbackPolicy_RequiresAuthenticationWhenEndpointHasNoAuthorizationMetadata()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/test/fallback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsForbiddenWhenSelfRegistrationIsDisabled()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "new-user",
            email = "new-user@example.local",
            password = "123456",
            firstName = "New",
            lastName = "User",
            warehouseNo = "101",
            warehouseName = "TEST BRANCH"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
