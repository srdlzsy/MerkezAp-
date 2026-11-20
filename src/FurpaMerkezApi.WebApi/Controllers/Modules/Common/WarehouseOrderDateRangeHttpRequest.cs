using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed class WarehouseOrderDateRangeHttpRequest : IValidatableObject
{
    private static readonly DateTime SqlDateTimeMinimum = new(1753, 1, 1);

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && StartDate.Value < SqlDateTimeMinimum)
        {
            yield return new ValidationResult(
                "Baslangic tarihi 01.01.1753 tarihinden once olamaz.",
                [nameof(StartDate)]);
        }

        if (EndDate.HasValue && EndDate.Value < SqlDateTimeMinimum)
        {
            yield return new ValidationResult(
                "Bitis tarihi 01.01.1753 tarihinden once olamaz.",
                [nameof(EndDate)]);
        }

        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value.Date > EndDate.Value.Date)
        {
            yield return new ValidationResult(
                "Baslangic tarihi bitis tarihinden sonra olamaz.",
                [nameof(StartDate), nameof(EndDate)]);
        }
    }
}
