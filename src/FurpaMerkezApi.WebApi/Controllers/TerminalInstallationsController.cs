using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.TerminalCihazlari;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/terminal-installations")]
public sealed class TerminalInstallationsController(ITerminalInstallationService service) : ControllerBase
{
    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(TerminalInstallationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TerminalInstallationDto>> Heartbeat(
        [FromBody] TerminalHeartbeatHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(User.FindFirstValue("client_type"), "terminal", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var deviceId = User.FindFirstValue("device_id")?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Device id is required",
                Detail = "The terminal session does not contain a device_id claim. Sign in again with a current terminal version."
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        return Ok(await service.RecordHeartbeatAsync(
            request.ToApplicationRequest(),
            new TerminalHeartbeatContext(
                deviceId,
                User.GetRequiredUserId(),
                User.GetRequiredWarehouseNo(),
                ipAddress),
            cancellationToken));
    }
}

public sealed class TerminalHeartbeatHttpRequest
{
    [Required(AllowEmptyStrings = false), StringLength(40)]
    public string AppVersion { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int BuildNumber { get; init; }

    [StringLength(100)]
    public string? Manufacturer { get; init; }

    [StringLength(150)]
    public string? DeviceModel { get; init; }

    [StringLength(40)]
    public string? AndroidVersion { get; init; }

    [Range(1, 1000)]
    public int? AndroidSdk { get; init; }

    [MaxLength(20)]
    public IReadOnlyCollection<string>? SupportedAbis { get; init; }

    public TerminalHeartbeatRequest ToApplicationRequest() =>
        new(AppVersion, BuildNumber, Manufacturer, DeviceModel, AndroidVersion, AndroidSdk, SupportedAbis);
}
