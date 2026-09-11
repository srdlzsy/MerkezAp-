using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.WebApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    MikroDbContext mikroDbContext,
    IOptionsMonitor<LegacyEDespatchBridgeOptions> options,
    ILogger<LegacyEDespatchBridgeController> logger) : ControllerBase
{
    private const byte CompanyDispatchDocumentType = 1;
    private const byte OutgoingMovementType = 1;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte InterWarehouseShipmentDocumentType = 17;

    [HttpPost("{documentKind}/{documentSerie}/{documentOrderNo:int}/gonder")]
    [HttpPost("{documentKind}/giden/{documentSerie}/{documentOrderNo:int}/gonder")]
    [ProducesResponseType(typeof(SendEDespatchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SendEDespatchResponse>> SendEDespatch(
        string documentKind,
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

        if (!TryResolveDocumentType(documentKind, out var documentType))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "Document kind is not supported for legacy e-despatch bridge."
            });
        }

        if (!IsOriginAllowed(bridgeOptions))
        {
            logger.LogWarning(
                "Legacy e-despatch bridge rejected request due to origin. DocumentKind={DocumentKind}; DocumentSerie={DocumentSerie}; DocumentOrderNo={DocumentOrderNo}; WarehouseNo={WarehouseNo}; Origin={Origin}; Referer={Referer}",
                documentKind,
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

        var effectiveDocumentType = await ResolveLegacyDocumentTypeAsync(
            documentType,
            documentSerie,
            documentOrderNo,
            warehouseNo,
            cancellationToken);

        if (effectiveDocumentType != documentType)
        {
            logger.LogInformation(
                "Legacy e-despatch bridge corrected document type from Mikro movement rows. RequestedDocumentType={RequestedDocumentType}; EffectiveDocumentType={EffectiveDocumentType}; DocumentKind={DocumentKind}; Document={DocumentSerie}/{DocumentOrderNo}; WarehouseNo={WarehouseNo}",
                documentType,
                effectiveDocumentType,
                documentKind,
                documentSerie,
                documentOrderNo,
                warehouseNo);
        }

        return Ok(await eDespatchService.SendAsync(
            new SendEDespatchRequest(
                effectiveDocumentType,
                warehouseNo,
                documentSerie,
                documentOrderNo,
                request.Plaque,
                request.DriverNameSurname,
                request.DriverTckn,
                request.DriverId),
            cancellationToken));
    }

    private async Task<EDespatchDocumentType> ResolveLegacyDocumentTypeAsync(
        EDespatchDocumentType requestedDocumentType,
        string documentSerie,
        int documentOrderNo,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var requestedCount = await CountLegacyMovementRowsAsync(
            requestedDocumentType,
            documentSerie,
            documentOrderNo,
            warehouseNo,
            cancellationToken);
        if (requestedCount > 0)
        {
            return requestedDocumentType;
        }

        var fallbackDocumentType = requestedDocumentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => EDespatchDocumentType.CompanyReturn,
            EDespatchDocumentType.CompanyReturn => EDespatchDocumentType.OutgoingCompanyShipment,
            EDespatchDocumentType.InterWarehouseShipment => EDespatchDocumentType.WarehouseReturn,
            EDespatchDocumentType.WarehouseReturn => EDespatchDocumentType.InterWarehouseShipment,
            _ => requestedDocumentType
        };

        if (fallbackDocumentType == requestedDocumentType)
        {
            return requestedDocumentType;
        }

        var fallbackCount = await CountLegacyMovementRowsAsync(
            fallbackDocumentType,
            documentSerie,
            documentOrderNo,
            warehouseNo,
            cancellationToken);

        return fallbackCount > 0
            ? fallbackDocumentType
            : requestedDocumentType;
    }

    private Task<int> CountLegacyMovementRowsAsync(
        EDespatchDocumentType documentType,
        string documentSerie,
        int documentOrderNo,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var query = mikroDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo);

        query = documentType switch
        {
            EDespatchDocumentType.OutgoingCompanyShipment => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            EDespatchDocumentType.CompanyReturn => query.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            EDespatchDocumentType.InterWarehouseShipment => query.Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            EDespatchDocumentType.WarehouseReturn => query.Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            _ => query.Where(_ => false)
        };

        return query.CountAsync(cancellationToken);
    }

    private static bool TryResolveDocumentType(string documentKind, out EDespatchDocumentType documentType)
    {
        documentType = documentKind.Trim().ToLowerInvariant() switch
        {
            "depolar-arasi-sevkler" => EDespatchDocumentType.InterWarehouseShipment,
            "depo-sevk" => EDespatchDocumentType.InterWarehouseShipment,
            "depo-sevki" => EDespatchDocumentType.InterWarehouseShipment,
            "depo-iadeleri" => EDespatchDocumentType.WarehouseReturn,
            "depo-iade" => EDespatchDocumentType.WarehouseReturn,
            "firma-sevkleri" => EDespatchDocumentType.OutgoingCompanyShipment,
            "firma-sevk" => EDespatchDocumentType.OutgoingCompanyShipment,
            "firma-iadeleri" => EDespatchDocumentType.CompanyReturn,
            "firma-iade" => EDespatchDocumentType.CompanyReturn,
            _ => default
        };

        return documentType != default || string.Equals(
            documentKind,
            "depolar-arasi-sevkler",
            StringComparison.OrdinalIgnoreCase);
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
