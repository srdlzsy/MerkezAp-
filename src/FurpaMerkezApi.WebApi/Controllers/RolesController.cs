using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.RolesManage)]
[Route("api/roles")]
[Route("api/kullanici-islemleri/roller")]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RoleDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await roleService.GetAllAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> Create([FromBody] SaveRoleBody request, CancellationToken cancellationToken) =>
        Ok(await roleService.CreateAsync(new SaveRoleRequest(request.Name, request.Description, request.IsActive), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> Update(
        Guid id,
        [FromBody] SaveRoleBody request,
        CancellationToken cancellationToken) =>
        Ok(await roleService.UpdateAsync(id, new SaveRoleRequest(request.Name, request.Description, request.IsActive), cancellationToken));

    [HttpPost("{id:guid}/permissions")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> AssignPermissions(
        Guid id,
        [FromBody] AssignPermissionsBody request,
        CancellationToken cancellationToken) =>
        Ok(await roleService.AssignPermissionsAsync(id, new AssignRolePermissionsRequest(request.PermissionIds), cancellationToken));

    public sealed class SaveRoleBody
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(100)]
        public required string Name { get; init; }

        [StringLength(250)]
        public string? Description { get; init; }

        public required bool IsActive { get; init; }
    }

    public sealed class AssignPermissionsBody
    {
        [MinLength(1)]
        public required IReadOnlyCollection<Guid> PermissionIds { get; init; }
    }
}
