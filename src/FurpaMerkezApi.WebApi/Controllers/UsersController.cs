using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.UsersManage)]
[Route("api/users")]
[Route("api/kullanici-islemleri/kullanicilar")]
public sealed class UsersController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await userManagementService.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await userManagementService.GetByIdAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Update(
        Guid id,
        [FromBody] UpdateUserBody request,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateAsync(
            id,
            new UpdateUserRequest(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.WarehouseNo,
                request.WarehouseName,
                request.IsActive),
            cancellationToken));

    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> AssignRoles(
        Guid id,
        [FromBody] AssignRolesBody request,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.AssignRolesAsync(
            id,
            new AssignUserRolesRequest(request.RoleIds),
            cancellationToken));

    public sealed class UpdateUserBody
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(50)]
        public required string Username { get; init; }

        [Required(AllowEmptyStrings = false)]
        [EmailAddress]
        [StringLength(200)]
        public required string Email { get; init; }

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

        public required bool IsActive { get; init; }
    }

    public sealed class AssignRolesBody
    {
        [MinLength(1)]
        public required IReadOnlyCollection<Guid> RoleIds { get; init; }
    }
}
