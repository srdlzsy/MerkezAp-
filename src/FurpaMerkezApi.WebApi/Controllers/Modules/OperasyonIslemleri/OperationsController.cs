using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;

[ApiController]
[Route("api/operations")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class OperationsController(IOperationsService operationsService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "operasyon-islemleri";
    private const string ModuleName = "OperasyonIslemleri";
    private const string MenuCode = "operations";
    private const string MenuName = "Operations";
    private const string ListPolicy = "operasyon-islemleri.operations.list";
    private const string DetailPolicy = "operasyon-islemleri.operations.detail";
    private const string CreatePolicy = "operasyon-islemleri.operations.create";
    private const string UpdatePolicy = "operasyon-islemleri.operations.update";

    [HttpGet("scalesfile")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(OperationJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationJobDto>> CreateScalesFileJob(CancellationToken cancellationToken)
    {
        var response = await operationsService.QueueScalesFileAsync(
            User.GetRequiredWarehouseNo(),
            User.GetRequiredUserId(),
            cancellationToken);

        return AcceptedAtAction(nameof(GetJob), new { jobId = response.JobId }, response);
    }

    [HttpGet("productbarcodeplunofile")]
    [HttpGet("productbarcodeplonofile")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(OperationJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationJobDto>> CreateProductBarcodePluNoFileJob(CancellationToken cancellationToken)
    {
        var response = await operationsService.QueueProductBarcodePluNoFileAsync(
            User.GetRequiredWarehouseNo(),
            User.GetRequiredUserId(),
            cancellationToken);

        return AcceptedAtAction(nameof(GetJob), new { jobId = response.JobId }, response);
    }

    [HttpGet("cashierfile")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(OperationJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationJobDto>> CashierFileJob(CancellationToken cancellationToken)
    {
        var response = await operationsService.QueueCashierFileAsync(
            User.GetRequiredWarehouseNo(),
            User.GetRequiredUserId(),
            cancellationToken);

        return AcceptedAtAction(nameof(GetJob), new { jobId = response.JobId }, response);
    }

    [HttpGet("promofile")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(OperationJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OperationJobDto>> PromoFile(CancellationToken cancellationToken)
    {
        var response = await operationsService.QueuePromoFileAsync(
            User.GetRequiredWarehouseNo(),
            User.GetRequiredUserId(),
            cancellationToken);

        return AcceptedAtAction(nameof(GetJob), new { jobId = response.JobId }, response);
    }

    [HttpGet("jobs/{jobId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(OperationJobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationJobDetailDto>> GetJob(Guid jobId, CancellationToken cancellationToken) =>
        Ok(await operationsService.GetJobAsync(jobId, cancellationToken));

    [HttpGet("getauthorizationfile")]
    [HttpGet("authorization-files")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AuthorizationFileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AuthorizationFileDto>>> GetAuthorizationFile(
        CancellationToken cancellationToken) =>
        Ok(await operationsService.GetAuthorizationFilesAsync(cancellationToken));

    [HttpPost("saveauthorizationfile")]
    [HttpPost("authorization-files")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveAuthorizationFile(
        [FromBody] IReadOnlyCollection<SaveAuthorizationFileHttpRequest> fileList,
        CancellationToken cancellationToken)
    {
        await operationsService.SaveAuthorizationFilesAsync(
            fileList
                .Select(item => new SaveAuthorizationFileItemRequest(
                    item.Id,
                    item.Name,
                    item.Z,
                    item.R,
                    item.X))
                .ToArray(),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created);
    }
}

public sealed class SaveAuthorizationFileHttpRequest
{
    [Range(1, int.MaxValue)]
    public int Id { get; init; }

    public DateTime? UpdateDate { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    public bool Z { get; init; }

    public bool R { get; init; }

    public bool X { get; init; }
}
