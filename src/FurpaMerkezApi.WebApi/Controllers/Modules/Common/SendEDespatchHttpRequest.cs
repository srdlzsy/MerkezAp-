using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed class SendEDespatchHttpRequest : IValidatableObject
{
    [Required]
    [StringLength(25)]
    public string Plaque { get; init; } = string.Empty;

    [Required]
    [StringLength(25)]
    public string DriverNameSurname { get; init; } = string.Empty;

    [Required]
    [StringLength(25)]
    public string DriverTckn { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var driverNameParts = (DriverNameSurname ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (driverNameParts.Length < 2)
        {
            yield return new ValidationResult(
                "Driver name surname must contain both name and surname.",
                [nameof(DriverNameSurname)]);
        }
    }
}
