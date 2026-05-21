using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Tags;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KunyeEtiketYazdirma;

[ApiController]
[Route("api/kasa-islemleri/kunye-etiket-yazdirma")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KunyeEtiketYazdirmaController(IListLabelTagsUseCase listLabelTagsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kunye-etiket-yazdirma";
    private const string MenuName = "KunyeEtiketYazdirma";
    private const string ListPolicy = "kasa-islemleri.kunye-etiket-yazdirma.list";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LabelTagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LabelTagDto>>> List(
        [FromQuery] KunyeLabelTagListHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await listLabelTagsUseCase.ExecuteAsync(
            new LabelTagListRequest(
                warehouseNo,
                request.DateToGet!.Value),
            cancellationToken);

        return Ok(response);
    }
}

public sealed class KunyeLabelTagListHttpRequest
{
    [Required]
    public DateTime? DateToGet { get; init; }
}
