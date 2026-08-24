using FurpaMerkezApi.Application.Modules.KasaIslemleri.BirlikKartSorgulama;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.BirlikKartSorgulama;

[ApiController]
[Route("api/kasa-islemleri/birlik-kart-sorgulama")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class BirlikKartSorgulamaController(IBirlikKartSorgulamaUseCase birlikKartSorgulamaUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "birlik-kart-sorgulama";
    private const string MenuName = "BirlikKartSorgulama";
    private const string ListPolicy = "kasa-islemleri.birlik-kart-sorgulama.list";

    [HttpPost("sorgula")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(BirlikKartSorgulamaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BirlikKartSorgulamaResponse>> Sorgula(
        [FromBody] BirlikKartSorgulamaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.KartNo))
        {
            return ValidationProblem("KartNo zorunludur.");
        }

        var response = await birlikKartSorgulamaUseCase.SorgulaAsync(request, cancellationToken);
        return Ok(response);
    }
}
