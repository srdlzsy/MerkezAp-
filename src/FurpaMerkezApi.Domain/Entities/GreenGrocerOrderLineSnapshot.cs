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

    public GreenGrocerOrderLineSnapshot(
        Guid id,
        Guid warehouseOrderLineGuid,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        DateTime orderDate,
        int sourceWarehouseNo,
        int targetWarehouseNo,
        string stockCode,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        double? averageKgPerCase,
        double? unitsPerCase,
        double estimatedQuantity,
        string microUnit,
        string averageSource,
        int? averageRecordCount,
        int? averageCaseCount,
        double? coefficientOfVariation,
        string confidence,
        bool requiresManualApproval,
        bool allowOrderLinking,
        double overDeliveryTolerancePercent,
        double severeOverDeliveryMultiplier,
        Guid createdByUserId,
        DateTime createdAtUtc,
        string? calculationTier = null,
        int? averageWindowDaysUsed = null,
        int? labelAgeDays = null,
        double? priorAverageKgPerCase = null,
        double? driftPercent = null,
        bool manualApprovalConfirmed = false,
        double? approvedKgPerCase = null)
    {
        Id = id;
        WarehouseOrderLineGuid = warehouseOrderLineGuid;
        DocumentSerie = NormalizeRequired(documentSerie, nameof(documentSerie), 20);
        DocumentOrderNo = documentOrderNo;
        RowNo = rowNo;
        OrderDate = orderDate.Date;
        SourceWarehouseNo = sourceWarehouseNo;
        TargetWarehouseNo = targetWarehouseNo;
        StockCode = NormalizeRequired(stockCode, nameof(stockCode), 25);
        InputQuantity = inputQuantity;
        InputMode = NormalizeRequired(inputMode, nameof(inputMode), 40);
        ConversionMode = NormalizeRequired(conversionMode, nameof(conversionMode), 60);
        AverageKgPerCase = averageKgPerCase;
        UnitsPerCase = unitsPerCase;
        EstimatedQuantity = estimatedQuantity;
        MicroUnit = NormalizeRequired(microUnit, nameof(microUnit), 20);
        AverageSource = NormalizeRequired(averageSource, nameof(averageSource), 60);
        AverageRecordCount = averageRecordCount;
        AverageCaseCount = averageCaseCount;
        CoefficientOfVariation = coefficientOfVariation;
        Confidence = NormalizeRequired(confidence, nameof(confidence), 30);
        RequiresManualApproval = requiresManualApproval;
        AllowOrderLinking = allowOrderLinking;
        OverDeliveryTolerancePercent = overDeliveryTolerancePercent;
        SevereOverDeliveryMultiplier = severeOverDeliveryMultiplier;
        CalculationTier = NormalizeOptional(calculationTier, 10);
        AverageWindowDaysUsed = averageWindowDaysUsed;
        LabelAgeDays = labelAgeDays;
        PriorAverageKgPerCase = priorAverageKgPerCase;
        DriftPercent = driftPercent;
        ManualApprovalConfirmed = manualApprovalConfirmed;
        ApprovedKgPerCase = approvedKgPerCase;
        ActualShippedQuantity = null;
        ActualShippedCaseCount = null;
        Status = "Ordered";
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedByUserId = null;
        UpdatedAtUtc = null;
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

    public bool RequiresManualApproval { get; private set; }

    public bool AllowOrderLinking { get; private set; }

    public double OverDeliveryTolerancePercent { get; private set; }

    public double SevereOverDeliveryMultiplier { get; private set; }

    public string? CalculationTier { get; private set; }

    public int? AverageWindowDaysUsed { get; private set; }

    public int? LabelAgeDays { get; private set; }

    public double? PriorAverageKgPerCase { get; private set; }

    public double? DriftPercent { get; private set; }

    public bool ManualApprovalConfirmed { get; private set; }

    public double? ApprovedKgPerCase { get; private set; }

    public double? ActualShippedQuantity { get; private set; }

    public double? ActualShippedCaseCount { get; private set; }

    public string Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public void RecordShipment(
        double actualShippedQuantity,
        double? actualShippedCaseCount,
        Guid? updatedByUserId,
        DateTime updatedAtUtc)
    {
        if (actualShippedQuantity < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(actualShippedQuantity));
        }

        ActualShippedQuantity = actualShippedQuantity;
        ActualShippedCaseCount = actualShippedCaseCount;
        Status = actualShippedQuantity <= 0d
            ? "Ordered"
            : actualShippedQuantity + 0.0005d >= EstimatedQuantity
                ? "Shipped"
                : "PartiallyShipped";
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException($"{parameterName} can not be longer than {maxLength} characters.", parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
