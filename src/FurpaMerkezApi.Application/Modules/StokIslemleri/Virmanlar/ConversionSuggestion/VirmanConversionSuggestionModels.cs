namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;

public sealed record VirmanConversionSuggestionRequest(
    string SourceStockCode,
    double SourceQuantity);

public sealed record VirmanConversionSuggestionDto(
    string SourceStockCode,
    string SourceStockName,
    string SourceUnitName,
    double SourceQuantity,
    string? TargetStockCode,
    string? TargetStockName,
    string? TargetUnitName,
    double? Multiplier,
    double? TargetQuantity,
    int SampleCount,
    int TargetMatchCount,
    int MultiplierMatchCount,
    double TargetConfidencePercent,
    double MultiplierConfidencePercent,
    double ConfidencePercent,
    bool IsReliable,
    string SuggestionSource,
    DateTime LookbackStartDate,
    DateTime LookbackEndDate,
    int MinimumSampleCount,
    int MaximumSampleCount,
    double MinimumConfidencePercent,
    string? Warning);
