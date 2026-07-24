using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.UrunDagilimlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;

[ApiController]
[Route("api/operasyon-islemleri/urun-dagilimlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class UrunDagilimlariController(IProductDistributionService productDistributionService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "urun-dagilimlari";
    private const string MenuName = "UrunDagilimlari";
    private const string ListPolicy = "operasyon-islemleri.urun-dagilimlari.list";
    private const string DetailPolicy = "operasyon-islemleri.urun-dagilimlari.detail";
    private const string CreatePolicy = "operasyon-islemleri.urun-dagilimlari.create";
    private const string UpdatePolicy = "operasyon-islemleri.urun-dagilimlari.update";
    private const string DeletePolicy = "operasyon-islemleri.urun-dagilimlari.delete";

    [HttpGet("dagitim-merkezleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductDistributionCenterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ProductDistributionCenterDto>>> DistributionCenters(
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.GetDistributionCentersAsync(cancellationToken));

    [HttpPost("oneri")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ProductDistributionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDistributionProposalDto>> Proposal(
        [FromBody] ProductDistributionProposalHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.CreateProposalAsync(
            new ProductDistributionProposalRequest(
                request.StockCode,
                request.DistributionCenterWarehouseNo,
                request.TotalCaseQuantity,
                request.SalesDayCount,
                request.ReferenceDate,
                request.IncludeBranchesWithoutSales),
            cancellationToken));

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductDistributionListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ProductDistributionListItemDto>>> List(
        [FromQuery] ProductDistributionListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.ListAsync(
            new ProductDistributionListRequest(
                request.Status,
                request.DocumentNo,
                request.StockCode,
                request.DistributionCenterWarehouseNo,
                request.CreatedFrom,
                request.CreatedTo,
                request.Take),
            cancellationToken));

    [HttpGet("{documentNo}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ProductDistributionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDistributionDetailDto>> Detail(
        string documentNo,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.GetAsync(documentNo, cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ProductDistributionDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDistributionDetailDto>> Save(
        [FromBody] ProductDistributionSaveHttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productDistributionService.SaveAsync(ToSaveRequest(request), cancellationToken);
        return CreatedAtAction(nameof(Detail), new { documentNo = result.Header.DocumentNo }, result);
    }

    [HttpPut("{documentNo}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ProductDistributionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDistributionDetailDto>> Update(
        string documentNo,
        [FromBody] ProductDistributionSaveHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.UpdateAsync(documentNo, ToSaveRequest(request), cancellationToken));

    [HttpPost("{documentNo}/bilgilendir")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ProductDistributionNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDistributionNotificationDto>> Notify(
        string documentNo,
        [FromBody] ProductDistributionNotifyHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.NotifyAsync(
            documentNo,
            new ProductDistributionNotifyRequest(
                ResolveUserName(request.NotifyBy),
                request.MarkStockOrderingStopped),
            cancellationToken));

    [HttpPost("{documentNo}/kesinlestir")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ProductDistributionFinalizeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDistributionFinalizeDto>> Finalize(
        string documentNo,
        [FromBody] ProductDistributionFinalizeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.FinalizeAsync(
            documentNo,
            new ProductDistributionFinalizeRequest(
                ResolveUserName(request.FinalizeBy),
                request.OrderDate,
                request.DeliveryDate,
                request.AllowFinalizeWithoutNotification),
            cancellationToken));

    [HttpDelete("{documentNo}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(typeof(ProductDistributionDeleteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDistributionDeleteDto>> Delete(
        string documentNo,
        CancellationToken cancellationToken) =>
        Ok(await productDistributionService.DeleteAsync(documentNo, cancellationToken));

    private ProductDistributionSaveRequest ToSaveRequest(ProductDistributionSaveHttpRequest request) =>
        new(
            request.StockCode,
            request.DistributionCenterWarehouseNo,
            request.TotalCaseQuantity,
            ResolveUserName(request.DistributedBy),
            request.Lines
                .Select(line => new ProductDistributionSaveLineRequest(
                    line.WarehouseNo,
                    line.CaseQuantity,
                    line.UnitQuantity,
                    line.LastSalesQuantity,
                    line.CompanyAverageDailySales,
                    line.BranchAverageDailySales))
                .ToArray());

    private string? ResolveUserName(string? requestedName)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return requestedName.Trim();
        }

        return User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Email);
    }
}

public sealed class ProductDistributionProposalHttpRequest
{
    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DistributionCenterWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int TotalCaseQuantity { get; init; }

    [Range(1, 365)]
    public int? SalesDayCount { get; init; }

    public DateTime? ReferenceDate { get; init; }

    public bool IncludeBranchesWithoutSales { get; init; }
}

public sealed class ProductDistributionListHttpRequest
{
    [Range(0, 2)]
    public int? Status { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(1, int.MaxValue)]
    public int? DistributionCenterWarehouseNo { get; init; }

    public DateTime? CreatedFrom { get; init; }

    public DateTime? CreatedTo { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class ProductDistributionSaveHttpRequest
{
    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DistributionCenterWarehouseNo { get; init; }

    [Range(0, int.MaxValue)]
    public int TotalCaseQuantity { get; init; }

    [StringLength(100)]
    public string? DistributedBy { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<ProductDistributionSaveLineHttpRequest> Lines { get; init; } =
        Array.Empty<ProductDistributionSaveLineHttpRequest>();
}

public sealed class ProductDistributionSaveLineHttpRequest
{
    [Range(1, int.MaxValue)]
    public int WarehouseNo { get; init; }

    [Range(0, int.MaxValue)]
    public int CaseQuantity { get; init; }

    [Range(0, int.MaxValue)]
    public int? UnitQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? LastSalesQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? CompanyAverageDailySales { get; init; }

    [Range(0, double.MaxValue)]
    public double? BranchAverageDailySales { get; init; }
}

public sealed class ProductDistributionNotifyHttpRequest
{
    [StringLength(100)]
    public string? NotifyBy { get; init; }

    public bool MarkStockOrderingStopped { get; init; } = true;
}

public sealed class ProductDistributionFinalizeHttpRequest
{
    [StringLength(100)]
    public string? FinalizeBy { get; init; }

    public DateTime? OrderDate { get; init; }

    public DateTime? DeliveryDate { get; init; }

    public bool AllowFinalizeWithoutNotification { get; init; }
}
