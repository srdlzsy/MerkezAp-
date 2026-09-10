using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.WebApi.Controllers.Legacy;

[ApiController]
[AllowAnonymous]
[Route("api/legacy/e-irsaliye")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public sealed class LegacyEDespatchBridgeController(
    IEDespatchService eDespatchService,
    IOptionsMonitor<LegacyEDespatchBridgeOptions> options,
    ILogger<LegacyEDespatchBridgeController> logger) : ControllerBase
{
    [HttpPost("depolar-arasi-sevkler/{documentSerie}/{documentOrderNo:int}/gonder")]
    [HttpPost("depolar-arasi-sevkler/giden/{documentSerie}/{documentOrderNo:int}/gonder")]
    [ProducesResponseType(typeof(SendEDespatchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SendEDespatchResponse>> SendInterWarehouseEDespatch(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int warehouseNo,
        [FromBody, Required] LegacySendEDespatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var bridgeOptions = options.CurrentValue;

        if (!bridgeOptions.Enabled)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "Legacy e-despatch bridge is not enabled."
            });
        }

        if (!IsOriginAllowed(bridgeOptions))
        {
            logger.LogWarning(
                "Legacy e-despatch bridge rejected request due to origin. DocumentSerie={DocumentSerie}; DocumentOrderNo={DocumentOrderNo}; WarehouseNo={WarehouseNo}; Origin={Origin}; Referer={Referer}",
                documentSerie,
                documentOrderNo,
                warehouseNo,
                Request.Headers["Origin"].FirstOrDefault(),
                Request.Headers["Referer"].FirstOrDefault());

            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Request origin is not allowed for legacy e-despatch bridge."
            });
        }

        if (bridgeOptions.AllowedWarehouseNos.Length > 0 &&
            !bridgeOptions.AllowedWarehouseNos.Contains(warehouseNo))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Warehouse is not allowed for legacy e-despatch bridge."
            });
        }

        return Ok(await eDespatchService.SendAsync(
            new SendEDespatchRequest(
                EDespatchDocumentType.InterWarehouseShipment,
                warehouseNo,
                documentSerie,
                documentOrderNo,
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn,
                request.DriverId),
            cancellationToken));
    }

    private bool IsOriginAllowed(LegacyEDespatchBridgeOptions bridgeOptions)
    {
        var allowedOrigins = bridgeOptions.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(NormalizeOrigin)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedOrigins.Count == 0)
        {
            return true;
        }

        var requestOrigin = Request.Headers["Origin"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestOrigin))
        {
            requestOrigin = Request.Headers["Referer"].FirstOrDefault();
        }

        return !string.IsNullOrWhiteSpace(requestOrigin) &&
            allowedOrigins.Contains(NormalizeOrigin(requestOrigin));
    }

    private static string NormalizeOrigin(string origin)
    {
        var trimmed = origin.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Authority}"
            : trimmed.TrimEnd('/');
    }
}

public sealed class LegacySendEDespatchHttpRequest : IValidatableObject
{
    public Guid? DriverId { get; init; }

    [StringLength(25)]
    public string Plaque { get; init; } = string.Empty;

    [StringLength(25)]
    public string DriverNameSurname { get; init; } = string.Empty;

    [StringLength(25)]
    public string DriverTckn { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DriverId == Guid.Empty)
        {
            yield return new ValidationResult(
                "Driver id can not be empty.",
                [nameof(DriverId)]);
        }

        var hasDriverId = DriverId.HasValue && DriverId.Value != Guid.Empty;
        var plaque = Plaque ?? string.Empty;
        var driverNameSurname = DriverNameSurname ?? string.Empty;
        var driverTckn = DriverTckn ?? string.Empty;

        if (!hasDriverId)
        {
            if (string.IsNullOrWhiteSpace(plaque))
            {
                yield return new ValidationResult(
                    "Plaque is required when driver id is not provided.",
                    [nameof(Plaque)]);
            }

            if (string.IsNullOrWhiteSpace(driverNameSurname))
            {
                yield return new ValidationResult(
                    "Driver name surname is required when driver id is not provided.",
                    [nameof(DriverNameSurname)]);
            }

            if (string.IsNullOrWhiteSpace(driverTckn))
            {
                yield return new ValidationResult(
                    "Driver TCKN is required when driver id is not provided.",
                    [nameof(DriverTckn)]);
            }
        }

        var driverNameParts = driverNameSurname
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!string.IsNullOrWhiteSpace(driverNameSurname) && driverNameParts.Length < 2)
        {
            yield return new ValidationResult(
                "Driver name surname must contain both name and surname.",
                [nameof(DriverNameSurname)]);
        }

        if (!string.IsNullOrWhiteSpace(driverTckn) &&
            (driverTckn.Length != 11 || driverTckn.Any(character => !char.IsDigit(character))))
        {
            yield return new ValidationResult(
                "Driver TCKN must be 11 digits.",
                [nameof(DriverTckn)]);
        }
    }
}
