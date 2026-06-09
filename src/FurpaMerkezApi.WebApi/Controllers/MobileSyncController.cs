using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;
using FurpaMerkezApi.Application.Modules.MobileSync.ProductPriceCatalog;
using FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/mobile-sync")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class MobileSyncController(
    IGetMobileProductPriceCatalogUseCase getMobileProductPriceCatalogUseCase,
    IGetMobileCustomerCatalogUseCase getMobileCustomerCatalogUseCase,
    IGetMobileWarehouseCatalogUseCase getMobileWarehouseCatalogUseCase) : ControllerBase
{
    private const string PriceLookupPolicy = "arama-islemleri.fiyat-gor.list";

    [HttpGet("urun-fiyat-katalogu")]
    [Authorize(Policy = PriceLookupPolicy)]
    [ProducesResponseType(typeof(MobileProductPriceCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MobileProductPriceCatalogResponse>> GetProductPriceCatalog(
        [FromQuery] MobileProductPriceCatalogHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getMobileProductPriceCatalogUseCase.ExecuteAsync(
            new MobileProductPriceCatalogRequest(
                warehouseNo,
                request.Since,
                request.Cursor,
                request.PageSize),
            cancellationToken));
    }

    [HttpGet("cari-katalogu")]
    [ProducesResponseType(typeof(MobileCustomerCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MobileCustomerCatalogResponse>> GetCustomerCatalog(
        [FromQuery] MobileReferenceCatalogHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await getMobileCustomerCatalogUseCase.ExecuteAsync(
            new MobileCustomerCatalogRequest(
                request.Since,
                request.Cursor,
                request.PageSize),
            cancellationToken));

    [HttpGet("depo-katalogu")]
    [ProducesResponseType(typeof(MobileWarehouseCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MobileWarehouseCatalogResponse>> GetWarehouseCatalog(
        [FromQuery] MobileReferenceCatalogHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await getMobileWarehouseCatalogUseCase.ExecuteAsync(
            new MobileWarehouseCatalogRequest(
                request.Since,
                request.Cursor,
                request.PageSize),
            cancellationToken));
}

public sealed class MobileProductPriceCatalogHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? Since { get; init; }

    public string? Cursor { get; init; }

    [Range(1, 10000)]
    public int PageSize { get; init; } = 5000;
}

public sealed class MobileReferenceCatalogHttpRequest
{
    public DateTime? Since { get; init; }

    public string? Cursor { get; init; }

    [Range(1, 10000)]
    public int PageSize { get; init; } = 5000;
}
