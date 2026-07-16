using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.OnerilenDepoSiparisleri;

[ApiController]
[Route("api/siparis-islemleri/onerilen-depo-siparisleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class OnerilenDepoSiparisleriController(
    IListSuggestedWarehouseOrdersUseCase listSuggestedWarehouseOrdersUseCase,
    IDocumentFlowService documentFlowService,
    ICreateIssuedWarehouseOrderUseCase createIssuedWarehouseOrderUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "siparis-islemleri";
    private const string ModuleName = "SiparisIslemleri";
    private const string MenuCode = "onerilen-depo-siparisleri";
    private const string MenuName = "OnerilenDepoSiparisleri";
    private const string ListPolicy = "siparis-islemleri.onerilen-depo-siparisleri.list";
    private const string CreatePolicy = "siparis-islemleri.onerilen-depo-siparisleri.create";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<SuggestedWarehouseOrderListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<SuggestedWarehouseOrderListItemDto>>> List(
        [FromQuery] SuggestedWarehouseOrderListHttpRequest request,
        CancellationToken cancellationToken)
    {
        var targetWarehouseNo = User.ResolveWarehouseNo(request.TargetWarehouseNo);

        return Ok(await listSuggestedWarehouseOrdersUseCase.ExecuteAsync(
            new SuggestedWarehouseOrderListRequest(
                targetWarehouseNo,
                request.SourceWarehouseNo,
                request.LookbackDays,
                request.FallbackRecommendedDay),
            cancellationToken));
    }

    [HttpPost("convert-to-order")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateIssuedWarehouseOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateIssuedWarehouseOrderResponse>> ConvertToOrder(
        [FromBody] ConvertSuggestedWarehouseOrderHttpRequest request,
        CancellationToken cancellationToken)
    {
        var targetWarehouseNo = User.ResolveWarehouseNo(request.TargetWarehouseNo);
        var response = await createIssuedWarehouseOrderUseCase.ExecuteAsync(
            new CreateIssuedWarehouseOrderRequest(
                targetWarehouseNo,
                request.SourceWarehouseNo,
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
                "Oneriden depo siparisi olusturuldu.",
                ChangedByUserId: User.GetRequiredUserId()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }
}

public sealed class SuggestedWarehouseOrderListHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? TargetWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int SourceWarehouseNo { get; init; }

    [Range(1, 365)]
    public int LookbackDays { get; init; } = 43;

    [Range(1, 365)]
    public int FallbackRecommendedDay { get; init; } = 7;
}

public sealed class ConvertSuggestedWarehouseOrderHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? TargetWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int SourceWarehouseNo { get; init; }

    public DateTime? OrderDate { get; init; }

    public DateTime? DeliveryDate { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<ConvertSuggestedWarehouseOrderLineHttpRequest> Lines { get; init; } =
        Array.Empty<ConvertSuggestedWarehouseOrderLineHttpRequest>();
}

public sealed class ConvertSuggestedWarehouseOrderLineHttpRequest
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
