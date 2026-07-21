using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.StokRaporlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.StokRaporlari;

[ApiController]
[Route("api/rapor-islemleri/stok-raporlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class StokRaporlariController(IStockReportsUseCase stockReportsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "stok-raporlari";
    private const string MenuName = "StokRaporlari";
    private const string ListPolicy = "rapor-islemleri.stok-raporlari.list";

    [HttpGet("son-stok")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(StockOnHandReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockOnHandReportDto>> StockOnHand(
        [FromQuery] StockOnHandReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetStockOnHandAsync(
            new StockOnHandReportRequest(
                User.ResolveWarehouseNo(request.WarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.Search,
                request.SupplierCode,
                request.CategoryCode,
                request.ProducerCode,
                request.ProductManagerCode,
                request.ModelCode,
                request.OnlyWithStock,
                request.Take),
            cancellationToken));

    [HttpGet("tedarikci-son-stok")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(StockOnHandReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockOnHandReportDto>> SupplierStockOnHand(
        [FromQuery] SupplierStockOnHandHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetStockOnHandAsync(
            new StockOnHandReportRequest(
                User.ResolveWarehouseNo(request.WarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.Search,
                request.SupplierCode,
                null,
                null,
                null,
                null,
                request.OnlyWithStock,
                request.Take),
            cancellationToken));

    [HttpGet("kategori-son-stok")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(StockOnHandReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockOnHandReportDto>> CategoryStockOnHand(
        [FromQuery] CategoryStockOnHandHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetStockOnHandAsync(
            new StockOnHandReportRequest(
                User.ResolveWarehouseNo(request.WarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.Search,
                null,
                request.CategoryCode,
                null,
                null,
                null,
                request.OnlyWithStock,
                request.Take),
            cancellationToken));

    [HttpGet("uretici-son-stok")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(StockOnHandReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockOnHandReportDto>> ProducerStockOnHand(
        [FromQuery] ProducerStockOnHandHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetStockOnHandAsync(
            new StockOnHandReportRequest(
                User.ResolveWarehouseNo(request.WarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.Search,
                null,
                null,
                request.ProducerCode,
                null,
                null,
                request.OnlyWithStock,
                request.Take),
            cancellationToken));

    [HttpGet("envanter-degeri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(StockOnHandReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<StockOnHandReportDto>> InventoryValue(
        [FromQuery] StockOnHandReportHttpRequest request,
        CancellationToken cancellationToken) =>
        StockOnHand(request, cancellationToken);

    [HttpGet("urun-depo-durum")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductWarehouseStockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ProductWarehouseStockDto>>> ProductWarehouseStock(
        [FromQuery] ProductWarehouseStockHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetProductWarehouseStockAsync(
            new ProductWarehouseStockRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.StockCodeOrBarcode ?? string.Empty,
                request.OnlyWithStock,
                request.Take),
            cancellationToken));

    [HttpGet("stok-kartlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockCardDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<StockCardDetailDto>>> StockCards(
        [FromQuery] StockCardDetailHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetStockCardDetailsAsync(
            new StockCardDetailRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.Barcode,
                request.StockCode,
                request.StockName,
                request.SupplierCode,
                request.ProductManagerCode,
                request.Take),
            cancellationToken));

    [HttpGet("depoda-var-subede-yok")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseMissingStockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseMissingStockDto>>> WarehouseHasBranchMissing(
        [FromQuery] WarehouseMissingStockHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetWarehouseHasBranchMissingAsync(
            new WarehouseMissingStockRequest(
                request.SourceWarehouseNo!.Value,
                User.ResolveWarehouseNo(request.TargetWarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.Search,
                request.ModelCode,
                request.Take),
            cancellationToken));

    [HttpGet("depo-sifir-stok")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseZeroStockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseZeroStockDto>>> WarehouseZeroStocks(
        [FromQuery] WarehouseZeroStockHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetWarehouseZeroStocksAsync(
            new WarehouseZeroStockRequest(
                User.ResolveWarehouseNo(request.WarehouseNo),
                request.ReportDate ?? DateTime.Today,
                request.ModelCode,
                request.Take),
            cancellationToken));

    [HttpGet("hareketler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockMovementReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<StockMovementReportItemDto>>> StockMovements(
        [FromQuery] StockMovementReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetStockMovementsAsync(
            new StockMovementReportRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.StockCode,
                request.Take),
            cancellationToken));

    [HttpGet("giris-cikis-karsilastirma")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<MovementInOutComparisonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<MovementInOutComparisonDto>>> InOutComparison(
        [FromQuery] FilteredDateRangeReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetInOutComparisonAsync(
            new MovementInOutComparisonRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.FilterType,
                request.FilterValue,
                request.Take),
            cancellationToken));

    [HttpGet("satislar/sube-detay")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BranchSalesReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BranchSalesReportItemDto>>> BranchSales(
        [FromQuery] FilteredDateRangeReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetBranchSalesAsync(
            new BranchSalesReportRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.FilterType,
                request.FilterValue,
                request.Take),
            cancellationToken));

    [HttpGet("satislar/yil-karsilastirma")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YearSalesComparisonItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YearSalesComparisonItemDto>>> YearSalesComparison(
        [FromQuery] FilteredDateRangeReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetYearSalesComparisonAsync(
            new YearSalesComparisonRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.FilterType,
                request.FilterValue,
                request.Take),
            cancellationToken));

    [HttpGet("iadeler/subeler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ReturnBranchReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ReturnBranchReportItemDto>>> ReturnBranches(
        [FromQuery] ReturnBranchReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetReturnBranchesAsync(
            new ReturnBranchReportRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.StockCode ?? string.Empty,
                request.Take),
            cancellationToken));

    [HttpGet("satislar/satmayan-urunler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<NotSoldProductReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<NotSoldProductReportItemDto>>> NotSoldProducts(
        [FromQuery] NotSoldProductReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetNotSoldProductsAsync(
            new NotSoldProductReportRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.ProductManagerCode,
                request.IncludeDls,
                request.Take),
            cancellationToken));

    [HttpGet("karlilik")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProfitabilityReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ProfitabilityReportItemDto>>> Profitability(
        [FromQuery] ProfitabilityReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetProfitabilityAsync(
            new ProfitabilityReportRequest(
                User.ResolveWarehouseScope(request.WarehouseNo),
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.Scope,
                request.FilterValue,
                request.Take),
            cancellationToken));

    [HttpGet("sayim-karsilastirma")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CountingComparisonReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CountingComparisonReportItemDto>>> CountingComparison(
        [FromQuery] CountingComparisonReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await stockReportsUseCase.GetCountingComparisonAsync(
            new CountingComparisonReportRequest(
                User.ResolveWarehouseNo(request.WarehouseNo),
                request.CountDate!.Value,
                request.DocumentNo,
                request.PackageCode,
                request.Take),
            cancellationToken));
}

public sealed class StockOnHandReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [StringLength(25)]
    public string? SupplierCode { get; init; }

    [StringLength(25)]
    public string? CategoryCode { get; init; }

    [StringLength(25)]
    public string? ProducerCode { get; init; }

    [StringLength(25)]
    public string? ProductManagerCode { get; init; }

    [StringLength(25)]
    public string? ModelCode { get; init; }

    public bool OnlyWithStock { get; init; } = true;

    [Range(1, 1000)]
    public int Take { get; init; } = 100;
}

public sealed class SupplierStockOnHandHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [Required]
    [StringLength(25)]
    public string? SupplierCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    public bool OnlyWithStock { get; init; } = true;

    [Range(1, 1000)]
    public int Take { get; init; } = 100;
}

public sealed class CategoryStockOnHandHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [Required]
    [StringLength(25)]
    public string? CategoryCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    public bool OnlyWithStock { get; init; } = true;

    [Range(1, 1000)]
    public int Take { get; init; } = 100;
}

public sealed class ProducerStockOnHandHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [Required]
    [StringLength(25)]
    public string? ProducerCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    public bool OnlyWithStock { get; init; } = true;

    [Range(1, 1000)]
    public int Take { get; init; } = 100;
}

public sealed class ProductWarehouseStockHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [Required]
    [StringLength(50)]
    public string? StockCodeOrBarcode { get; init; }

    public bool OnlyWithStock { get; init; } = true;

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class StockCardDetailHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [StringLength(50)]
    public string? Barcode { get; init; }

    [StringLength(25)]
    public string? StockCode { get; init; }

    [StringLength(100)]
    public string? StockName { get; init; }

    [StringLength(25)]
    public string? SupplierCode { get; init; }

    [StringLength(25)]
    public string? ProductManagerCode { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 100;
}

public sealed class WarehouseMissingStockHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? SourceWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int? TargetWarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [StringLength(25)]
    public string? ModelCode { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class WarehouseZeroStockHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? ReportDate { get; init; }

    [StringLength(25)]
    public string? ModelCode { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class StockMovementReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class FilteredDateRangeReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(30)]
    public string? FilterType { get; init; }

    [StringLength(100)]
    public string? FilterValue { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class ReturnBranchReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [Required]
    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class NotSoldProductReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(25)]
    public string? ProductManagerCode { get; init; }

    public bool IncludeDls { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class ProfitabilityReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(30)]
    public string? Scope { get; init; }

    [StringLength(100)]
    public string? FilterValue { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 250;
}

public sealed class CountingComparisonReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? CountDate { get; init; }

    [Range(0, int.MaxValue)]
    public int? DocumentNo { get; init; }

    [StringLength(25)]
    public string? PackageCode { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 500;
}
