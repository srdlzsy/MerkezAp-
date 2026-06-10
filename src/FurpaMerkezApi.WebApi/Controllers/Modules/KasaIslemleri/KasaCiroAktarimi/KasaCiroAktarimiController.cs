using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCiroAktarimi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaCiroAktarimi;

[ApiController]
[Route("api/kasa-islemleri/kasa-ciro-aktarimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaCiroAktarimiController(IKasaCiroAktarimiService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kasa-ciro-aktarimi";
    private const string MenuName = "KasaCiroAktarimi";
    private const string ListPolicy = "kasa-islemleri.kasa-ciro-aktarimi.list";
    private const string CreatePolicy = "kasa-islemleri.kasa-ciro-aktarimi.create";

    [HttpGet("subeler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaCiroBranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<KasaCiroBranchDto>>> ListBranches(
        CancellationToken cancellationToken) =>
        Ok(await service.ListBranchesAsync(cancellationToken));

    [HttpPost("metin/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaCiroImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaCiroImportResultDto>> ImportTextMovements(
        [FromBody] KasaCiroImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportTextMovementsAsync(
            new KasaCiroImportRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.Branches,
                request.MovementRootPath,
                request.DryRun),
            cancellationToken));
}

public sealed class KasaCiroImportHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public IReadOnlyCollection<int>? Branches { get; init; }

    [StringLength(400)]
    public string? MovementRootPath { get; init; }

    public bool DryRun { get; init; }
}
