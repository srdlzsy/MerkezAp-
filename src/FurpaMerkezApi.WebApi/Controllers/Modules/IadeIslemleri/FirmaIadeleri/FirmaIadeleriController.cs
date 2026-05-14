using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Create;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Detail;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.IadeIslemleri.FirmaIadeleri;

[ApiController]
[Route("api/iade-islemleri/firma-iadeleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class FirmaIadeleriController(
    IListCompanyReturnsUseCase listCompanyReturnsUseCase,
    IGetCompanyReturnDetailUseCase getCompanyReturnDetailUseCase,
    ICreateCompanyReturnUseCase createCompanyReturnUseCase,
    IEDespatchService eDespatchService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "iade-islemleri";
    private const string ModuleName = "IadeIslemleri";
    private const string MenuCode = "firma-iadeleri";
    private const string MenuName = "FirmaIadeleri";
    private const string ListPolicy = "iade-islemleri.firma-iadeleri.list";
    private const string DetailPolicy = "iade-islemleri.firma-iadeleri.detail";
    private const string CreatePolicy = "iade-islemleri.firma-iadeleri.create";
    private const string UpdatePolicy = "iade-islemleri.firma-iadeleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyMovementListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyMovementListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listCompanyReturnsUseCase.ExecuteAsync(
            new CompanyMovementListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CompanyMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyMovementDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getCompanyReturnDetailUseCase.ExecuteAsync(
            new CompanyMovementDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateCompanyMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateCompanyMovementResponse>> Create(
        [FromBody] CreateCompanyMovementHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await createCompanyReturnUseCase.ExecuteAsync(
            new CreateCompanyMovementRequest(
                warehouseNo,
                request.CustomerCode,
                request.MovementDate,
                request.DocumentDate,
                request.DocumentNo,
                request.Description,
                request.Lines
                    .Select(line => new CreateCompanyMovementLineRequest(
                        line.StockCode,
                        line.Quantity,
                        line.UnitPrice,
                        line.UnitPointer,
                        line.Description,
                        line.PartyCode,
                        line.LotNo,
                        line.ProjectCode,
                        line.CustomerResponsibilityCenter,
                        line.ProductResponsibilityCenter))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{documentSerie}/{documentOrderNo:int}/e-irsaliye")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(SendEDespatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SendEDespatchResponse>> SendEDespatch(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        [FromBody, Required] SendEDespatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await eDespatchService.SendAsync(
            new SendEDespatchRequest(
                EDespatchDocumentType.CompanyReturn,
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo,
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}/e-irsaliye/pdf")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetEDespatchPdf(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await eDespatchService.GetPdfAsync(
            new GetEDespatchPdfRequest(
                EDespatchDocumentType.CompanyReturn,
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        Response.Headers.ContentDisposition = $"inline; filename=\"{response.FileName}\"";

        return File(response.Content, "application/pdf");
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}
