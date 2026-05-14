using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaHareketleri;

[ApiController]
[Route("api/kasa-islemleri/kasa-hareketleri")]
[ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaHareketleriController
    : ModuleMenuControllerBase
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kasa-hareketleri";
    private const string MenuName = "KasaHareketleri";
    private const string ListPolicy = "kasa-islemleri.kasa-hareketleri.list";
    private const string DetailPolicy = "kasa-islemleri.kasa-hareketleri.detail";
    private const string CreatePolicy = "kasa-islemleri.kasa-hareketleri.create";
    private const string UpdatePolicy = "kasa-islemleri.kasa-hareketleri.update";

    public KasaHareketleriController()
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
