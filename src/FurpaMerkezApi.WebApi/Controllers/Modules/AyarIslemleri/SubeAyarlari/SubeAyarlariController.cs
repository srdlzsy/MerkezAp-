using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.SubeAyarlari;

[ApiController]
[Route("api/ayar-islemleri/sube-ayarlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class SubeAyarlariController(IAyarlarService ayarlarService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "ayar-islemleri";
    private const string ModuleName = "AyarIslemleri";
    private const string MenuCode = "sube-ayarlari";
    private const string MenuName = "SubeAyarlari";
    private const string ListPolicy = "ayar-islemleri.sube-ayarlari.list";
    private const string DetailPolicy = "ayar-islemleri.sube-ayarlari.detail";
    private const string CreatePolicy = "ayar-islemleri.sube-ayarlari.create";
    private const string UpdatePolicy = "ayar-islemleri.sube-ayarlari.update";

    [HttpGet("secenekler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(BranchSettingsLookupsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BranchSettingsLookupsDto>> Lookups(
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.GetBranchSettingsLookupsAsync(cancellationToken));

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BranchDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<BranchDetailDto>>> List(
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListBranchesAsync(cancellationToken));

    [HttpGet("{branchNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(BranchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchDetailDto>> Detail(
        [Range(1, int.MaxValue)] int branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.GetBranchAsync(branchNo, cancellationToken));

    [HttpGet("{branchNo:int}/kasalar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegistryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegistryDto>>> CashRegisters(
        [Range(1, int.MaxValue)] int branchNo,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.ListBranchCashRegistersAsync(branchNo, cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(BranchDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BranchDetailDto>> Create(
        [FromBody] CreateBranchSettingsHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await ayarlarService.CreateBranchAsync(
            new CreateBranchSettingsRequest(
                request.BranchNo!.Value,
                request.BranchIpAddress!,
                request.BranchScalesFolderPath!,
                request.ScalesType!.Value,
                request.PoskonFolderPath!,
                request.PosGenelFolderPath!,
                (request.CashRegisters ?? Array.Empty<CreateCashRegistryHttpRequest>())
                    .Select(item => new CreateCashRegistryRequest(
                        item.CashNo!.Value,
                        item.CashType!.Value))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{branchNo:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(BranchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchDetailDto>> Update(
        [Range(1, int.MaxValue)] int branchNo,
        [FromBody] UpdateBranchSettingsHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ayarlarService.UpdateBranchAsync(
            branchNo,
            new UpdateBranchSettingsRequest(
                request.BranchIpAddress!,
                request.BranchScalesFolderPath!,
                request.ScalesType!.Value,
                request.PoskonFolderPath!,
                request.PosGenelFolderPath!),
            cancellationToken));
}

public sealed class CreateBranchSettingsHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? BranchIpAddress { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? BranchScalesFolderPath { get; init; }

    [Required]
    [Range(0, 1)]
    public byte? ScalesType { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? PoskonFolderPath { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? PosGenelFolderPath { get; init; }

    public IReadOnlyCollection<CreateCashRegistryHttpRequest>? CashRegisters { get; init; }
}

public sealed class UpdateBranchSettingsHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string? BranchIpAddress { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? BranchScalesFolderPath { get; init; }

    [Required]
    [Range(0, 1)]
    public byte? ScalesType { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? PoskonFolderPath { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public string? PosGenelFolderPath { get; init; }
}

public sealed class CreateCashRegistryHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? CashNo { get; init; }

    [Required]
    [Range(0, byte.MaxValue)]
    public byte? CashType { get; init; }
}
