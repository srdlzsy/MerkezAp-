using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.KasaRaporlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.KasaRaporlari;

[ApiController]
[Route("api/rapor-islemleri/kasa-raporlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaRaporlariController(IKasaRaporlariUseCase kasaRaporlariUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "kasa-raporlari";
    private const string MenuName = "KasaRaporlari";
    private const string ListPolicy = "rapor-islemleri.kasa-raporlari.list";
    private const string DetailPolicy = "rapor-islemleri.kasa-raporlari.detail";
    private const string AllWarehousesPolicy = "rapor-islemleri.kasa-raporlari.all-warehouses";

    [HttpGet("gider-pusulalari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ExpenseSlipZoneSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseSlipZoneSummaryDto>>> ExpenseSlips(
        [FromQuery] ExpenseSlipReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseScope(request.WarehouseNo, AllWarehousesPolicy);

        return Ok(await kasaRaporlariUseCase.GetExpenseSlipHierarchyReportAsync(
            new ExpenseSlipReportRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                resolvedWarehouseNo,
                request.CashRegisterNo,
                request.CashierCode),
            cancellationToken));
    }

    [HttpGet("gider-pusulalari/detaylar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ExpenseSlipDetailItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseSlipDetailItemDto>>> ExpenseSlipDetails(
        [FromQuery] ExpenseSlipReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseScope(request.WarehouseNo, AllWarehousesPolicy);

        return Ok(await kasaRaporlariUseCase.GetExpenseSlipDetailsAsync(
            new ExpenseSlipReportRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                resolvedWarehouseNo,
                request.CashRegisterNo,
                request.CashierCode),
            cancellationToken));
    }
}

public sealed class ExpenseSlipReportHttpRequest
{
    [Required]
    public DateOnly? StartDate { get; init; }

    [Required]
    public DateOnly? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public int? CashRegisterNo { get; init; }

    public string? CashierCode { get; init; }
}
