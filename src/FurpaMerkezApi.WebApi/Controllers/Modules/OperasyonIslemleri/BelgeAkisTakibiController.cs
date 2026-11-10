using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;

[ApiController]
[Route("api/operasyon-islemleri/belge-akis-takibi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class BelgeAkisTakibiController(IDocumentFlowService documentFlowService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "belge-akis-takibi";
    private const string MenuName = "BelgeAkisTakibi";
    private const string ListPolicy = "operasyon-islemleri.belge-akis-takibi.list";
    private const string DetailPolicy = "operasyon-islemleri.belge-akis-takibi.detail";
    private const string AllWarehousesPolicy = "operasyon-islemleri.belge-akis-takibi.all-warehouses";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(DocumentFlowListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentFlowListResponse>> List(
        [FromQuery] DocumentFlowListHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate.HasValue &&
            request.EndDate.HasValue &&
            request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            throw new ArgumentException("Start date can not be later than end date.");
        }

        var warehouseNo = User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy);

        return Ok(await documentFlowService.ListAsync(
            new DocumentFlowListRequest(
                warehouseNo,
                request.StartDate,
                request.EndDate,
                request.DocumentType,
                request.Status,
                request.Search,
                request.Skip,
                request.Take,
                ParseDirection(request.Direction),
                request.CurrentStep,
                ParseCategory(request.Category),
                ParsePreset(request.Preset)),
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(DocumentFlowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentFlowDetailDto>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        int? allowedWarehouseNo = User.HasPermission(AllWarehousesPolicy)
            ? null
            : User.GetRequiredWarehouseNo();

        return Ok(await documentFlowService.GetAsync(id, allowedWarehouseNo, cancellationToken));
    }

    private static DocumentFlowDirection ParseDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DocumentFlowDirection.All;
        }

        if (Enum.TryParse<DocumentFlowDirection>(value.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "incoming" or "target" or "in" or "gelen" or "hedef" => DocumentFlowDirection.Incoming,
            "outgoing" or "source" or "out" or "cikan" or "giden" or "kaynak" => DocumentFlowDirection.Outgoing,
            _ => DocumentFlowDirection.All
        };
    }

    private static DocumentFlowCategory ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DocumentFlowCategory.All;
        }

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<DocumentFlowCategory>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "shipments" or "shipments-and-receiving" or "sevk" => DocumentFlowCategory.ShipmentsAndReceiving,
            "returns" or "iade" => DocumentFlowCategory.Returns,
            "orders" or "siparis" => DocumentFlowCategory.Orders,
            "cash" or "cash-and-store" or "kasa" => DocumentFlowCategory.CashAndStore,
            "stock" or "stock-and-counting" or "stok" => DocumentFlowCategory.StockAndCounting,
            _ => DocumentFlowCategory.All
        };
    }

    private static DocumentFlowPreset ParsePreset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DocumentFlowPreset.All;
        }

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<DocumentFlowPreset>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "action-required" or "incomplete" or "eksik" => DocumentFlowPreset.ActionRequired,
            "pending-receiving" or "bekleyen-kabul" => DocumentFlowPreset.PendingReceiving,
            "unaccepted-outgoing" or "kabul-bekleyen-cikis" => DocumentFlowPreset.UnacceptedOutgoing,
            "pending-edespatch" or "bekleyen-eirsaliye" => DocumentFlowPreset.PendingEDespatch,
            "failed-edespatch" or "hatali-eirsaliye" => DocumentFlowPreset.FailedEDespatch,
            "open-orders" or "acik-siparis" => DocumentFlowPreset.OpenOrders,
            "failed-only" or "hatali" => DocumentFlowPreset.FailedOnly,
            "today-shipments" or "bugun-sevk" => DocumentFlowPreset.TodayShipments,
            "today-returns" or "bugun-iade" => DocumentFlowPreset.TodayReturns,
            "today-receivings" or "bugun-kabul" => DocumentFlowPreset.TodayReceivings,
            _ => DocumentFlowPreset.All
        };
    }
}

public sealed class DocumentFlowListHttpRequest
{
    [Range(0, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public DocumentFlowType? DocumentType { get; init; }

    public DocumentFlowStatus? Status { get; init; }

    public DocumentFlowStep? CurrentStep { get; init; }

    [StringLength(50)]
    public string? Direction { get; init; }

    [StringLength(50)]
    public string? Category { get; init; }

    [StringLength(50)]
    public string? Preset { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [Range(0, int.MaxValue)]
    public int Skip { get; init; }

    [Range(1, 500)]
    public int Take { get; init; } = 100;
}
