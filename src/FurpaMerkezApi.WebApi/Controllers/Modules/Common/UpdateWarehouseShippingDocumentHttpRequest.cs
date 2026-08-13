using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.Common;

public sealed class UpdateWarehouseShippingDocumentHttpRequest
{
    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [Range(1, int.MaxValue)]
    public int? TargetWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int? TransitWarehouseNo { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    public IReadOnlyCollection<UpdateWarehouseShippingDocumentLineHttpRequest> Lines { get; init; } =
        Array.Empty<UpdateWarehouseShippingDocumentLineHttpRequest>();
}

public sealed class UpdateWarehouseShippingDocumentLineHttpRequest
{
    public Guid MovementGuid { get; init; }

    [Range(0, int.MaxValue)]
    public int? RowNo { get; init; }

    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(0.000001, double.MaxValue)]
    public double? Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? UnitPrice { get; init; }

    [Range(0, double.MaxValue)]
    public double? Amount { get; init; }

    [Range(1, 4)]
    public int? UnitPointer { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PartyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? LotNo { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProductResponsibilityCenter { get; init; }
}
