using System.Globalization;
using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;

public sealed record GreenGrocerCaseCalculationSettings(
    int RecentWindowDays = 7,
    int FullWindowDays = 30,
    int T1MinRecordCount = 2,
    int T1MinCaseCount = 15,
    int T2MinCaseCount = 15,
    int T2PriorMinRecordCount = 1,
    int T2PriorMinCaseCount = 10,
    double T2MaxDriftPercent = 20d,
    double T1DriftWarningPercent = 25d,
    int T1DriftWarningMinPriorCases = 10,
    int T4aMinRecordCount = 3,
    int T4aMinCaseCount = 20,
    int T4aMaxLabelAgeDays = 14,
    double MaxCoefficientOfVariation = 0.25d,
    double? MinExpectedKgPerCase = null,
    double? MaxExpectedKgPerCase = null)
{
    public static GreenGrocerCaseCalculationSettings Default { get; } = new();
}

public sealed record GreenGrocerCaseCalculationResult(
    string CalculationTier,
    string Confidence,
    double? AverageKgPerCase,
    int AverageWindowDaysUsed,
    int RecordCount,
    int CaseCount,
    double? CoefficientOfVariation,
    DateTime? LatestLabelDate,
    int? LabelAgeDays,
    double? PriorAverageKgPerCase,
    double? DriftPercent,
    bool DriftDetected,
    double? AlternativeAverageKgPerCase,
    bool RequiresManualApproval,
    bool IsUsable,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<string> Errors);

