using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.IrsaliyeKabulleri;

[ApiController]
[Route("api/mal-kabul-islemleri/irsaliye-kabulleri")]
[ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class IrsaliyeKabulleriController
    : ModuleMenuControllerBase
{
    private const string ModuleCode = "mal-kabul-islemleri";
    private const string ModuleName = "MalKabulIslemleri";
    private const string MenuCode = "irsaliye-kabulleri";
    private const string MenuName = "IrsaliyeKabulleri";
    private const string ListPolicy = "mal-kabul-islemleri.irsaliye-kabulleri.list";
    private const string DetailPolicy = "mal-kabul-islemleri.irsaliye-kabulleri.detail";
    private const string CreatePolicy = "mal-kabul-islemleri.irsaliye-kabulleri.create";
    private const string UpdatePolicy = "mal-kabul-islemleri.irsaliye-kabulleri.update";

    public IrsaliyeKabulleriController()
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
