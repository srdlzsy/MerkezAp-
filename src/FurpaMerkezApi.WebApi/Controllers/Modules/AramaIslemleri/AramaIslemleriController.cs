using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductLatestTag;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchCustomers;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AramaIslemleri;

[ApiController]
[Authorize]
[Route("api/arama-islemleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class AramaIslemleriController(
    ISearchProductsUseCase searchProductsUseCase,
    ISearchCustomersUseCase searchCustomersUseCase,
    ISearchWarehousesUseCase searchWarehousesUseCase,
    IResolveBarcodeUseCase resolveBarcodeUseCase,
    IGetProductCustomerSuggestionsUseCase getProductCustomerSuggestionsUseCase,
    IGetProductLatestTagUseCase getProductLatestTagUseCase) : ControllerBase
{
    private const string PriceLookupPolicy = "arama-islemleri.fiyat-gor.list";
    private const string BarcodeCustomerLookupPolicy = "arama-islemleri.cari-bul.list";

    [HttpGet("urunler")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductLookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ProductLookupItemDto>>> SearchProducts(
        [FromQuery] ProductSearchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await searchProductsUseCase.ExecuteAsync(
            new ProductSearchRequest(
                warehouseNo,
                request.Barcode,
                request.StockCode,
                request.StockName,
                request.CompanyCode ?? request.SupplierCode,
                request.Take),
            cancellationToken));
    }

    [HttpGet("fiyat-gor")]
    [Authorize(Policy = PriceLookupPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductLookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ProductLookupItemDto>>> GetProductPrices(
        [FromQuery] ProductSearchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await searchProductsUseCase.ExecuteAsync(
            new ProductSearchRequest(
                warehouseNo,
                request.Barcode,
                request.StockCode,
                request.StockName,
                request.CompanyCode ?? request.SupplierCode,
                request.Take),
            cancellationToken));
    }

    [HttpGet("barkodlar/{barcode}/fiyat")]
    [Authorize(Policy = PriceLookupPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductLookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ProductLookupItemDto>>> GetProductPricesByBarcode(
        string barcode,
        [FromQuery] ProductBarcodePriceLookupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedBarcode = NormalizeOrNull(barcode)
            ?? throw new ArgumentException("Barcode is required.", nameof(barcode));
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await searchProductsUseCase.ExecuteAsync(
            new ProductSearchRequest(
                warehouseNo,
                normalizedBarcode,
                null,
                null,
                null,
                request.Take),
            cancellationToken));
    }

    [HttpGet("urunler/{stockCode}/son-kunye")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductLatestTagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductLatestTagDto?>> GetProductLatestTag(
        string stockCode,
        [FromQuery] ProductLatestTagHttpRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedStockCode = NormalizeOrNull(stockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(stockCode));
        var warehouseNo = ResolveWarehouseNo(request.WarehouseNo);

        return Ok(await getProductLatestTagUseCase.ExecuteAsync(
            new ProductLatestTagRequest(
                warehouseNo,
                normalizedStockCode),
            cancellationToken));
    }

    [HttpGet("cariler")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CustomerLookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerLookupItemDto>>> SearchCustomers(
        [FromQuery] CustomerSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await searchCustomersUseCase.ExecuteAsync(
            new CustomerSearchRequest(
                request.SearchText,
                request.Take),
            cancellationToken));

    [HttpGet("depolar")]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseLookupItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseLookupItemDto>>> SearchWarehouses(
        [FromQuery] WarehouseSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await searchWarehousesUseCase.ExecuteAsync(
            new WarehouseSearchRequest(
                request.SearchText,
                request.WarehouseNo,
                request.Take),
            cancellationToken));

    [HttpGet("barkodlar/{barcode}/cozumle")]
    [ProducesResponseType(typeof(BarcodeResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BarcodeResolutionDto>> ResolveBarcode(
        string barcode,
        [FromQuery] BarcodeResolutionHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await resolveBarcodeUseCase.ExecuteAsync(
            new BarcodeResolutionRequest(
                warehouseNo,
                barcode,
                request.ScreenCode),
            cancellationToken));
    }

    [HttpGet("cari-bul")]
    [Authorize(Policy = BarcodeCustomerLookupPolicy)]
    [ProducesResponseType(typeof(BarcodeCustomerSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BarcodeCustomerSuggestionResponse>> FindCustomersByBarcode(
        [FromQuery] BarcodeCustomerLookupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var barcode = NormalizeOrNull(request.Barcode)
            ?? throw new ArgumentException("Barcode is required.", nameof(request.Barcode));
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await FindCustomersByBarcodeAsync(
            warehouseNo,
            barcode,
            request.Take,
            cancellationToken));
    }

    [HttpGet("barkodlar/{barcode}/cariler")]
    [Authorize(Policy = BarcodeCustomerLookupPolicy)]
    [ProducesResponseType(typeof(BarcodeCustomerSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BarcodeCustomerSuggestionResponse>> FindCustomersByBarcodePath(
        string barcode,
        [FromQuery] BarcodeCustomerLookupByPathHttpRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedBarcode = NormalizeOrNull(barcode)
            ?? throw new ArgumentException("Barcode is required.", nameof(barcode));
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await FindCustomersByBarcodeAsync(
            warehouseNo,
            normalizedBarcode,
            request.Take,
            cancellationToken));
    }

    [HttpGet("urunler/{stockCode}/cari-onerileri")]
    [ProducesResponseType(typeof(ProductCustomerSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductCustomerSuggestionResponse>> GetProductCustomerSuggestions(
        string stockCode,
        [FromQuery] ProductCustomerSuggestionHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await getProductCustomerSuggestionsUseCase.ExecuteAsync(
            new ProductCustomerSuggestionRequest(
                stockCode,
                request.Take),
            cancellationToken));

    private async Task<BarcodeCustomerSuggestionResponse> FindCustomersByBarcodeAsync(
        int warehouseNo,
        string barcode,
        int take,
        CancellationToken cancellationToken)
    {
        var resolved = await resolveBarcodeUseCase.ExecuteAsync(
            new BarcodeResolutionRequest(
                warehouseNo,
                barcode,
                "cari-bul"),
            cancellationToken);

        if (!resolved.IsFound || string.IsNullOrWhiteSpace(resolved.StockCode))
        {
            return new BarcodeCustomerSuggestionResponse(
                false,
                resolved.Barcode,
                resolved.WarehouseNo,
                resolved.ResolutionSource,
                resolved.StockCode,
                resolved.StockName,
                resolved.MatchedBarcode,
                resolved.PrimaryBarcode,
                resolved.CaseBarcode,
                resolved.UnitsPerCase,
                resolved.DefaultSupplierCode,
                resolved.DefaultSupplierName,
                Array.Empty<ProductCustomerSuggestionDto>());
        }

        var suggestions = await getProductCustomerSuggestionsUseCase.ExecuteAsync(
            new ProductCustomerSuggestionRequest(resolved.StockCode, take),
            cancellationToken);

        return new BarcodeCustomerSuggestionResponse(
            suggestions.IsProductFound,
            resolved.Barcode,
            resolved.WarehouseNo,
            resolved.ResolutionSource,
            resolved.StockCode,
            suggestions.StockName ?? resolved.StockName,
            resolved.MatchedBarcode,
            resolved.PrimaryBarcode,
            resolved.CaseBarcode,
            resolved.UnitsPerCase,
            suggestions.DefaultSupplierCode ?? resolved.DefaultSupplierCode,
            suggestions.DefaultSupplierName ?? resolved.DefaultSupplierName,
            suggestions.Suggestions);
    }

    private int ResolveWarehouseNo(int? warehouseNo)
    {
        if (warehouseNo.HasValue)
        {
            return warehouseNo.Value;
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return User.GetRequiredWarehouseNo();
        }

        throw new ArgumentException("Warehouse no is required.", nameof(ProductLatestTagHttpRequest.WarehouseNo));
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

public sealed class ProductSearchHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public string? Barcode { get; init; }

    public string? StockCode { get; init; }

    public string? StockName { get; init; }

    public string? SupplierCode { get; init; }

    public string? CompanyCode { get; init; }

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class CustomerSearchHttpRequest
{
    [Required]
    [MinLength(2)]
    public string SearchText { get; init; } = string.Empty;

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class WarehouseSearchHttpRequest
{
    public string? SearchText { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Range(1, 200)]
    public int Take { get; init; } = 100;
}

public sealed class ProductBarcodePriceLookupHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class ProductLatestTagHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class BarcodeResolutionHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [StringLength(64)]
    public string? ScreenCode { get; init; }
}

public sealed class BarcodeCustomerLookupHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(128)]
    public string Barcode { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Range(1, 25)]
    public int Take { get; init; } = 10;
}

public sealed class BarcodeCustomerLookupByPathHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Range(1, 25)]
    public int Take { get; init; } = 10;
}

public sealed class ProductCustomerSuggestionHttpRequest
{
    [Range(1, 25)]
    public int Take { get; init; } = 10;
}
