using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.VerilenFirmaSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/verilen-firma-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class VerilenFirmaSiparisleriController(
    IListIssuedCompanyOrdersUseCase listIssuedCompanyOrdersUseCase,
    IGetIssuedCompanyOrderDetailUseCase getIssuedCompanyOrderDetailUseCase,
    IDocumentFlowService documentFlowService,
    ICreateIssuedCompanyOrderUseCase createIssuedCompanyOrderUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "siparis-islemleri";
    private const string ModuleName = "SiparisIslemleri";
    private const string MenuCode = "verilen-firma-siparisleri";
    private const string MenuName = "VerilenFirmaSiparisleri";
    private const string ListPolicy = "siparis-islemleri.verilen-firma-siparisleri.list";
    private const string DetailPolicy = "siparis-islemleri.verilen-firma-siparisleri.detail";
    private const string CreatePolicy = "siparis-islemleri.verilen-firma-siparisleri.create";
    private const string UpdatePolicy = "siparis-islemleri.verilen-firma-siparisleri.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompanyOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyOrderListItemDto>>> List(
        [FromQuery] IssuedCompanyOrderListHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listIssuedCompanyOrdersUseCase.ExecuteAsync(
            new CompanyOrderListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value,
                request.CustomerCode,
                request.OnlyOpen),
            cancellationToken));
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CompanyOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyOrderDetailDto>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await getIssuedCompanyOrderDetailUseCase.ExecuteAsync(
            new CompanyOrderDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpGet("key/{documentKey}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CompanyOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyOrderDetailDto>> DetailByKey(
        string documentKey,
        CancellationToken cancellationToken) =>
        Ok(await getIssuedCompanyOrderDetailUseCase.ExecuteAsync(
            CompanyOrderDocumentKey.Parse(documentKey),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateIssuedCompanyOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateIssuedCompanyOrderResponse>> Create(
        [FromBody] CreateIssuedCompanyOrderHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await createIssuedCompanyOrderUseCase.ExecuteAsync(
            new CreateIssuedCompanyOrderRequest(
                warehouseNo,
                request.CustomerCode,
                request.OrderDate,
                request.DeliveryDate!.Value,
                request.Description1,
                request.Description2,
                request.Deliverer,
                request.Receiver,
                request.Lines
                    .Select(line => new CreateIssuedCompanyOrderLineRequest(
                        line.StockCode,
                        line.Quantity,
                        line.RecommendedQuantity,
                        line.UnitPrice,
                        line.UnitPointer,
                        line.Description1,
                        line.Description2,
                        line.PackageCode,
                        line.ProjectCode,
                        line.CustomerResponsibilityCenter,
                        line.ProductResponsibilityCenter))
                    .ToArray()),
            cancellationToken);

        await documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    DocumentFlowType.IssuedCompanyOrder,
                    response.WarehouseNo,
                    response.DocumentSerie,
                    response.DocumentOrderNo),
                DocumentFlowType.IssuedCompanyOrder,
                response.WarehouseNo,
                null,
                response.DocumentSerie,
                response.DocumentOrderNo,
                DocumentFlowStep.OrderCreated,
                DocumentFlowStatus.Succeeded,
                "Verilen firma siparisi olusturuldu.",
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

public sealed class IssuedCompanyOrderListHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(25)]
    public string? CustomerCode { get; init; }

    public bool OnlyOpen { get; init; }
}

public sealed class CreateIssuedCompanyOrderHttpRequest
{
    [Required]
    [StringLength(25)]
    public string CustomerCode { get; init; } = string.Empty;

    public DateTime? OrderDate { get; init; }

    [Required]
    public DateTime? DeliveryDate { get; init; }

    [StringLength(50)]
    public string? Description1 { get; init; }

    [StringLength(50)]
    public string? Description2 { get; init; }

    [StringLength(25)]
    public string? Deliverer { get; init; }

    [StringLength(25)]
    public string? Receiver { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateIssuedCompanyOrderLineHttpRequest> Lines { get; init; } =
        Array.Empty<CreateIssuedCompanyOrderLineHttpRequest>();
}

public sealed class CreateIssuedCompanyOrderLineHttpRequest
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
    public string? Description1 { get; init; }

    [StringLength(50)]
    public string? Description2 { get; init; }

    [StringLength(25)]
    public string? PackageCode { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProductResponsibilityCenter { get; init; }
}
