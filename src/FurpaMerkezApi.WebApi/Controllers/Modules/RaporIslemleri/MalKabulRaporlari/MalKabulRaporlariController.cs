using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.MalKabulRaporlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.MalKabulRaporlari;

[ApiController]
[Route("api/rapor-islemleri/mal-kabul")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class MalKabulRaporlariController(IMalKabulRaporlariUseCase malKabulRaporlariUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "mal-kabul-raporlari";
    private const string MenuName = "MalKabulRaporlari";
    private const string ListPolicy = "rapor-islemleri.mal-kabul-raporlari.list";
    private const string DetailPolicy = "rapor-islemleri.mal-kabul-raporlari.detail";
    private const string AllWarehousesPolicy = "rapor-islemleri.mal-kabul-raporlari.all-warehouses";

    [HttpGet("tembel-subeler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LazyBranchReceivingItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LazyBranchReceivingItemDto>>> LazyBranches(
        [FromQuery] LazyBranchesWarehouseReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseScope(request.WarehouseNo, AllWarehousesPolicy);

        return Ok(await malKabulRaporlariUseCase.GetLazyBranchesAsync(
            new LazyBranchesWarehouseReceivingRequest(
                request.StartDate,
                request.EndDate,
                request.Region,
                resolvedWarehouseNo),
            cancellationToken));
    }

    [HttpGet("tembel-subeler/evraklar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LazyBranchDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LazyBranchDocumentDetailDto>>> LazyBranchDocuments(
        [FromQuery] LazyBranchesWarehouseReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseScope(request.WarehouseNo, AllWarehousesPolicy);

        return Ok(await malKabulRaporlariUseCase.GetLazyBranchDocumentsAsync(
            new LazyBranchesWarehouseReceivingRequest(
                request.StartDate,
                request.EndDate,
                request.Region,
                resolvedWarehouseNo),
            cancellationToken));
    }

    [HttpGet("tedarikci-hacim-analizi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyDocumentByCustomerItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyDocumentByCustomerItemDto>>> CompanyDocumentByCustomer(
        [FromQuery] CompanyDocumentByCustomerHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseScope(request.WarehouseNo, AllWarehousesPolicy);

        return Ok(await malKabulRaporlariUseCase.GetCompanyDocumentByCustomerAsync(
            new CompanyDocumentByCustomerRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                resolvedWarehouseNo,
                request.Take),
            cancellationToken));
    }
}

public sealed class LazyBranchesWarehouseReceivingHttpRequest
{
    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public string? Region { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class CompanyDocumentByCustomerHttpRequest
{
    [Required]
    public DateOnly? StartDate { get; init; }

    [Required]
    public DateOnly? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public int Take { get; init; } = 100;
}
