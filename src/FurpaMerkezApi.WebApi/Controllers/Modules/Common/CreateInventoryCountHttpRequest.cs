using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed class CreateInventoryCountHttpRequest
{
    public Guid? ClientRequestId { get; init; }

    [StringLength(25)]
    public string? Name { get; init; }

    public DateTime? DocumentDate { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateInventoryCountLineHttpRequest> Lines { get; init; } =
        Array.Empty<CreateInventoryCountLineHttpRequest>();
}

public sealed class CreateInventoryCountLineHttpRequest
{
    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public double Quantity { get; init; }

    [StringLength(50)]
    public string? Barcode { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;
}
