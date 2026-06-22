using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.ManavKunyeEtiketYazdirma;

[ApiController]
[Route("api/kasa-islemleri/manav-kunye-etiket-yazdirma")]
public sealed class ManavKunyeEtiketYazdirmaController(IListKunyeLabelTagsUseCase listKunyeLabelTagsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "manav-kunye-etiket-yazdirma";
    private const string MenuName = "ManavKunyeEtiketYazdirma";

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
}

public sealed class ManavKunyeDetailedLabelTagListHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? DateToGet { get; init; }
}
