using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.Kasiyerler;

[ApiController]
[Route("api/ayar-islemleri/kasiyerler")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasiyerlerController(IAyarlarService ayarlarService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "kasiyerler";
    private const string MenuName = "Kasiyerler";
    private const string ListPolicy = "ayar-islemleri.kasiyerler.list";
    private const string CreatePolicy = "ayar-islemleri.kasiyerler.create";
    private const string UpdatePolicy = "ayar-islemleri.kasiyerler.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<CashierDto>>> List(
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListCashiersAsync(cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CashierPasswordMutationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashierPasswordMutationDto>> Create(
        [FromBody] CreateCashierHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ayarlarService.CreateCashierAsync(
            new CreateCashierRequest(
                request.CashierName!,
                request.CashierAuthorization!),
            User.GetRequiredWarehouseNo(),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{cashierCode:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CashierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashierDto>> Update(
        [Range(1, int.MaxValue)] int cashierCode,
        [FromBody] UpdateCashierHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.UpdateCashierAsync(
            cashierCode,
            new UpdateCashierRequest(
                request.CashierName!,
                request.CashierAuthorization!,
                request.CashierState!.Value),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("{cashierCode:int}/sifre-sifirla")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CashierPasswordMutationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashierPasswordMutationDto>> ResetPassword(
        [Range(1, int.MaxValue)] int cashierCode,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ResetCashierPasswordAsync(
            cashierCode,
            User.GetRequiredWarehouseNo(),
            cancellationToken));
}

public sealed class CreateCashierHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? CashierName { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? CashierAuthorization { get; init; }
}

public sealed class UpdateCashierHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? CashierName { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? CashierAuthorization { get; init; }

    [Required]
    public bool? CashierState { get; init; }
}
