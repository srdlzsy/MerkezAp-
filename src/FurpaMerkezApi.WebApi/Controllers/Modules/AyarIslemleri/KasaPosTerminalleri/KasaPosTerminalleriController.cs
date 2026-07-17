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
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListCashRegisterTerminalsAsync(cashNo, cancellationToken));

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
        Ok(await ayarlarService.ReadCashRegisterMessageStatusAsync(branchNo, cancellationToken));

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
                request.BranchNo!.Value,
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
        await ayarlarService.DeleteCashRegisterAsync(branchNo, cashNo, cancellationToken);
        return NoContent();
    }

    [HttpDelete("subeler/{branchNo:int}/terminaller/{terminalNo}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTerminal(
        [Range(1, int.MaxValue)] int branchNo,
        [Required(AllowEmptyStrings = false), StringLength(40)] string terminalNo,
        CancellationToken cancellationToken)
    {
        await ayarlarService.DeleteCashRegisterTerminalAsync(branchNo, terminalNo, cancellationToken);
        return NoContent();
    }
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
