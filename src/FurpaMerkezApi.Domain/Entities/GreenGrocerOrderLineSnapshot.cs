namespace FurpaMerkezApi.Domain.Entities;

public sealed class GreenGrocerOrderLineSnapshot
{
    private GreenGrocerOrderLineSnapshot()
    {
        DocumentSerie = string.Empty;
        StockCode = string.Empty;
        InputMode = string.Empty;
        ConversionMode = string.Empty;
        MicroUnit = string.Empty;
        AverageSource = string.Empty;
        Confidence = string.Empty;
        Status = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid WarehouseOrderLineGuid { get; private set; }

    public string DocumentSerie { get; private set; }

    public int DocumentOrderNo { get; private set; }

    public int RowNo { get; private set; }

    public DateTime OrderDate { get; private set; }

    public int SourceWarehouseNo { get; private set; }

    public int TargetWarehouseNo { get; private set; }

    public string StockCode { get; private set; }

    public double InputQuantity { get; private set; }

    public string InputMode { get; private set; }

    public string ConversionMode { get; private set; }

    public double? AverageKgPerCase { get; private set; }

    public double? UnitsPerCase { get; private set; }

    public double EstimatedQuantity { get; private set; }

    public string MicroUnit { get; private set; }

    public string AverageSource { get; private set; }

    public int? AverageRecordCount { get; private set; }

    public int? AverageCaseCount { get; private set; }

    public double? CoefficientOfVariation { get; private set; }

    public string Confidence { get; private set; }

    public double? ActualShippedQuantity { get; private set; }

    public double? ActualShippedCaseCount { get; private set; }

    public string Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }
}
