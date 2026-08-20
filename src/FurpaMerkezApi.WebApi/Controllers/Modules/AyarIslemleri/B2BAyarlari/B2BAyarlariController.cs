using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.B2BAyarlari;

[ApiController]
[Route("api/ayar-islemleri/b2b-ayarlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class B2BAyarlariController(IAyarlarService ayarlarService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "b2b-ayarlari";
    private const string MenuName = "B2BAyarlari";
    private const string ListPolicy = "ayar-islemleri.b2b-ayarlari.list";
    private const string DetailPolicy = "ayar-islemleri.b2b-ayarlari.detail";
    private const string CreatePolicy = "ayar-islemleri.b2b-ayarlari.create";
    private const string UpdatePolicy = "ayar-islemleri.b2b-ayarlari.update";
    private const string DeletePolicy = "ayar-islemleri.b2b-ayarlari.delete";

    [HttpGet("bultenler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<B2BBulletinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<B2BBulletinDto>>> Bulletins(
        [FromQuery] B2BBulletinListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListB2BBulletinsAsync(
            request.Search,
            request.Take ?? 100,
            cancellationToken));

    [HttpPost("bultenler")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(B2BBulletinDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<B2BBulletinDto>> CreateBulletin(
        [FromBody] SaveB2BBulletinHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ayarlarService.CreateB2BBulletinAsync(
            new SaveB2BBulletinRequest(
                request.Definition!,
                request.Link!,
                request.CreateDate),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("bultenler/{id:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(B2BBulletinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<B2BBulletinDto>> UpdateBulletin(
        [Range(1, int.MaxValue)] int id,
        [FromBody] SaveB2BBulletinHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.UpdateB2BBulletinAsync(
            id,
            new SaveB2BBulletinRequest(
                request.Definition!,
                request.Link!,
                request.CreateDate),
            cancellationToken));

    [HttpDelete("bultenler/{id:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBulletin(
        [Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        await ayarlarService.DeleteB2BBulletinAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("kullanicilar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<B2BUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<B2BUserDto>>> Users(
        [FromQuery] B2BUserListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListB2BUsersAsync(
            request.Search,
            request.IncludeInactive ?? false,
            request.Take ?? 100,
            cancellationToken));

    [HttpGet("kullanicilar/{userId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(B2BUserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<B2BUserDetailDto>> UserDetail(
        Guid userId,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.GetB2BUserAsync(userId, cancellationToken));

    [HttpPut("kullanicilar/{userId:guid}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(B2BUserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<B2BUserDetailDto>> UpdateUser(
        Guid userId,
        [FromBody] UpdateB2BUserHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.UpdateB2BUserAsync(
            userId,
            new UpdateB2BUserRequest(
                request.UserFullName!,
                request.UserMail!,
                request.Status!.Value,
                request.Menus,
                request.UserEndDate!.Value),
            cancellationToken));
}

public sealed class B2BBulletinListHttpRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class SaveB2BBulletinHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    public string? Definition { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Link { get; init; }

    public DateTime? CreateDate { get; init; }
}

public sealed class B2BUserListHttpRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public bool? IncludeInactive { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class UpdateB2BUserHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(70)]
    public string? UserFullName { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(150)]
    [EmailAddress]
    public string? UserMail { get; init; }

    [Required]
    public bool? Status { get; init; }

    public string? Menus { get; init; }

    [Required]
    public DateTime? UserEndDate { get; init; }
}
