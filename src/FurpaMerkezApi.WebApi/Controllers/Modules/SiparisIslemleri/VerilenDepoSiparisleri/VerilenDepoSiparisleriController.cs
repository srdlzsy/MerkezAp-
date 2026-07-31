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
        var warehouseNo = User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy);

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
        var resolvedWarehouseNo = User.ResolveWarehouseNoForPolicy(warehouseNo, DetailPolicy);

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
        var inWarehouseNo = User.ResolveWarehouseNoForPolicy(request.InWarehouseNo, CreatePolicy);
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
                        line.ResponsibilityCenter,
                        ToApplicationRequest(line.GreenGrocerCase)))
                    .ToArray(),
                User.GetRequiredUserId()),
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

    private static GreenGrocerOrderLineSnapshotRequest? ToApplicationRequest(
        GreenGrocerOrderLineSnapshotHttpRequest? request) =>
        request is null
            ? null
            : new GreenGrocerOrderLineSnapshotRequest(
                request.InputQuantity,
                request.InputMode,
                request.ConversionMode,
                request.MicroUnit,
                request.EstimatedQuantity,
                request.AverageKgPerCase,
                request.UnitsPerCase,
                request.AverageSource,
                request.AverageRecordCount,
                request.AverageCaseCount,
                request.CoefficientOfVariation,
                request.Confidence);
}

public sealed class CreateIssuedWarehouseOrderHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? InWarehouseNo { get; init; }

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

    public GreenGrocerOrderLineSnapshotHttpRequest? GreenGrocerCase { get; init; }
}

public sealed class GreenGrocerOrderLineSnapshotHttpRequest
{
    [Range(0.000001, double.MaxValue)]
    public double InputQuantity { get; init; }

    [Required]
    [StringLength(40)]
    public string InputMode { get; init; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string ConversionMode { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string MicroUnit { get; init; } = string.Empty;

    [Range(0.000001, double.MaxValue)]
    public double EstimatedQuantity { get; init; }

    [Range(0.000001, double.MaxValue)]
    public double? AverageKgPerCase { get; init; }

    [Range(0.000001, double.MaxValue)]
    public double? UnitsPerCase { get; init; }

    [Required]
    [StringLength(60)]
    public string AverageSource { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int? AverageRecordCount { get; init; }

    [Range(0, int.MaxValue)]
    public int? AverageCaseCount { get; init; }

    [Range(0d, double.MaxValue)]
    public double? CoefficientOfVariation { get; init; }

    [Required]
    [StringLength(30)]
    public string Confidence { get; init; } = string.Empty;
}
