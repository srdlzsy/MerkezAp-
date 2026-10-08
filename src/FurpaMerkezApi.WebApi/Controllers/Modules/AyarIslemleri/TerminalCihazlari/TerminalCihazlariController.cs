using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.TerminalCihazlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.TerminalCihazlari;

[ApiController]
[Route("api/ayar-islemleri/terminal-cihazlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class TerminalCihazlariController(ITerminalInstallationService service)
    : ModuleMenuControllerBase("ayar-islemleri", "AyarIslemleri", "terminal-cihazlari", "TerminalCihazlari")
{
    private const string ListPolicy = "ayar-islemleri.terminal-cihazlari.list";
    private const string DetailPolicy = "ayar-islemleri.terminal-cihazlari.detail";

    [HttpGet("ozet")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(TerminalInstallationSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TerminalInstallationSummaryDto>> Summary(
        [FromQuery] int? warehouseNo,
        CancellationToken cancellationToken) =>
        Ok(await service.GetSummaryAsync(
            User.ResolveWarehouseScopeForPolicy(warehouseNo, ListPolicy),
            cancellationToken));

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TerminalInstallationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TerminalInstallationDto>>> List(
        [FromQuery] TerminalInstallationListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(
            new TerminalInstallationListRequest(
                User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy),
                request.Search,
                request.AppVersion,
                request.IsCurrentVersion,
                request.ActiveWithinDays,
                request.Take),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(TerminalInstallationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminalInstallationDto>> Detail(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(
            id,
            User.ResolveWarehouseScopeForPolicy(null, DetailPolicy),
            cancellationToken));
}

public sealed class TerminalInstallationListHttpRequest
{
    public int? WarehouseNo { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [StringLength(40)]
    public string? AppVersion { get; init; }

    public bool? IsCurrentVersion { get; init; }

    [Range(1, 3650)]
    public int? ActiveWithinDays { get; init; }

    [Range(1, 1000)]
    public int Take { get; init; } = 200;
}
