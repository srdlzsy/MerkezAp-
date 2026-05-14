using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.PermissionsManage)]
[Route("api/permissions")]
[Route("api/kullanici-islemleri/yetkiler")]
public sealed class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    [HttpGet("catalog")]
    [ProducesResponseType(typeof(IReadOnlyCollection<PermissionModuleDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<PermissionModuleDto>> GetCatalog() =>
        Ok(PermissionTreeBuilder.BuildFromDefinitions(PermissionCatalog.Definitions));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PermissionDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await permissionService.GetAllAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PermissionDto>> Create(
        [FromBody] SavePermissionBody request,
        CancellationToken cancellationToken) =>
        Ok(await permissionService.CreateAsync(new SavePermissionRequest(request.Code, request.Name, request.Description), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PermissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PermissionDto>> Update(
        Guid id,
        [FromBody] SavePermissionBody request,
        CancellationToken cancellationToken) =>
        Ok(await permissionService.UpdateAsync(id, new SavePermissionRequest(request.Code, request.Name, request.Description), cancellationToken));

    public sealed class SavePermissionBody
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(100)]
        public required string Code { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(100)]
        public required string Name { get; init; }

        [StringLength(250)]
        public string? Description { get; init; }
    }
}
