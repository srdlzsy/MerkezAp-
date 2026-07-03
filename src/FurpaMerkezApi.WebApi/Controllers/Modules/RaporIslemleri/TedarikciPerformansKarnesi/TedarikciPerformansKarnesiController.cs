using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.TedarikciPerformansKarnesi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.TedarikciPerformansKarnesi;

[ApiController]
[Route("api/rapor-islemleri/tedarikci-performans-karnesi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class TedarikciPerformansKarnesiController(
    ITedarikciPerformansKarnesiUseCase useCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "tedarikci-performans-karnesi";
    private const string MenuName = "TedarikciPerformansKarnesi";
    private const string ListPolicy = "rapor-islemleri.tedarikci-performans-karnesi.list";
    private const string DetailPolicy = "rapor-islemleri.tedarikci-performans-karnesi.detail";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SupplierPerformanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SupplierPerformanceReportDto>> List(
        [FromQuery] SupplierPerformanceHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await useCase.GetReportAsync(ToApplicationRequest(request, request.CustomerCode), cancellationToken));

    [HttpGet("{customerCode}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(SupplierPerformanceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierPerformanceDetailDto>> Detail(
        [FromRoute] string customerCode,
        [FromQuery] SupplierPerformanceDetailHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await useCase.GetDetailAsync(
            new SupplierPerformanceDetailRequest(
                request.WarehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value,
                customerCode,
                request.EventTake ?? 100),
            cancellationToken));

    private static SupplierPerformanceRequest ToApplicationRequest(
        SupplierPerformanceHttpRequest request,
        string? customerCode) =>
        new(
            request.WarehouseNo,
            request.StartDate!.Value,
            request.EndDate!.Value,
            customerCode,
            request.Take ?? 100);
}

public sealed class SupplierPerformanceHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public string? CustomerCode { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class SupplierPerformanceDetailHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [Range(1, 500)]
    public int? EventTake { get; init; }
}
