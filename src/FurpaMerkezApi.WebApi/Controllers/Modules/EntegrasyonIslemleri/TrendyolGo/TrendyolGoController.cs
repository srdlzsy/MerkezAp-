using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.TrendyolGo;

[ApiController]
[Route("api/entegrasyon-islemleri/trendyol-go")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class TrendyolGoController(
    ITrendyolGoIntegrationService service,
    ITrendyolGoPriceStockWorkbench priceStockWorkbench)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "entegrasyon-islemleri";
    private const string ModuleName = "EntegrasyonIslemleri";
    private const string MenuCode = "trendyol-go";
    private const string MenuName = "TrendyolGo";
    private const string ListPolicy = "entegrasyon-islemleri.trendyol-go.list";
    private const string DetailPolicy = "entegrasyon-islemleri.trendyol-go.detail";
    private const string UpdatePolicy = "entegrasyon-islemleri.trendyol-go.update";

    [HttpGet]
    [HttpGet("status")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(TrendyolGoConnectionStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrendyolGoConnectionStatusDto>> GetStatus(
        CancellationToken cancellationToken) =>
        Ok(await service.GetStatusAsync(cancellationToken));

    [HttpGet("stores")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TrendyolGoStoreMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TrendyolGoStoreMappingDto>>> ListStores(
        CancellationToken cancellationToken) =>
        Ok(await service.ListStoresAsync(cancellationToken));

    [HttpGet("connection-test")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(TrendyolGoConnectionStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrendyolGoConnectionStatusDto>> TestConnection(
        [FromQuery, Range(1, long.MaxValue)] long storeId,
        CancellationToken cancellationToken) =>
        Ok(await service.TestConnectionAsync(storeId, cancellationToken));

    [HttpGet("orders")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
    public async Task<ActionResult<JsonElement>> ListOrders(
        [FromQuery] TrendyolGoOrderListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListOrdersAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("orders/by-number/{orderNumber}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
    public async Task<ActionResult<JsonElement>> GetOrderByNumber(
        string orderNumber,
        CancellationToken cancellationToken) =>
        Ok(await service.GetOrderByNumberAsync(orderNumber, cancellationToken));

    [HttpPut("packages/{packageId}/picked")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AcceptOrder(
        string packageId,
        CancellationToken cancellationToken)
    {
        var response = await service.AcceptOrderAsync(packageId, cancellationToken);
        return response.HasValue ? Ok(response.Value) : NoContent();
    }

    [HttpGet("orders/{orderId}/invoice-amount")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
    public async Task<ActionResult<JsonElement>> GetInvoiceAmountRange(
        string orderId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetInvoiceAmountRangeAsync(orderId, cancellationToken));

    [HttpPut("packages/{packageId}/invoiced")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkOrderInvoiced(
        string packageId,
        [FromBody] TrendyolGoInvoiceHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.MarkOrderInvoicedAsync(
            packageId,
            request.ToApplicationRequest(),
            cancellationToken);
        return response.HasValue ? Ok(response.Value) : NoContent();
    }

    [HttpGet("brands")]
    [Authorize(Policy = ListPolicy)]
    public async Task<ActionResult<JsonElement>> ListBrands(
        [FromQuery, Range(-1, int.MaxValue)] int page = -1,
        [FromQuery, Range(1, 200)] int size = 50,
        [FromQuery, MaxLength(200)] string? name = null,
        CancellationToken cancellationToken = default) =>
        Ok(await service.ListBrandsAsync(page, size, name, cancellationToken));

    [HttpGet("products")]
    [Authorize(Policy = ListPolicy)]
    public async Task<ActionResult<JsonElement>> ListProducts(
        [FromQuery] TrendyolGoProductListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListProductsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPost("products")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> CreateProducts(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.CreateProductsAsync(payload, cancellationToken));

    [HttpPut("products")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> UpdateProducts(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.UpdateProductsAsync(payload, cancellationToken));

    [HttpPost("products/price-and-inventory")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> UpdatePriceAndInventory(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.UpdatePriceAndInventoryAsync(payload, cancellationToken));

    [HttpGet("price-stock/preview")]
    [Authorize(Policy = ListPolicy)]
    public async Task<ActionResult<TrendyolGoPriceStockPreview>> PreviewPriceStock(
        [FromQuery, Range(1, long.MaxValue)] long storeId,
        [FromQuery, Range(0, int.MaxValue)] int page = 0,
        [FromQuery, Range(1, 100)] int size = 100,
        [FromQuery, RegularExpression("^(?i:actionable|all|issues)$")] string view = "actionable",
        CancellationToken cancellationToken = default) =>
        Ok(await priceStockWorkbench.PreviewAsync(storeId, page, size, view, cancellationToken));

    [HttpPost("price-stock/dispatch")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<ActionResult<TrendyolGoPriceStockDispatch>> DispatchPriceStock(
        [FromBody] TrendyolGoPriceStockDispatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await priceStockWorkbench.DispatchAsync(
            request.StoreId, request.Page, request.Size, request.PreviewHash,
            request.Barcodes, cancellationToken));

    [HttpGet("products/batch-requests/{batchRequestId}")]
    [Authorize(Policy = DetailPolicy)]
    public async Task<ActionResult<JsonElement>> GetProductBatchResult(
        string batchRequestId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetProductBatchResultAsync(batchRequestId, cancellationToken));

    [HttpPut("products/sale-on")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> EnableProducts(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.SetProductsSaleStatusAsync(true, payload, cancellationToken));

    [HttpPut("products/sale-off")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> DisableProducts(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.SetProductsSaleStatusAsync(false, payload, cancellationToken));

    [HttpPost("products/seller-attributes")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> UpdateProductAttributes(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.UpdateProductAttributesAsync(payload, cancellationToken));

    [HttpGet("packages/by-ids")]
    [Authorize(Policy = DetailPolicy)]
    public async Task<ActionResult<JsonElement>> GetOrdersByPackageIds(
        [FromQuery, MinLength(1), MaxLength(200)] string[] id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetOrdersByPackageIdsAsync(id, cancellationToken));

    [HttpPut("packages/{packageId}/items/unsupplied")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> MarkItemsUnsupplied(
        string packageId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.MarkItemsUnsuppliedAsync(packageId, payload, cancellationToken));

    [HttpPut("packages/{packageId}/mark-alternative")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> MarkAlternativeItems(
        string packageId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.MarkAlternativeItemsAsync(packageId, payload, cancellationToken));

    [HttpPut("packages/{packageId}/manual-shipped")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> MarkManualShipment(
        string packageId,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.MarkManualShipmentAsync(packageId, false, cancellationToken));

    [HttpPut("packages/{packageId}/manual-delivered")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> MarkManualDelivery(
        string packageId,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.MarkManualShipmentAsync(packageId, true, cancellationToken));

    [HttpPost("invoice-links")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> SendInvoiceLink(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.SendInvoiceLinkAsync(payload, cancellationToken));

    [HttpGet("claims")]
    [Authorize(Policy = ListPolicy)]
    public async Task<ActionResult<JsonElement>> ListClaims(
        [FromQuery] TrendyolGoClaimListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListClaimsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPut("claims/{claimId}/accept")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> AcceptClaim(
        string claimId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.AcceptClaimAsync(claimId, payload, cancellationToken));

    [HttpPut("claims/{claimId}/reject")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> RejectClaim(
        string claimId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.RejectClaimAsync(claimId, payload, cancellationToken));

    [HttpGet("claims/{claimId}/items/objectionable")]
    [Authorize(Policy = DetailPolicy)]
    public async Task<ActionResult<JsonElement>> GetObjectionableClaimItems(
        string claimId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetObjectionableClaimItemsAsync(claimId, cancellationToken));

    [HttpPost("claims/{claimId}/items/objections")]
    [Authorize(Policy = UpdatePolicy)]
    public async Task<IActionResult> CreateClaimObjection(
        string claimId,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken) =>
        OptionalJson(await service.CreateClaimObjectionAsync(claimId, payload, cancellationToken));

    private IActionResult OptionalJson(JsonElement? response) =>
        response.HasValue ? Ok(response.Value) : NoContent();
}

public sealed class TrendyolGoOrderListHttpRequest
{
    [Range(1, long.MaxValue)]
    public long StoreId { get; init; }

    [Range(0, long.MaxValue)]
    public long? StartDate { get; init; }

    [Range(0, long.MaxValue)]
    public long? EndDate { get; init; }

    [Range(-1, int.MaxValue)]
    public int Page { get; init; } = -1;

    [Range(1, 200)]
    public int Size { get; init; } = 50;

    public string[] Status { get; init; } = [];

    [RegularExpression("^(?i:ASC|DESC)$")]
    public string SortDirection { get; init; } = "DESC";

    public TrendyolGoOrderListRequest ToApplicationRequest() =>
        new(StoreId, StartDate, EndDate, Page, Size, Status, SortDirection.ToUpperInvariant());
}

public sealed class TrendyolGoInvoiceHttpRequest
{
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? InvoiceAmount { get; init; }

    [Range(0, 10)]
    public int? BagCount { get; init; }

    [Url]
    [MaxLength(2048)]
    public string? ReceiptLink { get; init; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? InvoiceTaxAmount { get; init; }

    public TrendyolGoInvoiceRequest ToApplicationRequest() =>
        new(InvoiceAmount, BagCount, ReceiptLink, InvoiceTaxAmount);
}

public sealed class TrendyolGoProductListHttpRequest
{
    [Range(1, long.MaxValue)]
    public long StoreId { get; init; }

    public string? ListType { get; init; }

    [MaxLength(40)]
    public string? Barcode { get; init; }

    [MaxLength(100)]
    public string? StockCode { get; init; }

    [Range(0, long.MaxValue)]
    public long? StartDate { get; init; }

    [Range(0, long.MaxValue)]
    public long? EndDate { get; init; }

    public long[] BrandIds { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int Page { get; init; }

    [Range(1, 100)]
    public int Size { get; init; } = 50;

    public TrendyolGoProductListRequest ToApplicationRequest() =>
        new(StoreId, ListType, Barcode, StockCode, StartDate, EndDate, BrandIds, Page, Size);
}

public sealed class TrendyolGoClaimListHttpRequest
{
    [MaxLength(50)]
    public string? ClaimItemStatus { get; init; }

    [Range(0, long.MaxValue)]
    public long? StartDate { get; init; }

    [Range(0, long.MaxValue)]
    public long? EndDate { get; init; }

    [Range(0, int.MaxValue)]
    public int Page { get; init; }

    [Range(1, 200)]
    public int Size { get; init; } = 50;

    public TrendyolGoClaimListRequest ToApplicationRequest() =>
        new(ClaimItemStatus, StartDate, EndDate, Page, Size);
}

public sealed class TrendyolGoPriceStockDispatchHttpRequest
{
    [Range(1, long.MaxValue)]
    public long StoreId { get; init; }

    [Range(0, int.MaxValue)]
    public int Page { get; init; }

    [Range(1, 100)]
    public int Size { get; init; } = 100;

    [Required]
    public string PreviewHash { get; init; } = string.Empty;

    [Required, MinLength(1), MaxLength(100)]
    public string[] Barcodes { get; init; } = [];
}
