using System.Net;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoIntegrationServiceTests
{
    [Fact]
    public async Task ListOrders_UsesSupplierStoreFiltersAndBasicToken()
    {
        var handler = new CapturingHandler("{\"content\":[]}");
        var service = CreateService(handler);

        var response = await service.ListOrdersAsync(
            new TrendyolGoOrderListRequest(
                402535,
                1_700_000_000_000,
                1_700_000_100_000,
                2,
                100,
                ["Created", "Picking"],
                "DESC"),
            CancellationToken.None);

        Assert.Equal(JsonValueKind.Object, response.ValueKind);
        Assert.NotNull(handler.Request);
        Assert.Equal("Basic", handler.Request.AuthorizationScheme);
        Assert.Equal("test-token", handler.Request.AuthorizationParameter);
        Assert.Equal("FurpaMerkezApi.Tests", handler.Request.AgentName);
        Assert.Equal("tests@furpa.local", handler.Request.ExecutorUser);
        Assert.Equal(
            "/integrator/order/grocery/suppliers/475658/packages?storeId=402535&page=2&size=100&sortDirection=DESC&startDate=1700000000000&endDate=1700000100000&status=Created&status=Picking",
            handler.Request.PathAndQuery);
    }

    [Fact]
    public async Task MarkOrderInvoiced_SendsCamelCasePayload()
    {
        var handler = new CapturingHandler(string.Empty, HttpStatusCode.NoContent);
        var service = CreateService(handler);

        var response = await service.MarkOrderInvoicedAsync(
            "package-1",
            new TrendyolGoInvoiceRequest(1250.50m, 3, null, 113.68m),
            CancellationToken.None);

        Assert.Null(response);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Put, handler.Request.Method);
        Assert.Equal(
            "/integrator/order/grocery/suppliers/475658/packages/package-1/invoiced",
            handler.Request.PathAndQuery);

        using var payload = JsonDocument.Parse(handler.Request.Body!);
        Assert.Equal(1250.50m, payload.RootElement.GetProperty("invoiceAmount").GetDecimal());
        Assert.Equal(3, payload.RootElement.GetProperty("bagCount").GetInt32());
        Assert.Equal(113.68m, payload.RootElement.GetProperty("invoiceTaxAmount").GetDecimal());
        Assert.False(payload.RootElement.TryGetProperty("receiptLink", out _));
    }

    [Fact]
    public async Task ListStores_ReturnsConfiguredWarehouseMappings()
    {
        var service = CreateService(new CapturingHandler("{}"));

        var stores = await service.ListStoresAsync(CancellationToken.None);

        var store = Assert.Single(stores);
        Assert.Equal(402535, store.StoreId);
        Assert.Equal(110, store.WarehouseNo);
        Assert.Equal("Kestel 1", store.StoreName);
    }

    [Fact]
    public async Task UpdatePriceAndInventory_SendsStoreScopedPayload()
    {
        var handler = new CapturingHandler("{\"batchRequestId\":\"batch-1\"}");
        var service = CreateService(handler);
        using var payload = JsonDocument.Parse("""
        {
          "items": [
            {
              "barcode": "8690000000000",
              "sellingPrice": 99.90,
              "quantity": 25,
              "storeId": 402535
            }
          ]
        }
        """);

        var response = await service.UpdatePriceAndInventoryAsync(
            payload.RootElement,
            CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal(
            "/integrator/product/grocery/suppliers/475658/products/price-and-inventory",
            handler.Request.PathAndQuery);
        Assert.Equal("batch-1", response!.Value.GetProperty("batchRequestId").GetString());

        using var sent = JsonDocument.Parse(handler.Request.Body!);
        Assert.Equal(402535, sent.RootElement.GetProperty("items")[0].GetProperty("storeId").GetInt64());
    }

    [Fact]
    public async Task UpdatePriceAndInventory_RejectsUnmappedStore()
    {
        var service = CreateService(new CapturingHandler("{}"));
        using var payload = JsonDocument.Parse("""
        {
          "items": [
            {
              "barcode": "8690000000000",
              "quantity": 25,
              "storeId": 999999
            }
          ]
        }
        """);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdatePriceAndInventoryAsync(payload.RootElement, CancellationToken.None));

        Assert.Contains("not configured", exception.Message);
    }

    private static TrendyolGoIntegrationService CreateService(CapturingHandler handler)
    {
        var options = new StaticOptionsMonitor<TrendyolGoOptions>(new TrendyolGoOptions
        {
            Enabled = true,
            Environment = "Test",
            BaseUrl = "https://api.tgoapis.com",
            SupplierId = 475658,
            AuthorizationToken = "test-token",
            AgentName = "FurpaMerkezApi.Tests",
            ExecutorUser = "tests@furpa.local",
            Stores =
            [
                new TrendyolGoStoreMappingOptions
                {
                    StoreId = 402535,
                    WarehouseNo = 110,
                    StoreName = "Kestel 1"
                }
            ]
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.tgoapis.com")
        };

        return new TrendyolGoIntegrationService(
            new TrendyolGoApiClient(httpClient, options),
            options);
    }

    private sealed class CapturingHandler(
        string responseBody,
        HttpStatusCode responseStatusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public CapturedRequest? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = new CapturedRequest(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("x-agentname", out var agentNames) ? agentNames.Single() : null,
                request.Headers.TryGetValues("x-executor-user", out var users) ? users.Single() : null,
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(responseStatusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? AgentName,
        string? ExecutorUser,
        string? Body);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
