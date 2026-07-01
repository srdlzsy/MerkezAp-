using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.VerilenDepoSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/verilen-depo-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class VerilenDepoSiparisleriController(
    IListIssuedWarehouseOrdersUseCase listIssuedWarehouseOrdersUseCase,
    IGetIssuedWarehouseOrderDetailUseCase getIssuedWarehouseOrderDetailUseCase,
    IDocumentFlowService documentFlowService,
    ICreateIssuedWarehouseOrderUseCase createIssuedWarehouseOrderUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "siparis-islemleri";
    private const string ModuleName = "SiparisIslemleri";
    private const string MenuCode = "verilen-depo-siparisleri";
    private const string MenuName = "VerilenDepoSiparisleri";
    private const string ListPolicy = "siparis-islemleri.verilen-depo-siparisleri.list";
    private const string DetailPolicy = "siparis-islemleri.verilen-depo-siparisleri.detail";
    private const string CreatePolicy = "siparis-islemleri.verilen-depo-siparisleri.create";
    private const string UpdatePolicy = "siparis-islemleri.verilen-depo-siparisleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseOrderListItemDto>>> List(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listIssuedWarehouseOrdersUseCase.ExecuteAsync(
            new WarehouseOrderListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseOrderDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getIssuedWarehouseOrderDetailUseCase.ExecuteAsync(
            new WarehouseOrderDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpGet("key/{documentKey}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseOrderDetailDto>> DetailByKey(
        string documentKey,
        CancellationToken cancellationToken) =>
        Ok(await getIssuedWarehouseOrderDetailUseCase.ExecuteAsync(
            WarehouseOrderDocumentKey.Parse(documentKey),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateIssuedWarehouseOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateIssuedWarehouseOrderResponse>> Create(
        [FromBody] CreateIssuedWarehouseOrderHttpRequest request,
        CancellationToken cancellationToken)
    {
        var inWarehouseNo = User.GetRequiredWarehouseNo();
        var response = await createIssuedWarehouseOrderUseCase.ExecuteAsync(
            new CreateIssuedWarehouseOrderRequest(
                inWarehouseNo,
                request.OutWarehouseNo,
                request.OrderDate,
                request.DeliveryDate,
                request.Description,
                request.Lines
                    .Select(line => new CreateIssuedWarehouseOrderLineRequest(
                        line.StockCode,
                        line.Quantity,
                        line.RecommendedQuantity,
                        line.UnitPrice,
                        line.UnitPointer,
                        line.Description,
                        line.PackageCode,
                        line.ProjectCode,
                        line.ResponsibilityCenter))
                    .ToArray()),
            cancellationToken);

        await documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    DocumentFlowType.IssuedWarehouseOrder,
                    response.InWarehouseNo,
                    response.DocumentSerie,
                    response.DocumentOrderNo),
                DocumentFlowType.IssuedWarehouseOrder,
                response.InWarehouseNo,
                response.OutWarehouseNo,
                response.DocumentSerie,
                response.DocumentOrderNo,
                DocumentFlowStep.OrderCreated,
                DocumentFlowStatus.Succeeded,
                "Verilen depo siparisi olusturuldu.",
                ChangedByUserId: User.GetRequiredUserId()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);
}

public sealed class CreateIssuedWarehouseOrderHttpRequest
{
    [Range(1, int.MaxValue)]
    public int OutWarehouseNo { get; init; }

    public DateTime? OrderDate { get; init; }

    public DateTime? DeliveryDate { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateIssuedWarehouseOrderLineHttpRequest> Lines { get; init; } =
        Array.Empty<CreateIssuedWarehouseOrderLineHttpRequest>();
}

public sealed class CreateIssuedWarehouseOrderLineHttpRequest
{
    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(0.000001, double.MaxValue)]
    public double Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? RecommendedQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public double UnitPrice { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PackageCode { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? ResponsibilityCenter { get; init; }
}
