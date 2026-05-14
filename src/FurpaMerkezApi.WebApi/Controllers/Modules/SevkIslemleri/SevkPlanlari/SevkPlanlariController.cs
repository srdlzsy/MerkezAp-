using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SevkIslemleri.SevkPlanlari;

[ApiController]
[Route("api/sevk-islemleri/sevk-planlari")]
[ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class SevkPlanlariController
    : ModuleMenuControllerBase
{
    private const string ModuleCode = "sevk-islemleri";
    private const string ModuleName = "SevkIslemleri";
    private const string MenuCode = "sevk-planlari";
    private const string MenuName = "SevkPlanlari";
    private const string ListPolicy = "sevk-islemleri.sevk-planlari.list";
    private const string DetailPolicy = "sevk-islemleri.sevk-planlari.detail";
    private const string CreatePolicy = "sevk-islemleri.sevk-planlari.create";
    private const string UpdatePolicy = "sevk-islemleri.sevk-planlari.update";

    public SevkPlanlariController()
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
