using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Soforler;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.Soforler;

[ApiController]
[Route("api/ayar-islemleri/soforler")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class SoforlerController(IDespatchDriverService driverService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "soforler";
    private const string MenuName = "Soforler";
    private const string ListPolicy = "ayar-islemleri.soforler.list";
    private const string DetailPolicy = "ayar-islemleri.soforler.detail";
    private const string CreatePolicy = "ayar-islemleri.soforler.create";
    private const string UpdatePolicy = "ayar-islemleri.soforler.update";
    private const string DeletePolicy = "ayar-islemleri.soforler.delete";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DespatchDriverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<DespatchDriverDto>>> List(
        [FromQuery] DespatchDriverListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await driverService.ListAsync(
            new DespatchDriverListRequest(
                request.Search,
                request.IncludeInactive,
                request.Take ?? 100),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(DespatchDriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DespatchDriverDto>> Detail(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await driverService.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(DespatchDriverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DespatchDriverDto>> Create(
        [FromBody] SaveDespatchDriverHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await driverService.CreateAsync(
            request.ToApplicationRequest(),
            User.GetRequiredUserId(),
            cancellationToken);

        return CreatedAtAction(nameof(Detail), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(DespatchDriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DespatchDriverDto>> Update(
        Guid id,
        [FromBody] SaveDespatchDriverHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await driverService.UpdateAsync(
            id,
            request.ToApplicationRequest(),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await driverService.DeleteAsync(id, User.GetRequiredUserId(), cancellationToken);

        return NoContent();
    }
}

public sealed class DespatchDriverListHttpRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public bool IncludeInactive { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class SaveDespatchDriverHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(60)]
    public string? FirstName { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(60)]
    public string? LastName { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(20)]
    public string? PlateNumber { get; init; }

    [Required(AllowEmptyStrings = false)]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "TCKN must be 11 digits.")]
    public string? Tckn { get; init; }

    public bool IsActive { get; init; } = true;

    [StringLength(1000)]
    public string? Notes { get; init; }

    public SaveDespatchDriverRequest ToApplicationRequest() =>
        new(
            FirstName!,
            LastName!,
            PlateNumber!,
            Tckn!,
            IsActive,
            Notes);
}
