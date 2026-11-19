using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.ManavKunyeEtiketYazdirma;

[ApiController]
[Route("api/kasa-islemleri/manav-kunye-etiket-yazdirma")]
public sealed class ManavKunyeEtiketYazdirmaController(
    IListKunyeLabelTagsUseCase listKunyeLabelTagsUseCase,
    IListManavCodeListUseCase listManavCodeListUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "manav-kunye-etiket-yazdirma";
    private const string MenuName = "ManavKunyeEtiketYazdirma";
    private const string ListPolicy = "kasa-islemleri.manav-kunye-etiket-yazdirma.list";

    [HttpGet("detayli-etiketler")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyCollection<KunyeLabelTagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<KunyeLabelTagDto>>> List(
        [FromQuery] ManavKunyeDetailedLabelTagListHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await listKunyeLabelTagsUseCase.ExecuteAsync(
            new KunyeLabelTagListRequest(
                request.WarehouseNo!.Value,
                request.DateToGet),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("kod-listesi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavCodeListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ManavCodeListItemDto>>> ListCodeList(
        CancellationToken cancellationToken)
    {
        var response = await listManavCodeListUseCase.ExecuteAsync(cancellationToken);
        return Ok(response);
    }
}

public sealed class ManavKunyeDetailedLabelTagListHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? DateToGet { get; init; }
}
