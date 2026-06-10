using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.Cihazlar;

[ApiController]
[Route("api/ayar-islemleri/cihazlar")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class CihazlarController(IAyarlarService ayarlarService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "cihazlar";
    private const string MenuName = "Cihazlar";
    private const string ListPolicy = "ayar-islemleri.cihazlar.list";
    private const string CreatePolicy = "ayar-islemleri.cihazlar.create";
    private const string UpdatePolicy = "ayar-islemleri.cihazlar.update";

    [HttpGet("tipler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DeviceTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<DeviceTypeDto>>> DeviceTypes(
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListDeviceTypesAsync(cancellationToken));

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<DeviceDto>>> List(
        [FromQuery, Range(1, int.MaxValue)] int? branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListDevicesAsync(branchNo, cancellationToken));

    [HttpGet("durum")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DeviceStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<DeviceStatusDto>>> Status(
        [FromQuery, Range(1, int.MaxValue)] int? branchNo,
        CancellationToken cancellationToken)
    {
        var resolvedBranchNo = branchNo ?? User.GetRequiredWarehouseNo();
        return Ok(await ayarlarService.CheckDeviceStatusAsync(resolvedBranchNo, cancellationToken));
    }

    [HttpGet("subeler/{branchNo:int}/durum")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DeviceStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<DeviceStatusDto>>> BranchStatus(
        [Range(1, int.MaxValue)] int branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.CheckDeviceStatusAsync(branchNo, cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceDto>> Create(
        [FromBody] CreateDeviceHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ayarlarService.CreateDeviceAsync(
            new CreateDeviceRequest(
                request.BranchNo!.Value,
                request.DeviceTypeId!.Value,
                request.IpAddress!,
                request.Description!),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        await ayarlarService.DeleteDeviceAsync(id, cancellationToken);
        return NoContent();
    }
}

public sealed class CreateDeviceHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? DeviceTypeId { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? IpAddress { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? Description { get; init; }
}
