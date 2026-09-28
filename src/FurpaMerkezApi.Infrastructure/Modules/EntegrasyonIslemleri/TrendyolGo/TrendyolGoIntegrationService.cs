using System.Globalization;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoIntegrationService(
    TrendyolGoApiClient apiClient,
    IOptionsMonitor<TrendyolGoOptions> options)
    : ITrendyolGoIntegrationService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Created",
        "Picking",
        "Invoiced",
        "Shipped",
        "Cancelled",
        "Delivered",
        "Returned",
        "UnPacked",
        "UnSupplied"
    };

    public Task<TrendyolGoConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        return Task.FromResult(CreateStatus(
            currentOptions,
            reachable: null,
            upstreamStatusCode: null,
            currentOptions.Enabled ? "Configuration loaded." : "Integration is disabled."));
    }

    public Task<IReadOnlyCollection<TrendyolGoStoreMappingDto>> ListStoresAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TrendyolGoStoreMappingDto> stores = options.CurrentValue.Stores
            .Where(store => store.StoreId > 0 && store.WarehouseNo > 0)
            .OrderBy(store => store.WarehouseNo)
            .Select(store => new TrendyolGoStoreMappingDto(
                store.StoreId,
                store.WarehouseNo,
                store.StoreName))
            .ToArray();

        return Task.FromResult(stores);
    }

    public async Task<TrendyolGoConnectionStatusDto> TestConnectionAsync(
        long storeId,
        CancellationToken cancellationToken)
    {
        if (storeId <= 0)
        {
            throw new ArgumentException("storeId must be greater than zero.", nameof(storeId));
        }

        var currentOptions = options.CurrentValue;
        var path = BuildOrdersPath(new TrendyolGoOrderListRequest(
            storeId,
            StartDate: null,
            EndDate: null,
            Page: 0,
            Size: 1,
            Statuses: [],
            SortDirection: "DESC"));
        var response = await apiClient.GetAsync(path, cancellationToken);

        return CreateStatus(
            currentOptions,
            response.IsSuccessStatusCode,
            response.StatusCode,
            response.IsSuccessStatusCode
                ? "Trendyol Go connection succeeded."
                : $"Trendyol Go returned HTTP {response.StatusCode}: {ReadErrorMessage(response.RawBody)}");
    }

    public async Task<JsonElement> ListOrdersAsync(
        TrendyolGoOrderListRequest request,
        CancellationToken cancellationToken)
    {
        ValidateOrderListRequest(request);
        var response = await apiClient.GetAsync(BuildOrdersPath(request), cancellationToken);
        return ReadRequiredJson(response, "Order list");
    }

    public async Task<JsonElement> GetOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        orderNumber = RequireIdentifier(orderNumber, nameof(orderNumber));
        var supplierId = GetSupplierId();
        var response = await apiClient.GetAsync(
            $"/integrator/order/grocery/suppliers/{supplierId}/packages/order-number/{Uri.EscapeDataString(orderNumber)}",
            cancellationToken);
        return ReadRequiredJson(response, "Order detail");
    }

    public async Task<JsonElement?> AcceptOrderAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        packageId = RequireIdentifier(packageId, nameof(packageId));
        var supplierId = GetSupplierId();
        var response = await apiClient.PutAsync(
            $"/integrator/order/grocery/suppliers/{supplierId}/packages/{Uri.EscapeDataString(packageId)}/picked",
            payload: null,
            cancellationToken);
        return ReadOptionalJson(response, "Order acceptance");
    }

    public async Task<JsonElement> GetInvoiceAmountRangeAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        orderId = RequireIdentifier(orderId, nameof(orderId));
        var supplierId = GetSupplierId();
        var response = await apiClient.GetAsync(
            $"/integrator/order/grocery/suppliers/{supplierId}/orders/{Uri.EscapeDataString(orderId)}/invoice-amount",
            cancellationToken);
        return ReadRequiredJson(response, "Invoice amount range");
    }

    public async Task<JsonElement?> MarkOrderInvoicedAsync(
        string packageId,
        TrendyolGoInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        packageId = RequireIdentifier(packageId, nameof(packageId));

        if (request.BagCount is < 0 or > 10)
        {
            throw new ArgumentException("bagCount must be between 0 and 10.", nameof(request));
        }

        if (request.InvoiceAmount < 0 || request.InvoiceTaxAmount < 0)
        {
            throw new ArgumentException("Invoice amounts cannot be negative.", nameof(request));
        }

        var supplierId = GetSupplierId();
        var response = await apiClient.PutAsync(
            $"/integrator/order/grocery/suppliers/{supplierId}/packages/{Uri.EscapeDataString(packageId)}/invoiced",
            new
            {
                request.InvoiceAmount,
                request.BagCount,
                request.ReceiptLink,
                request.InvoiceTaxAmount
            },
            cancellationToken);
        return ReadOptionalJson(response, "Order invoiced notification");
    }

    public async Task<JsonElement> ListBrandsAsync(
        int page,
        int size,
        string? name,
        CancellationToken cancellationToken)
    {
        if (page < 0 || size is < 1 or > 200)
        {
            throw new ArgumentException("Brand page must be non-negative and size must be between 1 and 200.");
        }

        var path = string.IsNullOrWhiteSpace(name)
            ? $"/integrator/product/grocery/brands?page={page}&size={size}"
            : $"/integrator/product/grocery/brands/by-name?name={Uri.EscapeDataString(name.Trim())}";
        return ReadRequiredJson(await apiClient.GetAsync(path, cancellationToken), "Brand list");
    }

    public async Task<JsonElement?> CreateProductsAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ValidateItemsPayload(payload, 1000, "Product create");
        return ReadOptionalJson(
            await apiClient.PostAsync(ProductRootPath(), payload, cancellationToken),
            "Product create");
    }

    public async Task<JsonElement?> UpdateProductsAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ValidateItemsPayload(payload, 1000, "Product update");
        return ReadOptionalJson(
            await apiClient.PutAsync(ProductRootPath(), payload, cancellationToken),
            "Product update");
    }

    public async Task<JsonElement?> UpdatePriceAndInventoryAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ValidatePriceAndInventoryPayload(payload);
        return ReadOptionalJson(
            await apiClient.PostAsync($"{ProductRootPath()}/price-and-inventory", payload, cancellationToken),
            "Price and inventory update");
    }

    public async Task<JsonElement> GetProductBatchResultAsync(
        string batchRequestId,
        CancellationToken cancellationToken)
    {
        batchRequestId = RequireIdentifier(batchRequestId, nameof(batchRequestId));
        return ReadRequiredJson(
            await apiClient.GetAsync(
                $"{ProductRootPath()}/batch-requests/{Uri.EscapeDataString(batchRequestId)}",
                cancellationToken),
            "Product batch result");
    }

    public async Task<JsonElement> ListProductsAsync(
        TrendyolGoProductListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StoreId <= 0 || request.Page < 0 || request.Size is < 1 or > 100)
        {
            throw new ArgumentException("storeId must be positive, page non-negative and size between 1 and 100.");
        }

        var allowedListTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OUT_OF_STOCK", "REJECTED", "ON_SALE", "ALL_PRODUCT", "NOT_ON_SALE", "LOCKED"
        };
        if (!string.IsNullOrWhiteSpace(request.ListType) && !allowedListTypes.Contains(request.ListType))
        {
            throw new ArgumentException($"Unsupported product listType '{request.ListType}'.");
        }

        var query = new List<string>
        {
            $"page={request.Page}",
            $"size={request.Size}"
        };
        AddQuery(query, "listType", request.ListType);
        AddQuery(query, "barcode", request.Barcode);
        AddQuery(query, "stockCode", request.StockCode);
        AddQuery(query, "startDate", request.StartDate);
        AddQuery(query, "endDate", request.EndDate);
        query.AddRange(request.BrandIds.Select(id => $"brandIds={id.ToString(CultureInfo.InvariantCulture)}"));

        var response = await apiClient.GetAsync(
            $"/integrator/product/grocery/suppliers/{GetSupplierId()}/stores/{request.StoreId}/products?{string.Join('&', query)}",
            cancellationToken);
        return ReadRequiredJson(response, "Product list");
    }

    public async Task<JsonElement?> SetProductsSaleStatusAsync(
        bool enabled,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ValidateItemsPayload(payload, 1000, enabled ? "Product sale-on" : "Product sale-off");
        return ReadOptionalJson(
            await apiClient.PutAsync(
                $"{ProductRootPath()}/{(enabled ? "sale-on" : "sale-off")}",
                payload,
                cancellationToken),
            enabled ? "Product sale-on" : "Product sale-off");
    }

    public async Task<JsonElement?> UpdateProductAttributesAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        ValidateItemsPayload(payload, 1000, "Product attribute update");
        return ReadOptionalJson(
            await apiClient.PostAsync($"{ProductRootPath()}/seller-attributes", payload, cancellationToken),
            "Product attribute update");
    }

    public async Task<JsonElement> GetOrdersByPackageIdsAsync(
        IReadOnlyCollection<string> packageIds,
        CancellationToken cancellationToken)
    {
        if (packageIds.Count is < 1 or > 200)
        {
            throw new ArgumentException("At least one and at most 200 package IDs must be supplied.", nameof(packageIds));
        }

        var query = string.Join('&', packageIds.Select(id =>
            $"id={Uri.EscapeDataString(RequireIdentifier(id, nameof(packageIds)))}"));
        return ReadRequiredJson(
            await apiClient.GetAsync($"{OrderPackagesRootPath()}/ids?{query}", cancellationToken),
            "Order packages by IDs");
    }

    public Task<JsonElement?> MarkItemsUnsuppliedAsync(
        string packageId,
        JsonElement payload,
        CancellationToken cancellationToken) =>
        PutPackageOperationAsync(packageId, "items/unsupplied", payload, "Unsupplied item notification", cancellationToken);

    public Task<JsonElement?> MarkAlternativeItemsAsync(
        string packageId,
        JsonElement payload,
        CancellationToken cancellationToken) =>
        PutPackageOperationAsync(packageId, "mark-alternative", payload, "Alternative item notification", cancellationToken);

    public Task<JsonElement?> MarkManualShipmentAsync(
        string packageId,
        bool delivered,
        CancellationToken cancellationToken) =>
        PutPackageOperationAsync(
            packageId,
            delivered ? "manual-delivered" : "manual-shipped",
            payload: null,
            delivered ? "Manual delivery notification" : "Manual shipment notification",
            cancellationToken);

    public async Task<JsonElement?> SendInvoiceLinkAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        RequireObjectPayload(payload, "Invoice link");
        return ReadOptionalJson(
            await apiClient.PostAsync(
                $"/integrator/invoice/grocery/suppliers/{GetSupplierId()}/supplier-invoice-links/instant",
                payload,
                cancellationToken),
            "Invoice link");
    }

    public async Task<JsonElement> ListClaimsAsync(
        TrendyolGoClaimListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 0 || request.Size is < 1 or > 200)
        {
            throw new ArgumentException("Claim page must be non-negative and size must be between 1 and 200.");
        }

        var query = new List<string> { $"page={request.Page}", $"size={request.Size}" };
        AddQuery(query, "claimItemStatus", request.ClaimItemStatus);
        AddQuery(query, "startDate", request.StartDate);
        AddQuery(query, "endDate", request.EndDate);
        return ReadRequiredJson(
            await apiClient.GetAsync($"{ClaimRootPath()}?{string.Join('&', query)}", cancellationToken),
            "Claim list");
    }

    public Task<JsonElement?> AcceptClaimAsync(
        string claimId,
        JsonElement payload,
        CancellationToken cancellationToken) =>
        PutClaimOperationAsync(claimId, "accept", payload, "Claim acceptance", cancellationToken);

    public Task<JsonElement?> RejectClaimAsync(
        string claimId,
        JsonElement payload,
        CancellationToken cancellationToken) =>
        PutClaimOperationAsync(claimId, "reject", payload, "Claim rejection", cancellationToken);

    public async Task<JsonElement> GetObjectionableClaimItemsAsync(
        string claimId,
        CancellationToken cancellationToken)
    {
        claimId = RequireIdentifier(claimId, nameof(claimId));
        return ReadRequiredJson(
            await apiClient.GetAsync(
                $"{ClaimRootPath()}/{Uri.EscapeDataString(claimId)}/items/objectionable",
                cancellationToken),
            "Objectionable claim items");
    }

    public async Task<JsonElement?> CreateClaimObjectionAsync(
        string claimId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        claimId = RequireIdentifier(claimId, nameof(claimId));
        RequireObjectPayload(payload, "Claim objection");
        return ReadOptionalJson(
            await apiClient.PostAsync(
                $"{ClaimRootPath()}/{Uri.EscapeDataString(claimId)}/items/objections",
                payload,
                cancellationToken),
            "Claim objection");
    }

    private long GetSupplierId()
    {
        var supplierId = options.CurrentValue.SupplierId;

        if (supplierId <= 0)
        {
            throw new InvalidOperationException("TrendyolGo:SupplierId must be configured.");
        }

        return supplierId;
    }

    private string ProductRootPath() =>
        $"/integrator/product/grocery/suppliers/{GetSupplierId()}/products";

    private string OrderPackagesRootPath() =>
        $"/integrator/order/grocery/suppliers/{GetSupplierId()}/packages";

    private string ClaimRootPath() =>
        $"/integrator/claim/grocery/suppliers/{GetSupplierId()}/claims";

    private async Task<JsonElement?> PutPackageOperationAsync(
        string packageId,
        string operation,
        object? payload,
        string operationName,
        CancellationToken cancellationToken)
    {
        packageId = RequireIdentifier(packageId, nameof(packageId));
        if (payload is JsonElement element)
        {
            RequireObjectPayload(element, operationName);
        }

        return ReadOptionalJson(
            await apiClient.PutAsync(
                $"{OrderPackagesRootPath()}/{Uri.EscapeDataString(packageId)}/{operation}",
                payload,
                cancellationToken),
            operationName);
    }

    private async Task<JsonElement?> PutClaimOperationAsync(
        string claimId,
        string operation,
        JsonElement payload,
        string operationName,
        CancellationToken cancellationToken)
    {
        claimId = RequireIdentifier(claimId, nameof(claimId));
        RequireObjectPayload(payload, operationName);
        return ReadOptionalJson(
            await apiClient.PutAsync(
                $"{ClaimRootPath()}/{Uri.EscapeDataString(claimId)}/{operation}",
                payload,
                cancellationToken),
            operationName);
    }

    private static void ValidateItemsPayload(JsonElement payload, int maximumItemCount, string operationName)
    {
        RequireObjectPayload(payload, operationName);
        if (!payload.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"{operationName} payload must contain an items array.");
        }

        var itemCount = items.GetArrayLength();
        if (itemCount is < 1 || itemCount > maximumItemCount)
        {
            throw new ArgumentException(
                $"{operationName} payload must contain between 1 and {maximumItemCount} items.");
        }
    }

    private void ValidatePriceAndInventoryPayload(JsonElement payload)
    {
        ValidateItemsPayload(payload, 1000, "Price and inventory update");
        foreach (var item in payload.GetProperty("items").EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("barcode", out var barcode) ||
                barcode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(barcode.GetString()))
            {
                throw new ArgumentException("Every price and inventory item must contain a barcode.");
            }

            ValidateNonNegativeNumber(item, "sellingPrice");
            ValidateNonNegativeNumber(item, "quantity");

            if (item.TryGetProperty("storeId", out var storeId) && storeId.ValueKind != JsonValueKind.Null)
            {
                if (!storeId.TryGetInt64(out var value) || value <= 0)
                {
                    throw new ArgumentException("storeId must be a positive integer when supplied.");
                }

                if (!options.CurrentValue.Stores.Any(store => store.StoreId == value))
                {
                    throw new ArgumentException($"storeId {value} is not configured in TrendyolGo:Stores.");
                }
            }
        }
    }

    private static void ValidateNonNegativeNumber(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number) || number < 0)
        {
            throw new ArgumentException($"{propertyName} must be a non-negative number when supplied.");
        }
    }

    private static void RequireObjectPayload(JsonElement payload, string operationName)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"{operationName} payload must be a JSON object.");
        }
    }

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static void AddQuery(List<string> query, string name, long? value)
    {
        if (value.HasValue)
        {
            query.Add($"{name}={value.Value.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    private string BuildOrdersPath(TrendyolGoOrderListRequest request)
    {
        var query = new List<string>
        {
            $"storeId={request.StoreId.ToString(CultureInfo.InvariantCulture)}",
            $"page={request.Page.ToString(CultureInfo.InvariantCulture)}",
            $"size={request.Size.ToString(CultureInfo.InvariantCulture)}",
            $"sortDirection={Uri.EscapeDataString(request.SortDirection)}"
        };

        if (request.StartDate.HasValue)
        {
            query.Add($"startDate={request.StartDate.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.EndDate.HasValue)
        {
            query.Add($"endDate={request.EndDate.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        query.AddRange(request.Statuses.Select(status => $"status={Uri.EscapeDataString(status)}"));
        return $"/integrator/order/grocery/suppliers/{GetSupplierId()}/packages?{string.Join('&', query)}";
    }

    private static void ValidateOrderListRequest(TrendyolGoOrderListRequest request)
    {
        if (request.StoreId <= 0)
        {
            throw new ArgumentException("storeId must be greater than zero.", nameof(request));
        }

        if (request.Page < 0)
        {
            throw new ArgumentException("page cannot be negative.", nameof(request));
        }

        if (request.Size is < 1 or > 200)
        {
            throw new ArgumentException("size must be between 1 and 200.", nameof(request));
        }

        if (!request.SortDirection.Equals("ASC", StringComparison.OrdinalIgnoreCase) &&
            !request.SortDirection.Equals("DESC", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("sortDirection must be ASC or DESC.", nameof(request));
        }

        var invalidStatus = request.Statuses.FirstOrDefault(status => !AllowedStatuses.Contains(status));

        if (invalidStatus is not null)
        {
            throw new ArgumentException($"Unsupported Trendyol Go order status '{invalidStatus}'.", nameof(request));
        }
    }

    private static string RequireIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }

    private static JsonElement ReadRequiredJson(TrendyolGoApiResponse response, string operationName) =>
        ReadOptionalJson(response, operationName)
        ?? throw new HttpRequestException($"Trendyol Go {operationName} returned an empty response.");

    private static JsonElement? ReadOptionalJson(TrendyolGoApiResponse response, string operationName)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Trendyol Go {operationName} failed with HTTP {response.StatusCode}: {ReadErrorMessage(response.RawBody)}");
        }

        if (string.IsNullOrWhiteSpace(response.RawBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(response.RawBody);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException(
                $"Trendyol Go {operationName} returned invalid JSON.",
                exception);
        }
    }

    private static string ReadErrorMessage(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return "Empty response body.";
        }

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            foreach (var propertyName in new[] { "message", "errorDescription", "error" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.GetString()))
                {
                    return property.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // The upstream may return a plain-text gateway error.
        }

        return rawBody.Length <= 500 ? rawBody : rawBody[..500];
    }

    private static TrendyolGoConnectionStatusDto CreateStatus(
        TrendyolGoOptions currentOptions,
        bool? reachable,
        int? upstreamStatusCode,
        string message) =>
        new(
            currentOptions.Enabled,
            currentOptions.Environment,
            currentOptions.BaseUrl,
            currentOptions.SupplierId,
            currentOptions.IntegrationReferenceCode,
            HasCredentials(currentOptions),
            reachable,
            upstreamStatusCode,
            message,
            DateTime.UtcNow);

    private static bool HasCredentials(TrendyolGoOptions currentOptions) =>
        !string.IsNullOrWhiteSpace(currentOptions.AuthorizationToken) ||
        (!string.IsNullOrWhiteSpace(currentOptions.ApiKey) &&
         !string.IsNullOrWhiteSpace(currentOptions.ApiSecret));
}
