using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaHareketAktarimi;

[ApiController]
[Route("api/kasa-islemleri/kasa-hareket-aktarimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaHareketAktarimiController(IKasaHareketAktarimiService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kasa-hareket-aktarimi";
    private const string MenuName = "KasaHareketAktarimi";
    private const string ListPolicy = "kasa-islemleri.kasa-hareket-aktarimi.list";
    private const string DetailPolicy = "kasa-islemleri.kasa-hareket-aktarimi.detail";
    private const string CreatePolicy = "kasa-islemleri.kasa-hareket-aktarimi.create";
    private const string UpdatePolicy = "kasa-islemleri.kasa-hareket-aktarimi.update";

    [HttpGet("subeler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaHareketBranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<KasaHareketBranchDto>>> ListBranches(
        CancellationToken cancellationToken) =>
        Ok(await service.ListBranchesAsync(cancellationToken));

    [HttpGet("subeler/{branchNo:int}/kasalar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaHareketCashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<KasaHareketCashRegisterDto>>> ListCashRegisters(
        int branchNo,
        CancellationToken cancellationToken) =>
        Ok(await service.ListCashRegistersAsync(branchNo, cancellationToken));

    [HttpPost("hareketler/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketImportResultDto>> ImportMovements(
        [FromBody] KasaHareketImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportMovementsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPost("iptal-belgeleri/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketImportResultDto>> ImportCancelMovements(
        [FromBody] KasaHareketImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportCancelMovementsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPost("zamanli-aktarim/calistir")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketImportResultDto>> RunScheduledImport(
        [FromBody] KasaHareketScheduledImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.RunScheduledImportAsync(
            new KasaHareketScheduledImportRequest(
                request.Date,
                request.AddDay,
                request.FileRootPath,
                request.SkipExisting,
                request.DryRun),
            cancellationToken));

    [HttpDelete("staging")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> DeleteStaging(
        [FromBody] KasaHareketDeleteStagingHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteStagingMovementsAsync(
            new KasaHareketDeleteStagingRequest(
                request.Date!.Value,
                request.BranchNo,
                request.CashRegisterNo),
            cancellationToken));

    [HttpPost("mikro/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> TransferToMikro(
        [FromBody] KasaHareketMikroTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.TransferMovementsToMikroAsync(
            new KasaHareketMikroTransferRequest(request.Date!.Value, request.BranchNo),
            cancellationToken));

    [HttpDelete("mikro")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> DeleteFromMikro(
        [FromBody] KasaHareketMikroTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteMovementsFromMikroAsync(
            new KasaHareketMikroTransferRequest(request.Date!.Value, request.BranchNo),
            cancellationToken));

    [HttpPost("mikro/aralik-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> TransferRangeToMikro(
        [FromBody] KasaHareketMikroTransferRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.TransferMovementRangeToMikroAsync(
            new KasaHareketMikroTransferRangeRequest(request.StartDate!.Value, request.EndDate!.Value),
            cancellationToken));

    [HttpGet("rapor")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaHareketReportRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<KasaHareketReportRowDto>>> Report(
        [FromQuery] KasaHareketReportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetReportAsync(
            new KasaHareketReportRequest(request.Date!.Value, request.BranchNo, request.CashRegisterNo),
            cancellationToken));
}

public sealed class KasaHareketImportHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public IReadOnlyCollection<int>? Branches { get; init; }

    public IReadOnlyCollection<int>? CashRegisters { get; init; }

    [StringLength(400)]
    public string? FileRootPath { get; init; }

    public bool SkipExisting { get; init; } = true;

    public bool DryRun { get; init; }

    public KasaHareketImportRequest ToApplicationRequest() =>
        new(
            StartDate!.Value,
            EndDate!.Value,
            Branches,
            CashRegisters,
            FileRootPath,
            SkipExisting,
            DryRun);
}

public sealed class KasaHareketScheduledImportHttpRequest
{
    public DateTime? Date { get; init; }

    [Range(-30, 30)]
    public int? AddDay { get; init; }

    [StringLength(400)]
    public string? FileRootPath { get; init; }

    public bool SkipExisting { get; init; } = true;

    public bool DryRun { get; init; }
}

public sealed class KasaHareketDeleteStagingHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }
}

public sealed class KasaHareketMikroTransferHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketMikroTransferRangeHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }
}

public sealed class KasaHareketReportHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }
}
