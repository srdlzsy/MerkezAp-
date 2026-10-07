using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.VeritabaniIzleme;

[ApiController]
[Route("api/ayar-islemleri/veritabani-izleme")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class VeritabaniIzlemeController(IDatabaseMonitoringService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "veritabani-izleme";
    private const string MenuName = "VeritabaniIzleme";
    private const string ListPolicy = "ayar-islemleri.veritabani-izleme.list";
    private const string DetailPolicy = "ayar-islemleri.veritabani-izleme.detail";
    private const string TerminatePolicy = "ayar-islemleri.veritabani-izleme.terminate-session";

    [HttpGet("anlik")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(DatabaseMonitoringSnapshotDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DatabaseMonitoringSnapshotDto>> Snapshot(CancellationToken cancellationToken) =>
        Ok(await service.GetSnapshotAsync(cancellationToken));

    [HttpGet("olaylar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DatabaseMonitoringIncidentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<DatabaseMonitoringIncidentDto>>> Incidents(
        [FromQuery, Range(1, 500)] int take = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetIncidentsAsync(take, cancellationToken));

    [HttpGet("oturum-sonlandirma-gecmisi")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DatabaseSessionTerminationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<DatabaseSessionTerminationDto>>> TerminationHistory(
        [FromQuery, Range(1, 500)] int take = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetTerminationHistoryAsync(take, cancellationToken));

    [HttpPost("oturumlar/{sessionId:int}/sonlandir")]
    [Authorize(Policy = TerminatePolicy)]
    [ProducesResponseType(typeof(DatabaseSessionTerminationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DatabaseSessionTerminationDto>> Terminate(
        int sessionId,
        [FromBody] TerminateDatabaseSessionHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.TerminateSessionAsync(
            sessionId,
            new TerminateDatabaseSessionRequest(
                request.ExpectedLoginTime,
                request.ExpectedHostProcessId,
                request.ExpectedProgramName,
                request.Reason!),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("oturumlar/{sessionId:int}/rollback-durumu")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(DatabaseRollbackStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DatabaseRollbackStatusDto>> RollbackStatus(
        int sessionId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetRollbackStatusAsync(sessionId, cancellationToken));
}

public sealed class TerminateDatabaseSessionHttpRequest
{
    [Required]
    public DateTime ExpectedLoginTime { get; init; }

    public int? ExpectedHostProcessId { get; init; }

    [StringLength(256)]
    public string? ExpectedProgramName { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(500, MinimumLength = 10)]
    public string? Reason { get; init; }
}
