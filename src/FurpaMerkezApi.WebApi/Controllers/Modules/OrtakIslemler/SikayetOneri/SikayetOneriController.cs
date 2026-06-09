using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.OrtakIslemler.SikayetOneri;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OrtakIslemler.SikayetOneri;

[ApiController]
[Authorize]
[Route("api/home/sikayet-oneri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class HomeSikayetOneriController(ISikayetOneriService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(FeedbackItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackItemDto>> Create(
        [FromBody] CreateFeedbackItemHttpRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await service.CreateAsync(
            new CreateFeedbackItemRequest(
                request.Type,
                request.Title,
                request.Message,
                request.Priority,
                User.GetRequiredUserId(),
                GetRequiredClaim(ClaimTypes.Name),
                ResolveFullName(),
                User.GetRequiredWarehouseNo(),
                GetRequiredClaim("warehouse_name")),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpGet("benim")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<FeedbackItemDto>>> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await service.GetMyItemsAsync(User.GetRequiredUserId(), cancellationToken));

    [HttpGet("ozet")]
    [ProducesResponseType(typeof(FeedbackSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeedbackSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await service.GetMySummaryAsync(User.GetRequiredUserId(), cancellationToken));

    private string ResolveFullName()
    {
        var firstName = User.FindFirstValue("first_name");
        var lastName = User.FindFirstValue("last_name");
        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(fullName) ? GetRequiredClaim(ClaimTypes.Name) : fullName;
    }

    private string GetRequiredClaim(string claimType) =>
        User.FindFirstValue(claimType)
        ?? throw new UnauthorizedAccessException($"Required claim was not found: {claimType}");
}

[ApiController]
[Authorize]
[Route("api/yonetim/sikayet-oneri")]
[Route("api/ortak-islemler/sikayet-oneri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class YonetimSikayetOneriController(ISikayetOneriService service) : ControllerBase
{
    private const string ListPolicy = "ortak-islemler.sikayet-oneri.list";
    private const string DetailPolicy = "ortak-islemler.sikayet-oneri.detail";
    private const string UpdatePolicy = "ortak-islemler.sikayet-oneri.update";
    private const string ListAllPolicy = "ortak-islemler.sikayet-oneri.list-all";
    private const string AdministratorRoleName = "Administrator";

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<FeedbackItemDto>>> List(
        [FromQuery] FeedbackManagementListHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanUseManagementAction(ListPolicy))
        {
            return Forbid();
        }

        return Ok(await service.ListForManagementAsync(
            new FeedbackManagementListRequest(
                request.Status,
                request.Type,
                request.WarehouseNo,
                request.StartDate,
                request.EndDate,
                request.Take,
                CanViewAllFeedback(),
                User.GetRequiredWarehouseNo()),
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FeedbackItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackItemDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!CanUseManagementAction(DetailPolicy))
        {
            return Forbid();
        }

        return Ok(await service.GetForManagementAsync(
            id,
            new FeedbackManagementScope(CanViewAllFeedback(), User.GetRequiredWarehouseNo()),
            cancellationToken));
    }

    [HttpPatch("{id:guid}/okundu")]
    [ProducesResponseType(typeof(FeedbackItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackItemDto>> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!CanUseManagementAction(UpdatePolicy))
        {
            return Forbid();
        }

        return Ok(await service.MarkAsReadAsync(
            id,
            new FeedbackManagementActionContext(User.GetRequiredUserId(), CanViewAllFeedback(), User.GetRequiredWarehouseNo()),
            cancellationToken));
    }

    [HttpPatch("{id:guid}/durum")]
    [ProducesResponseType(typeof(FeedbackItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackItemDto>> ChangeStatus(
        Guid id,
        [FromBody] ChangeFeedbackStatusHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanUseManagementAction(UpdatePolicy))
        {
            return Forbid();
        }

        return Ok(await service.ChangeStatusAsync(
            id,
            new ChangeFeedbackStatusRequest(request.Status, request.AdminNote),
            new FeedbackManagementActionContext(User.GetRequiredUserId(), CanViewAllFeedback(), User.GetRequiredWarehouseNo()),
            cancellationToken));
    }

    private bool CanViewAllFeedback() =>
        User.IsInRole(AdministratorRoleName) ||
        User.HasClaim("permission", ListAllPolicy);

    private bool CanUseManagementAction(string permissionCode) =>
        User.IsInRole(AdministratorRoleName) ||
        User.HasClaim("permission", permissionCode) ||
        (permissionCode == ListPolicy && User.HasClaim("permission", ListAllPolicy));
}

public sealed class CreateFeedbackItemHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(30)]
    public required string Type { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(120)]
    public required string Title { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(2000)]
    public required string Message { get; init; }

    [StringLength(30)]
    public string? Priority { get; init; }
}

public sealed class FeedbackManagementListHttpRequest
{
    [StringLength(30)]
    public string? Status { get; init; }

    [StringLength(30)]
    public string? Type { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class ChangeFeedbackStatusHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(30)]
    public required string Status { get; init; }

    [StringLength(1000)]
    public string? AdminNote { get; init; }
}
