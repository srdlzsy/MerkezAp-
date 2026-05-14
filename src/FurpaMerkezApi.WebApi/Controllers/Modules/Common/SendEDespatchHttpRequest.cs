using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed class SendEDespatchHttpRequest
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
}
