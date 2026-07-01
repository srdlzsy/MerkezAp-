using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
    private const string AdministratorRoleName = "Administrator";
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "belge-akis-takibi";
    private const string MenuName = "BelgeAkisTakibi";

    [HttpGet]
    [Authorize]
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

        var warehouseNo = CanViewAllWarehouses(User)
            ? request.WarehouseNo
            : User.GetRequiredWarehouseNo();

        return Ok(await documentFlowService.ListAsync(
            new DocumentFlowListRequest(
                warehouseNo,
                request.StartDate,
                request.EndDate,
                request.DocumentType,
                request.Status,
                request.Search,
                request.Take),
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(DocumentFlowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentFlowDetailDto>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        int? allowedWarehouseNo = CanViewAllWarehouses(User)
            ? null
            : User.GetRequiredWarehouseNo();

        return Ok(await documentFlowService.GetAsync(id, allowedWarehouseNo, cancellationToken));
    }

    private static bool CanViewAllWarehouses(ClaimsPrincipal user) =>
        user.IsInRole(AdministratorRoleName) || user.IsInRole("Admin");
}

public sealed class DocumentFlowListHttpRequest
{
    [Range(0, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public DocumentFlowType? DocumentType { get; init; }

    public DocumentFlowStatus? Status { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, 500)]
    public int Take { get; init; } = 100;
}
