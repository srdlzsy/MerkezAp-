using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;
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
    IGetProductCustomerSuggestionsUseCase getProductCustomerSuggestionsUseCase) : ControllerBase
{
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

public sealed class BarcodeResolutionHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [StringLength(64)]
    public string? ScreenCode { get; init; }
}

public sealed class ProductCustomerSuggestionHttpRequest
{
    [Range(1, 25)]
    public int Take { get; init; } = 10;
}
