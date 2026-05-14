using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.IadeIslemleri.MusteriIadeleri;

[ApiController]
[Route("api/iade-islemleri/musteri-iadeleri")]
[ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class MusteriIadeleriController
    : ModuleMenuControllerBase
{
    private const string ModuleCode = "iade-islemleri";
    private const string ModuleName = "IadeIslemleri";
    private const string MenuCode = "musteri-iadeleri";
    private const string MenuName = "MusteriIadeleri";
    private const string ListPolicy = "iade-islemleri.musteri-iadeleri.list";
    private const string DetailPolicy = "iade-islemleri.musteri-iadeleri.detail";
    private const string CreatePolicy = "iade-islemleri.musteri-iadeleri.create";
    private const string UpdatePolicy = "iade-islemleri.musteri-iadeleri.update";

    public MusteriIadeleriController()
        : base(ModuleCode, ModuleName, MenuCode, MenuName)
    {
    }

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    public ActionResult<ModuleActionScaffoldResponse> List() =>
        ListNotImplemented(ListPolicy);

    [HttpGet("{id}")]
    [Authorize(Policy = DetailPolicy)]
    public ActionResult<ModuleActionScaffoldResponse> Detail(string id) =>
        DetailNotImplemented(DetailPolicy, id);

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    public ActionResult<ModuleActionScaffoldResponse> Create([FromBody] ModuleActionRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}