public static class GreenGrocerCaseAverageCalculator
{
    private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static GreenGrocerCaseCalculationResult Evaluate(
        IReadOnlyCollection<GreenGrocerLabelObservation> observations,
        DateTime orderDate,
        GreenGrocerCaseCalculationSettings? settings = null)
    {
        var effectiveSettings = settings ?? GreenGrocerCaseCalculationSettings.Default;
        var recentDays = Math.Clamp(effectiveSettings.RecentWindowDays, 1, 30);
        var fullDays = Math.Max(effectiveSettings.FullWindowDays, recentDays);
        var priorDays = Math.Max(0, fullDays - recentDays);
        var maxCv = effectiveSettings.MaxCoefficientOfVariation >= 0d
            ? effectiveSettings.MaxCoefficientOfVariation
            : 0.25d;

        var endExclusive = orderDate.Date.AddDays(1);
        var recentStart = endExclusive.AddDays(-recentDays);
        var fullStart = endExclusive.AddDays(-fullDays);

        var validFull = observations
            .Where(item =>
                item.CaseCount > 0d &&
                item.NetKg > 0d &&
                item.CreatedAt >= fullStart &&
                item.CreatedAt < endExclusive)
            .ToArray();

        var recentList = validFull
            .Where(item => item.CreatedAt >= recentStart)
            .ToArray();

        var priorList = validFull
            .Where(item => item.CreatedAt < recentStart)
            .ToArray();

        var recentStats = ComputeWindowStats(recentList);
        var priorStats = ComputeWindowStats(priorList);
        var fullStats = ComputeWindowStats(validFull);

        var warnings = new List<string>();
        var errors = new List<string>();

        var latestDate = recentStats.LatestLabelDate ?? fullStats.LatestLabelDate;
        int? labelAgeDays = latestDate.HasValue
            ? Math.Max(0, (orderDate.Date - latestDate.Value.Date).Days)
            : null;

        double? driftPercent = recentStats.WeightedAverageKgPerCase is > 0d &&
                               priorStats.WeightedAverageKgPerCase is > 0d
            ? ((recentStats.WeightedAverageKgPerCase.Value - priorStats.WeightedAverageKgPerCase.Value)
               / priorStats.WeightedAverageKgPerCase.Value) * 100d
            : null;

        string tier;
        string confidence;
        double? selectedAvg;
        int windowDaysUsed;
        int recordCount;
        int caseCount;
        double? selectedCv;
        bool driftDetected = false;
        double? alternativeAvg = null;
        bool requiresApproval;
        bool isUsable = true;

        if (recentStats.RecordCount >= effectiveSettings.T1MinRecordCount &&
            recentStats.CaseCount >= effectiveSettings.T1MinCaseCount &&
            recentStats.WeightedCv.HasValue &&
            recentStats.WeightedCv.Value <= maxCv &&
            recentStats.WeightedAverageKgPerCase is > 0d)
        {
            tier = GreenGrocerProductCaseModes.Tier1;
            confidence = GreenGrocerProductCaseModes.ConfidenceHigh;
            selectedAvg = recentStats.WeightedAverageKgPerCase;
            windowDaysUsed = recentDays;
            recordCount = recentStats.RecordCount;
            caseCount = recentStats.CaseCount;
            selectedCv = recentStats.WeightedCv;
            requiresApproval = false;

            if (priorStats.RecordCount >= 1 &&
                priorStats.CaseCount >= effectiveSettings.T1DriftWarningMinPriorCases &&
                driftPercent.HasValue &&
                Math.Abs(driftPercent.Value) > effectiveSettings.T1DriftWarningPercent)
            {
                driftDetected = true;
                alternativeAvg = priorStats.WeightedAverageKgPerCase;
                warnings.Add(
                    $"Kasa agirligi degismis. Onceki {priorDays} gun ortalamasi: {FormatKg(priorStats.WeightedAverageKgPerCase!.Value)} KG/kasa (fark %{FormatSignedPercent(driftPercent.Value)}).");
            }
        }
        else if (recentStats.RecordCount == 1 &&
                 recentStats.CaseCount >= effectiveSettings.T2MinCaseCount &&
                 priorStats.RecordCount >= effectiveSettings.T2PriorMinRecordCount &&
                 priorStats.CaseCount >= effectiveSettings.T2PriorMinCaseCount &&
                 driftPercent.HasValue &&
                 Math.Abs(driftPercent.Value) <= effectiveSettings.T2MaxDriftPercent &&
                 recentStats.WeightedAverageKgPerCase is > 0d)
        {
            tier = GreenGrocerProductCaseModes.Tier2;
            confidence = GreenGrocerProductCaseModes.ConfidenceMedium;
            selectedAvg = recentStats.WeightedAverageKgPerCase;
            windowDaysUsed = recentDays;
            recordCount = recentStats.RecordCount;
            caseCount = recentStats.CaseCount;
            selectedCv = null;
            requiresApproval = false;
            warnings.Add(
                $"Son {recentDays} gunde tek buyuk parti bulundu ({recentStats.CaseCount} kasa); onceki {priorDays} gun ortalamasi ({FormatKg(priorStats.WeightedAverageKgPerCase!.Value)} KG/kasa) ile uyumlu.");
        }
        else if (recentStats.RecordCount >= 1 && recentStats.WeightedAverageKgPerCase is > 0d)
        {
            tier = GreenGrocerProductCaseModes.Tier3;
            confidence = GreenGrocerProductCaseModes.ConfidenceLow;
            requiresApproval = true;

            var useFull30Fallback =
                recentStats.RecordCount == 1 &&
                priorStats.RecordCount >= effectiveSettings.T2PriorMinRecordCount &&
                priorStats.CaseCount >= effectiveSettings.T2PriorMinCaseCount &&
                fullStats.WeightedAverageKgPerCase is > 0d;

            if (useFull30Fallback)
            {
                selectedAvg = fullStats.WeightedAverageKgPerCase;
                windowDaysUsed = fullDays;
                recordCount = fullStats.RecordCount;
                caseCount = fullStats.CaseCount;
                selectedCv = fullStats.WeightedCv;
                alternativeAvg = recentStats.WeightedAverageKgPerCase;
            }
            else
            {
                selectedAvg = recentStats.WeightedAverageKgPerCase;
                windowDaysUsed = recentDays;
                recordCount = recentStats.RecordCount;
                caseCount = recentStats.CaseCount;
                selectedCv = recentStats.WeightedCv;
                alternativeAvg = priorStats.WeightedAverageKgPerCase ?? fullStats.WeightedAverageKgPerCase;
            }

            if (recentStats.RecordCount == 1)
            {
                if (priorStats.RecordCount >= effectiveSettings.T2PriorMinRecordCount &&
                    priorStats.CaseCount >= effectiveSettings.T2PriorMinCaseCount &&
                    driftPercent.HasValue &&
                    Math.Abs(driftPercent.Value) > effectiveSettings.T2MaxDriftPercent)
                {
                    driftDetected = true;
                    warnings.Add(
                        $"Son {recentDays} gunde tek etiket var ({FormatKg(recentStats.WeightedAverageKgPerCase.Value)} KG/kasa) ve onceki {priorDays} gun ortalamasindan ({FormatKg(priorStats.WeightedAverageKgPerCase!.Value)} KG/kasa) %{FormatAbsPercent(driftPercent.Value)} farkli; guvenli {fullDays} gunluk agirlikli ortalama kullanildi (merkez/depo kontrolu onerilir).");
                }
                else if (recentStats.CaseCount < effectiveSettings.T2MinCaseCount)
                {
                    warnings.Add(
                        $"Son {recentDays} gunde yalnizca tek etiket ve {recentStats.CaseCount} kasa bulunuyor; tahmini ortalama kullanildi (merkez/depo kontrolu onerilir).");
                }
                else
                {
                    warnings.Add(
                        $"Son {recentDays} gunde tek etiket var ({recentStats.CaseCount} kasa) ve karsilastirilacak yeterli gecmis veri yok; tahmini ortalama kullanildi (merkez/depo kontrolu onerilir).");
                }
            }
            else
            {
                if (recentStats.WeightedCv.HasValue && recentStats.WeightedCv.Value > maxCv)
                {
                    warnings.Add(
                        $"Son {recentDays} gunde kasa agirliklari yuksek degiskenlik gosteriyor (CV: {recentStats.WeightedCv.Value.ToString("0.00", TrCulture)}); tahmini ortalama kullanildi (merkez/depo kontrolu onerilir).");
                }

                if (recentStats.CaseCount < effectiveSettings.T1MinCaseCount)
                {
                    warnings.Add(
                        $"Son {recentDays} gunde kasa sayisi yetersiz ({recentStats.CaseCount} kasa); tahmini ortalama kullanildi (merkez/depo kontrolu onerilir).");
                }

                if (driftPercent.HasValue && Math.Abs(driftPercent.Value) > effectiveSettings.T1DriftWarningPercent)
                {
                    driftDetected = true;
                }
            }
        }
        else if (fullStats.RecordCount >= effectiveSettings.T4aMinRecordCount &&
                 fullStats.CaseCount >= effectiveSettings.T4aMinCaseCount &&
                 fullStats.WeightedCv.HasValue &&
                 fullStats.WeightedCv.Value <= maxCv &&
                 labelAgeDays.HasValue &&
                 labelAgeDays.Value <= effectiveSettings.T4aMaxLabelAgeDays &&
                 fullStats.WeightedAverageKgPerCase is > 0d)
        {
            tier = GreenGrocerProductCaseModes.Tier4a;
            confidence = GreenGrocerProductCaseModes.ConfidenceMedium;
            selectedAvg = fullStats.WeightedAverageKgPerCase;
            windowDaysUsed = fullDays;
            recordCount = fullStats.RecordCount;
            caseCount = fullStats.CaseCount;
            selectedCv = fullStats.WeightedCv;
            requiresApproval = false;
            warnings.Add(
                $"Son {recentDays} gunde etiket yok; son {labelAgeDays.Value} gun icindeki {fullDays} gunluk agirlikli ortalama kullanildi.");
        }
        else if (fullStats.RecordCount > 0 && fullStats.WeightedAverageKgPerCase is > 0d)
        {
            tier = GreenGrocerProductCaseModes.Tier4b;
            confidence = GreenGrocerProductCaseModes.ConfidenceLow;
            selectedAvg = fullStats.WeightedAverageKgPerCase;
            windowDaysUsed = fullDays;
            recordCount = fullStats.RecordCount;
            caseCount = fullStats.CaseCount;
            selectedCv = fullStats.WeightedCv;
            requiresApproval = true;
            warnings.Add(
                $"Son {recentDays} gunde yeni etiket yok ve {fullDays} gunluk veri bayat/yetersiz ({labelAgeDays ?? fullDays} gunluk, {fullStats.RecordCount} etiket / {fullStats.CaseCount} kasa); mevcut ortalama kullanildi (merkez/depo kontrolu onerilir).");
        }
        else
        {
            tier = GreenGrocerProductCaseModes.Tier4b;
            confidence = GreenGrocerProductCaseModes.ConfidenceBlocked;
            selectedAvg = null;
            windowDaysUsed = fullDays;
            recordCount = 0;
            caseCount = 0;
            selectedCv = null;
            requiresApproval = true;
            isUsable = false;
            errors.Add("Urun icin son 30 gunde gecerli kasa kg ortalamasi yok; manuel profil tanimlanmali.");
        }

        if (selectedAvg is > 0d)
        {
            if (effectiveSettings.MinExpectedKgPerCase.HasValue &&
                selectedAvg.Value < effectiveSettings.MinExpectedKgPerCase.Value)
            {
                confidence = GreenGrocerProductCaseModes.ConfidenceLow;
                requiresApproval = true;
                warnings.Add(
                    $"Ortalama kg ({FormatKg(selectedAvg.Value)}) profil minimum beklentisinin ({FormatKg(effectiveSettings.MinExpectedKgPerCase.Value)}) altinda; onay gerekli.");
            }

            if (effectiveSettings.MaxExpectedKgPerCase.HasValue &&
                selectedAvg.Value > effectiveSettings.MaxExpectedKgPerCase.Value)
            {
                confidence = GreenGrocerProductCaseModes.ConfidenceLow;
                requiresApproval = true;
                warnings.Add(
                    $"Ortalama kg ({FormatKg(selectedAvg.Value)}) profil maksimum beklentisinin ({FormatKg(effectiveSettings.MaxExpectedKgPerCase.Value)}) ustunde; onay gerekli.");
            }
        }

        return new GreenGrocerCaseCalculationResult(
            tier,
            confidence,
            selectedAvg,
            windowDaysUsed,
            recordCount,
            caseCount,
            selectedCv,
            latestDate,
            labelAgeDays,
            priorStats.WeightedAverageKgPerCase,
            driftPercent,
            driftDetected,
            alternativeAvg,
            requiresApproval,
            isUsable,
            warnings,
            errors);
    }

