using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.MalKabulFarklari;

[ApiController]
[Route("api/mal-kabul-islemleri/mal-kabul-farklari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class MalKabulFarklariController(
    IListWarehouseReceivingDifferencesUseCase listWarehouseReceivingDifferencesUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "mal-kabul-islemleri";
    private const string ModuleName = "MalKabulIslemleri";
    private const string MenuCode = "mal-kabul-farklari";
    private const string MenuName = "MalKabulFarklari";
    private const string ListPolicy = "mal-kabul-islemleri.mal-kabul-farklari.list";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseReceivingDifferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseReceivingDifferenceDto>>> List(
        [FromQuery] WarehouseReceivingDifferenceListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ListByScopeAsync(
            request,
            ResolveScope(request.Scope),
            cancellationToken));

    [HttpGet("olusturdugum")]
    [HttpGet("created")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseReceivingDifferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseReceivingDifferenceDto>>> ListCreatedByWarehouse(
        [FromQuery] WarehouseReceivingDifferenceDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ListByScopeAsync(
            request,
            WarehouseReceivingDifferenceScope.CreatedByWarehouse,
            cancellationToken));

    [HttpGet("kabul-ettigim")]
    [HttpGet("accepted")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseReceivingDifferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseReceivingDifferenceDto>>> ListAcceptedByWarehouse(
        [FromQuery] WarehouseReceivingDifferenceDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await ListByScopeAsync(
            request,
            WarehouseReceivingDifferenceScope.AcceptedByWarehouse,
            cancellationToken));

    private async Task<IReadOnlyCollection<WarehouseReceivingDifferenceDto>> ListByScopeAsync(
        WarehouseReceivingDifferenceDateRangeHttpRequest request,
        WarehouseReceivingDifferenceScope scope,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);

        return await listWarehouseReceivingDifferencesUseCase.ExecuteAsync(
            new WarehouseReceivingDifferenceListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value,
                scope),
            cancellationToken);
    }

    private static WarehouseReceivingDifferenceScope ResolveScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return WarehouseReceivingDifferenceScope.AcceptedByWarehouse;
        }

        var normalizedScope = scope.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalizedScope switch
        {
            "created" or "createdbywarehouse" or "olusturdugum" =>
                WarehouseReceivingDifferenceScope.CreatedByWarehouse,
            "accepted" or "acceptedbywarehouse" or "kabulettigim" =>
                WarehouseReceivingDifferenceScope.AcceptedByWarehouse,
            _ => throw new ArgumentException("Scope must be one of: accepted, created.")
        };
    }
}

public class WarehouseReceivingDifferenceDateRangeHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }
}

public sealed class WarehouseReceivingDifferenceListHttpRequest : WarehouseReceivingDifferenceDateRangeHttpRequest
{
    public string? Scope { get; init; }
}
