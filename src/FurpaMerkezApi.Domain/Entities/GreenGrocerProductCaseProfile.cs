namespace FurpaMerkezApi.Domain.Entities;

public sealed class GreenGrocerProductCaseProfile
{
    private GreenGrocerProductCaseProfile()
    {
        StockCode = string.Empty;
        InputMode = string.Empty;
        ConversionMode = string.Empty;
    }

    public Guid Id { get; private set; }

    public string StockCode { get; private set; }

    public bool IsActive { get; private set; }

    public string InputMode { get; private set; } = string.Empty;

    public string ConversionMode { get; private set; } = string.Empty;

    public double? ManualKgPerCase { get; private set; }

    public double? ManualUnitsPerCase { get; private set; }

    public double? MinExpectedKgPerCase { get; private set; }

    public double? MaxExpectedKgPerCase { get; private set; }

    public int AverageWindowDays { get; private set; }

    public int MinAverageRecordCount { get; private set; }

    public int MinAverageCaseCount { get; private set; }

    public double MaxCoefficientOfVariation { get; private set; }

    public bool RequiresManualApproval { get; private set; }

    public bool AllowOrderLinking { get; private set; }

    public double OverDeliveryTolerancePercent { get; private set; }

    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public GreenGrocerProductCaseProfile(
        Guid id,
        string stockCode,
        string inputMode,
        string conversionMode,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Green grocer product case profile id can not be empty.", nameof(id));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created by user id can not be empty.", nameof(createdByUserId));
        }

        Id = id;
        StockCode = NormalizeRequired(stockCode, nameof(stockCode), 25);
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        UpdateCore(
            true,
            inputMode,
            conversionMode,
            null,
            null,
            null,
            null,
            30,
            5,
            20,
            0.25d,
            false,
            true,
            20d,
            null);
    }

    public void Update(
        bool isActive,
        string inputMode,
        string conversionMode,
        double? manualKgPerCase,
        double? manualUnitsPerCase,
        double? minExpectedKgPerCase,
        double? maxExpectedKgPerCase,
        int averageWindowDays,
        int minAverageRecordCount,
        int minAverageCaseCount,
        double maxCoefficientOfVariation,
        bool requiresManualApproval,
        bool allowOrderLinking,
        double overDeliveryTolerancePercent,
        string? notes,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updated by user id can not be empty.", nameof(updatedByUserId));
        }

        UpdateCore(
            isActive,
            inputMode,
            conversionMode,
            manualKgPerCase,
            manualUnitsPerCase,
            minExpectedKgPerCase,
            maxExpectedKgPerCase,
            averageWindowDays,
            minAverageRecordCount,
            minAverageCaseCount,
            maxCoefficientOfVariation,
            requiresManualApproval,
            allowOrderLinking,
            overDeliveryTolerancePercent,
            notes);
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
    }

    private void UpdateCore(
        bool isActive,
        string inputMode,
        string conversionMode,
        double? manualKgPerCase,
        double? manualUnitsPerCase,
        double? minExpectedKgPerCase,
        double? maxExpectedKgPerCase,
        int averageWindowDays,
        int minAverageRecordCount,
        int minAverageCaseCount,
        double maxCoefficientOfVariation,
        bool requiresManualApproval,
        bool allowOrderLinking,
        double overDeliveryTolerancePercent,
        string? notes)
    {
        if (averageWindowDays is < 1 or > 365)
        {
            throw new ArgumentException("Average window days must be between 1 and 365.", nameof(averageWindowDays));
        }

        if (minAverageRecordCount < 0)
        {
            throw new ArgumentException("Minimum average record count can not be negative.", nameof(minAverageRecordCount));
        }

        if (minAverageCaseCount < 0)
        {
            throw new ArgumentException("Minimum average case count can not be negative.", nameof(minAverageCaseCount));
        }

        if (maxCoefficientOfVariation is < 0 or > 10)
        {
            throw new ArgumentException("Maximum coefficient of variation must be between 0 and 10.", nameof(maxCoefficientOfVariation));
        }

        if (overDeliveryTolerancePercent is < 0 or > 1000)
        {
            throw new ArgumentException("Over delivery tolerance percent must be between 0 and 1000.", nameof(overDeliveryTolerancePercent));
        }

        if (minExpectedKgPerCase.HasValue && minExpectedKgPerCase.Value < 0)
        {
            throw new ArgumentException("Minimum expected kg per case can not be negative.", nameof(minExpectedKgPerCase));
        }

        if (maxExpectedKgPerCase.HasValue && maxExpectedKgPerCase.Value < 0)
        {
            throw new ArgumentException("Maximum expected kg per case can not be negative.", nameof(maxExpectedKgPerCase));
        }

        if (minExpectedKgPerCase.HasValue &&
            maxExpectedKgPerCase.HasValue &&
            minExpectedKgPerCase.Value > maxExpectedKgPerCase.Value)
        {
            throw new ArgumentException("Minimum expected kg per case can not be greater than maximum expected kg per case.");
        }

        IsActive = isActive;
        InputMode = NormalizeRequired(inputMode, nameof(inputMode), 40);
        ConversionMode = NormalizeRequired(conversionMode, nameof(conversionMode), 60);
        ManualKgPerCase = NormalizePositiveOrNull(manualKgPerCase, nameof(manualKgPerCase));
        ManualUnitsPerCase = NormalizePositiveOrNull(manualUnitsPerCase, nameof(manualUnitsPerCase));
        MinExpectedKgPerCase = minExpectedKgPerCase;
        MaxExpectedKgPerCase = maxExpectedKgPerCase;
        AverageWindowDays = averageWindowDays;
        MinAverageRecordCount = minAverageRecordCount;
        MinAverageCaseCount = minAverageCaseCount;
        MaxCoefficientOfVariation = maxCoefficientOfVariation;
        RequiresManualApproval = requiresManualApproval;
        AllowOrderLinking = allowOrderLinking;
        OverDeliveryTolerancePercent = overDeliveryTolerancePercent;
        Notes = NormalizeOptional(notes, 1000);
    }

    private static double? NormalizePositiveOrNull(double? value, string parameterName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value <= 0)
        {
            throw new ArgumentException($"{parameterName} must be greater than zero.", parameterName);
        }

        return value.Value;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} can not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