    private static WindowStats ComputeWindowStats(IReadOnlyList<GreenGrocerLabelObservation> items)
    {
        if (items.Count == 0)
        {
            return WindowStats.Empty;
        }

        var totalCases = 0d;
        var totalNetKg = 0d;
        var latestDate = items[0].CreatedAt;

        foreach (var item in items)
        {
            totalCases += item.CaseCount;
            totalNetKg += item.NetKg;
            if (item.CreatedAt > latestDate)
            {
                latestDate = item.CreatedAt;
            }
        }

        if (totalCases <= 0d || totalNetKg <= 0d)
        {
            return WindowStats.Empty;
        }

        var weightedMean = totalNetKg / totalCases;
        double? weightedCv = null;

        if (items.Count >= 2 && weightedMean > 0d)
        {
            var weightedSquaredDiffSum = 0d;
            foreach (var item in items)
            {
                var kgPerCase = item.NetKg / item.CaseCount;
                var diff = kgPerCase - weightedMean;
                weightedSquaredDiffSum += item.CaseCount * diff * diff;
            }

            var weightedVariance = Math.Max(0d, weightedSquaredDiffSum / totalCases);
            weightedCv = Math.Sqrt(weightedVariance) / weightedMean;
        }

        return new WindowStats(
            items.Count,
            (int)Math.Round(totalCases, MidpointRounding.AwayFromZero),
            weightedMean,
            weightedCv,
            latestDate);
    }

    private static string FormatKg(double value) => value.ToString("0.00", TrCulture);

    private static string FormatAbsPercent(double value) => Math.Abs(value).ToString("0", TrCulture);

    private static string FormatSignedPercent(double value) =>
        value >= 0d
            ? $"+{value.ToString("0", TrCulture)}"
            : value.ToString("0", TrCulture);

    private sealed record WindowStats(
        int RecordCount,
        int CaseCount,
        double? WeightedAverageKgPerCase,
        double? WeightedCv,
        DateTime? LatestLabelDate)
    {
        public static WindowStats Empty { get; } = new(0, 0, null, null, null);
    }
}
