using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.KasaPosTerminalleri;

[ApiController]
[Route("api/ayar-islemleri/kasa-pos-terminalleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaPosTerminalleriController(IAyarlarService ayarlarService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "kasa-pos-terminalleri";
    private const string MenuName = "KasaPosTerminalleri";
    private const string ListPolicy = "ayar-islemleri.kasa-pos-terminalleri.list";
    private const string CreatePolicy = "ayar-islemleri.kasa-pos-terminalleri.create";
    private const string UpdatePolicy = "ayar-islemleri.kasa-pos-terminalleri.update";

    [HttpGet("secenekler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CashRegisterSettingsLookupsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CashRegisterSettingsLookupsDto>> Lookups(
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.GetCashRegisterSettingsLookupsAsync(cancellationToken));

    [HttpGet("kasalar/{cashNo:int}/terminaller")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterTerminalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterTerminalDto>>> Terminals(
        [Range(1, int.MaxValue)] int cashNo,
        [FromQuery, Range(1, int.MaxValue)] int? branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListCashRegisterTerminalsAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, ListPolicy),
            cashNo,
            cancellationToken));

    [HttpGet("terminaller/konum")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterTerminalLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterTerminalLocationDto>>> TerminalLocations(
        [FromQuery, Required(AllowEmptyStrings = false), StringLength(40)] string terminalNo,
        [FromQuery, Range(1, int.MaxValue)] int? branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.FindCashRegisterTerminalLocationsAsync(
            terminalNo,
            User.ResolveWarehouseScopeForPolicy(branchNo, ListPolicy),
            cancellationToken));

    [HttpGet("tanim-sorunlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterDefinitionIssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterDefinitionIssueDto>>> DefinitionIssues(
        [FromQuery, Range(1, int.MaxValue)] int? branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListCashRegisterDefinitionIssuesAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, ListPolicy),
            cancellationToken));

    [HttpPost("subeler/{branchNo:int}/kasalar/{cashNo:int}/terminaller")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CashRegisterTerminalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterTerminalDto>> CreateTerminal(
        [Range(1, int.MaxValue)] int branchNo,
        [Range(1, int.MaxValue)] int cashNo,
        [FromBody] CreateCashRegisterTerminalHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ayarlarService.CreateCashRegisterTerminalAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, CreatePolicy),
            cashNo,
            ToTerminalRequest(request),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("subeler/{branchNo:int}/kasalar/{cashNo:int}/terminaller/{terminalId:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CashRegisterTerminalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterTerminalDto>> UpdateTerminal(
        [Range(1, int.MaxValue)] int branchNo,
        [Range(1, int.MaxValue)] int cashNo,
        [Range(1, int.MaxValue)] int terminalId,
        [FromBody] CreateCashRegisterTerminalHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.UpdateCashRegisterTerminalAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, UpdatePolicy),
            cashNo,
            terminalId,
            ToTerminalRequest(request),
            cancellationToken));

    [HttpPut("subeler/{branchNo:int}/kasalar/{cashNo:int}/tip")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CashRegistryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashRegistryDto>> UpdateCashRegisterType(
        [Range(1, int.MaxValue)] int branchNo,
        [Range(1, int.MaxValue)] int cashNo,
        [FromBody] UpdateCashRegisterTypeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.UpdateCashRegisterTypeAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, UpdatePolicy),
            cashNo,
            new UpdateCashRegisterTypeRequest(request.CashType!.Value),
            cancellationToken));

    [HttpPut("subeler/{branchNo:int}/kasalar/{cashNo:int}/terminal-gruplari/{terminalNo}/tasi")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterTerminalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterTerminalDto>>> MoveTerminalGroup(
        [Range(1, int.MaxValue)] int branchNo,
        [Range(1, int.MaxValue)] int cashNo,
        [StringLength(40, MinimumLength = 1)] string terminalNo,
        [FromBody] MoveCashRegisterTerminalGroupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var sourceBranchNo = User.ResolveWarehouseNoForPolicy(branchNo, UpdatePolicy);
        var targetBranchNo = User.ResolveWarehouseNoForPolicy(request.TargetBranchNo, UpdatePolicy);

        return Ok(await ayarlarService.MoveCashRegisterTerminalGroupAsync(
            sourceBranchNo,
            cashNo,
            terminalNo,
            new MoveCashRegisterTerminalGroupRequest(targetBranchNo, request.TargetCashNo!.Value),
            cancellationToken));
    }

    [HttpGet("mevcut-sube/mesaj-durumlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterMessageStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterMessageStatusDto>>> CurrentBranchMessageStatus(
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ReadCashRegisterMessageStatusAsync(
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpGet("subeler/{branchNo:int}/mesaj-durumlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterMessageStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterMessageStatusDto>>> BranchMessageStatus(
        [Range(1, int.MaxValue)] int branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ReadCashRegisterMessageStatusAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, ListPolicy),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CashRegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterResponse>> Create(
        [FromBody] CreateCashRegisterHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ayarlarService.CreateCashRegisterAsync(
            new CreateCashRegisterRequest(
                User.ResolveWarehouseNoForPolicy(request.BranchNo, CreatePolicy),
                request.CashNo!.Value,
                request.CashType!.Value,
                request.Terminals
                    .Select(item => new CreateCashRegisterTerminalRequest(
                        item.TerminalNo!,
                        item.Bank!,
                        item.TerminalId!,
                        item.MerchantNo!))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("subeler/{branchNo:int}/kasalar/{cashNo:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCashRegister(
        [Range(1, int.MaxValue)] int branchNo,
        [Range(1, int.MaxValue)] int cashNo,
        CancellationToken cancellationToken)
    {
        await ayarlarService.DeleteCashRegisterAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, UpdatePolicy),
            cashNo,
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("subeler/{branchNo:int}/kasalar/{cashNo:int}/terminaller/{terminalId:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTerminal(
        [Range(1, int.MaxValue)] int branchNo,
        [Range(1, int.MaxValue)] int cashNo,
        [Range(1, int.MaxValue)] int terminalId,
        CancellationToken cancellationToken)
    {
        await ayarlarService.DeleteCashRegisterTerminalAsync(
            User.ResolveWarehouseNoForPolicy(branchNo, UpdatePolicy),
            cashNo,
            terminalId,
            cancellationToken);
        return NoContent();
    }

    private static CreateCashRegisterTerminalRequest ToTerminalRequest(
        CreateCashRegisterTerminalHttpRequest request) =>
        new(
            request.TerminalNo!,
            request.Bank!,
            request.TerminalId!,
            request.MerchantNo!);
}

public sealed class CreateCashRegisterHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CashNo { get; init; }

    [Required]
    [Range(0, byte.MaxValue)]
    public byte? CashType { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateCashRegisterTerminalHttpRequest> Terminals { get; init; } =
        Array.Empty<CreateCashRegisterTerminalHttpRequest>();
}

public sealed class CreateCashRegisterTerminalHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(40)]
    public string? TerminalNo { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? Bank { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(40)]
    public string? TerminalId { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(40)]
    public string? MerchantNo { get; init; }
}

public sealed class MoveCashRegisterTerminalGroupHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? TargetBranchNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? TargetCashNo { get; init; }
}

public sealed class UpdateCashRegisterTypeHttpRequest
{
    [Required]
    [Range(0, byte.MaxValue)]
    public byte? CashType { get; init; }
}
