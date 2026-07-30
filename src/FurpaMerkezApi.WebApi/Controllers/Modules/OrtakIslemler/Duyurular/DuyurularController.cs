using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.OrtakIslemler.Duyurular;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OrtakIslemler.Duyurular;

[ApiController]
[Authorize]
[Route("api/home/duyurular")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class HomeDuyurularController(IDuyurularService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<AnnouncementDto>>> GetInbox(
        [FromQuery] AnnouncementInboxHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetInboxAsync(
            new AnnouncementInboxRequest(
                User.GetRequiredUserId(),
                User.GetRequiredWarehouseNo(),
                request.IncludeRead,
                request.Take),
            cancellationToken));

    [HttpGet("ozet")]
    [ProducesResponseType(typeof(AnnouncementSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnnouncementSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await service.GetSummaryAsync(
            User.GetRequiredUserId(),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPatch("{id:guid}/okundu")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.MarkAsReadAsync(
            id,
            User.GetRequiredUserId(),
            User.GetRequiredWarehouseNo(),
            cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/ortak-islemler/duyurular")]
[Route("api/yonetim/duyurular")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class DuyurularController(IDuyurularService service) : ControllerBase
{
    private const string ModuleCode = "ortak-islemler";
    private const string MenuCode = "duyurular";

    private const string ListPolicy = ModuleCode + "." + MenuCode + ".list";
    private const string DetailPolicy = ModuleCode + "." + MenuCode + ".detail";
    private const string CreatePolicy = ModuleCode + "." + MenuCode + ".create";
    private const string UpdatePolicy = ModuleCode + "." + MenuCode + ".update";
    private const string ArchivePolicy = ModuleCode + "." + MenuCode + ".archive";
    private const string AllWarehousesPolicy = ModuleCode + "." + MenuCode + ".all-warehouses";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<AnnouncementDto>>> List(
        [FromQuery] AnnouncementManagementListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListForManagementAsync(
            new AnnouncementManagementListRequest(
                request.Status,
                request.TargetType,
                request.TargetWarehouseNo,
                request.TargetUserId,
                request.StartDate,
                request.EndDate,
                request.IncludeArchived,
                request.Take,
                CreateActorContext()),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetForManagementAsync(id, CreateActorContext(), cancellationToken));

    [HttpGet("{id:guid}/okuyanlar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AnnouncementReadReceiptListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementReadReceiptListDto>> GetReadReceipts(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetReadReceiptsAsync(id, CreateActorContext(), cancellationToken));

    [HttpGet("hedef-kullanicilar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AnnouncementTargetUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<AnnouncementTargetUserDto>>> SearchTargetUsers(
        [FromQuery] AnnouncementTargetUserSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchTargetUsersAsync(
            new AnnouncementTargetUserSearchRequest(
                request.Search,
                request.WarehouseNo,
                request.Take,
                CreateActorContext()),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnnouncementDto>> Create(
        [FromBody] SaveAnnouncementHttpRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await service.CreateAsync(
            new CreateAnnouncementRequest(
                request.Title,
                request.Message,
                request.Priority,
                request.TargetType,
                request.TargetWarehouseNos,
                request.TargetUserIds,
                request.StartsAtUtc,
                request.ExpiresAtUtc,
                CreateActorContext()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Update(
        Guid id,
        [FromBody] SaveAnnouncementHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(
            id,
            new UpdateAnnouncementRequest(
                request.Title,
                request.Message,
                request.Priority,
                request.TargetType,
                request.TargetWarehouseNos,
                request.TargetUserIds,
                request.StartsAtUtc,
                request.ExpiresAtUtc,
                CreateActorContext()),
            cancellationToken));

    [HttpPatch("{id:guid}/arsivle")]
    [Authorize(Policy = ArchivePolicy)]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Archive(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.ArchiveAsync(id, CreateActorContext(), cancellationToken));

    private AnnouncementActorContext CreateActorContext() =>
        new(
            User.GetRequiredUserId(),
            GetRequiredClaim(ClaimTypes.Name),
            ResolveFullName(),
            User.GetRequiredWarehouseNo(),
            GetRequiredClaim("warehouse_name"),
            User.HasPermission(AllWarehousesPolicy));

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

public sealed class AnnouncementInboxHttpRequest
{
    public bool IncludeRead { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class AnnouncementManagementListHttpRequest
{
    [StringLength(30)]
    public string? Status { get; init; }

    [StringLength(30)]
    public string? TargetType { get; init; }

    [Range(1, int.MaxValue)]
    public int? TargetWarehouseNo { get; init; }

    public Guid? TargetUserId { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public bool IncludeArchived { get; init; }

    [Range(1, 500)]
    public int? Take { get; init; }
}

public sealed class AnnouncementTargetUserSearchHttpRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Range(1, 100)]
    public int? Take { get; init; }
}

public sealed class SaveAnnouncementHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(140)]
    public required string Title { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(4000)]
    public required string Message { get; init; }

    [StringLength(30)]
    public string? Priority { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(30)]
    public required string TargetType { get; init; }

    public IReadOnlyCollection<int>? TargetWarehouseNos { get; init; }

    public IReadOnlyCollection<Guid>? TargetUserIds { get; init; }

    public DateTime? StartsAtUtc { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }
}
