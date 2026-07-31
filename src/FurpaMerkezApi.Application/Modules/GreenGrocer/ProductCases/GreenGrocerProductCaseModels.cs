namespace FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;

public static class GreenGrocerProductCaseModes
{
    public const string InputCase = "Case";
    public const string InputPack = "Pack";
    public const string InputPiece = "Piece";
    public const string InputKgDirect = "KgDirect";
    public const string InputSarf = "Sarf";

    public const string ConversionLabelAverageKgPerCase = "LabelAverageKgPerCase";
    public const string ConversionManualKgPerCase = "ManualKgPerCase";
    public const string ConversionFixedUnitsPerCase = "FixedUnitsPerCase";
    public const string ConversionDirectQuantity = "DirectQuantity";
    public const string ConversionManualOnly = "ManualOnly";
    public const string ConversionBlocked = "Blocked";

    public const string AverageSourceLabelHistory = "LabelHistory";
    public const string AverageSourceManualProfile = "ManualProfile";
    public const string AverageSourceStockUnitFactor = "StockUnitFactor";
    public const string AverageSourceDirect = "Direct";
    public const string AverageSourceNone = "None";

    public const string ConfidenceHigh = "High";
    public const string ConfidenceMedium = "Medium";
    public const string ConfidenceLow = "Low";
    public const string ConfidenceBlocked = "Blocked";
}

public sealed record GreenGrocerProductCaseProfileListRequest(
    string? Search = null,
    bool IncludeInactive = false,
    int Take = 100);

public sealed record SaveGreenGrocerProductCaseProfileRequest(
    bool IsActive,
    string InputMode,
    string ConversionMode,
    double? ManualKgPerCase,
    double? ManualUnitsPerCase,
    double? MinExpectedKgPerCase,
    double? MaxExpectedKgPerCase,
    int AverageWindowDays,
    int MinAverageRecordCount,
    int MinAverageCaseCount,
    double MaxCoefficientOfVariation,
    bool RequiresManualApproval,
    bool AllowOrderLinking,
    double OverDeliveryTolerancePercent,
    string? Notes);

public sealed record GreenGrocerProductCaseProfileDto(
    Guid Id,
    string StockCode,
    string StockName,
    string ModelCode,
    string ModelName,
    string Unit1,
    string Unit2,
    double Unit2Factor,
    bool IsActive,
    string InputMode,
    string ConversionMode,
    double? ManualKgPerCase,
    double? ManualUnitsPerCase,
    double? MinExpectedKgPerCase,
    double? MaxExpectedKgPerCase,
    int AverageWindowDays,
    int MinAverageRecordCount,
    int MinAverageCaseCount,
    double MaxCoefficientOfVariation,
    bool RequiresManualApproval,
    bool AllowOrderLinking,
    double OverDeliveryTolerancePercent,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record GreenGrocerProductCaseResolutionRequest(
    string StockCode,
    double InputQuantity,
    int SourceWarehouseNo,
    int? TargetWarehouseNo = null,
    DateTime? OrderDate = null);

public sealed record GreenGrocerProductCaseResolutionDto(
    string StockCode,
    string StockName,
    string ModelCode,
    string ModelName,
    string Unit1,
    string Unit2,
    double Unit2Factor,
    double InputQuantity,
    string InputMode,
    string ConversionMode,
    string MicroUnit,
    double EstimatedQuantity,
    double? AverageKgPerCase,
    double? UnitsPerCase,
    string AverageSource,
    int? AverageRecordCount,
    int? AverageCaseCount,
    double? CoefficientOfVariation,
    DateTime? LatestLabelDate,
    string Confidence,
    bool RequiresManualApproval,
    bool IsOrderLinkable,
    bool IsUsable,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<string> Errors);
