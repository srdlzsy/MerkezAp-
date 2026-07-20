using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.WebApi.Configuration;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IOptions<ApiAuthOptions> authOptions) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<AuthResponse> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (!authOptions.Value.AllowSelfRegistration)
        {
            throw new ForbiddenAccessException("Self registration is disabled.");
        }

        return authService.RegisterAsync(
            new RegisterRequest(
                request.Username,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.WarehouseNo,
                request.WarehouseName),
            cancellationToken);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> Login([FromBody] LoginUserRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return authService.LoginAsync(new LoginRequest(request.UsernameOrEmail, request.Password, ip), cancellationToken);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        return Ok(await authService.GetUserByIdAsync(userId, cancellationToken));
    }

    public sealed class RegisterUserRequest
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(50)]
        public required string Username { get; init; }

        [Required(AllowEmptyStrings = false)]
        [EmailAddress]
        [StringLength(200)]
        public required string Email { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(200, MinimumLength = 6)]
        public required string Password { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(100)]
        public required string FirstName { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(100)]
        public required string LastName { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(50)]
        public required string WarehouseNo { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(150)]
        public required string WarehouseName { get; init; }
    }

    public sealed class LoginUserRequest
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(200)]
        public required string UsernameOrEmail { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(200)]
        public required string Password { get; init; }
    }
}
