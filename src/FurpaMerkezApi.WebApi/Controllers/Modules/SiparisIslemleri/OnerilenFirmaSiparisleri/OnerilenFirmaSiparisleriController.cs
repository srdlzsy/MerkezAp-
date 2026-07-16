using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/onerilen-firma-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class OnerilenFirmaSiparisleriController(
    IListSuggestedCompanyOrdersUseCase listSuggestedCompanyOrdersUseCase,
    IDocumentFlowService documentFlowService,
    ICreateIssuedCompanyOrderUseCase createIssuedCompanyOrderUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "siparis-islemleri";
    private const string ModuleName = "SiparisIslemleri";
    private const string MenuCode = "onerilen-firma-siparisleri";
    private const string MenuName = "OnerilenFirmaSiparisleri";
    private const string ListPolicy = "siparis-islemleri.onerilen-firma-siparisleri.list";
    private const string CreatePolicy = "siparis-islemleri.onerilen-firma-siparisleri.create";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<SuggestedCompanyOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<SuggestedCompanyOrderListItemDto>>> List(
        [FromQuery] SuggestedCompanyOrderListHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

        return Ok(await listSuggestedCompanyOrdersUseCase.ExecuteAsync(
            new SuggestedCompanyOrderListRequest(
                warehouseNo,
                request.SupplierCode,
                request.LookbackDays,
                request.FallbackRecommendedDay),
            cancellationToken));
    }

    [HttpPost("convert-to-order")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateIssuedCompanyOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateIssuedCompanyOrderResponse>> ConvertToOrder(
        [FromBody] ConvertSuggestedCompanyOrderHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNo(request.WarehouseNo);
        var response = await createIssuedCompanyOrderUseCase.ExecuteAsync(
            new CreateIssuedCompanyOrderRequest(
                warehouseNo,
                request.SupplierCode,
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
                "Oneriden firma siparisi olusturuldu.",
                ChangedByUserId: User.GetRequiredUserId()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }
}

public sealed class SuggestedCompanyOrderListHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [StringLength(25)]
    public string SupplierCode { get; init; } = string.Empty;

    [Range(1, 365)]
    public int LookbackDays { get; init; } = 43;

    [Range(1, 365)]
    public int FallbackRecommendedDay { get; init; } = 7;
}

public sealed class ConvertSuggestedCompanyOrderHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [StringLength(25)]
    public string SupplierCode { get; init; } = string.Empty;

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
    public IReadOnlyCollection<ConvertSuggestedCompanyOrderLineHttpRequest> Lines { get; init; } =
        Array.Empty<ConvertSuggestedCompanyOrderLineHttpRequest>();
}

public sealed class ConvertSuggestedCompanyOrderLineHttpRequest
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
