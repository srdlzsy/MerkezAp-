using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed class SendEDespatchHttpRequest : IValidatableObject
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

        var driverNameParts = (DriverNameSurname ?? string.Empty)
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
