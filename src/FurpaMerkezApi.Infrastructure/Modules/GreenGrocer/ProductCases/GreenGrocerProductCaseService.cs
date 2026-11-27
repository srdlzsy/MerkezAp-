using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;

public sealed class GreenGrocerProductCaseService(
    AuthDbContext authDbContext,
    MikroDbContext mikroDbContext,
    FurpaDbContext furpaDbContext,
    IClock clock,
    IOptionsMonitor<GreenGrocerProductCaseOptions> options,
    IGreenGrocerLabelStatsReader? labelStatsReader = null)
    : IGreenGrocerProductCaseService
{
    private readonly IGreenGrocerLabelStatsReader _labelStatsReader =
        labelStatsReader ?? new SqlGreenGrocerLabelStatsReader(furpaDbContext);
    private const int DefaultGreenGrocerWarehouseNo = 56;
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const int DefaultAverageWindowDays = 30;
    private const int DefaultMinAverageRecordCount = 5;
    private const int DefaultMinAverageCaseCount = 20;
    private const double DefaultMaxCoefficientOfVariation = 0.25d;
    private const double DefaultOverDeliveryTolerancePercent = 20d;

    private static readonly string[] GreenGrocerModelCodes = ["10", "11", "12", "23"];
    private static readonly string[] ValidInputModes =
    [
        GreenGrocerProductCaseModes.InputCase,
        GreenGrocerProductCaseModes.InputPack,
        GreenGrocerProductCaseModes.InputPiece,
        GreenGrocerProductCaseModes.InputKgDirect,
        GreenGrocerProductCaseModes.InputSarf
    ];

    private static readonly string[] ValidConversionModes =
    [
        GreenGrocerProductCaseModes.ConversionLabelAverageKgPerCase,
        GreenGrocerProductCaseModes.ConversionManualKgPerCase,
        GreenGrocerProductCaseModes.ConversionFixedUnitsPerCase,
        GreenGrocerProductCaseModes.ConversionDirectQuantity,
        GreenGrocerProductCaseModes.ConversionManualOnly,
        GreenGrocerProductCaseModes.ConversionBlocked
    ];

    public async Task<IReadOnlyCollection<GreenGrocerProductCaseProfileDto>> ListProfilesAsync(
        GreenGrocerProductCaseProfileListRequest request,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (!options.CurrentValue.AuthDbProfileStorageEnabled)
        {
            return [];
        }

        var take = NormalizeTake(request.Take);
        var search = NormalizeSearch(request.Search);

        var query = authDbContext.GreenGrocerProductCaseProfiles.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(profile => profile.IsActive);
        }

        var profiles = await query
            .OrderBy(profile => profile.StockCode)
            .Take(MaxTake)
            .ToListAsync(cancellationToken);

        var stockInfos = await GetStockInfosAsync(
            profiles.Select(profile => profile.StockCode).ToArray(),
            cancellationToken);

        var items = profiles
            .Select(profile => MapProfile(profile, stockInfos.GetValueOrDefault(NormalizeStockCode(profile.StockCode))))
            .Where(profile =>
                search is null ||
                profile.StockCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                profile.StockName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                profile.ModelCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                profile.ModelName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (profile.Notes?.Contains(search, StringComparison.OrdinalIgnoreCase) == true))
            .Take(take)
            .ToArray();

        return items;
    }

    public async Task<GreenGrocerProductCaseProfileDto> GetProfileAsync(
        string stockCode,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureProfileStorageEnabled();

        var normalizedStockCode = NormalizeStockCode(stockCode);
        var profile = await authDbContext.GreenGrocerProductCaseProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.StockCode == normalizedStockCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Green grocer product case profile was not found: {normalizedStockCode}");

        var stockInfo = await GetStockInfoAsync(normalizedStockCode, cancellationToken);

        return MapProfile(profile, stockInfo);
    }

    public async Task<GreenGrocerProductCaseProfileDto> SaveProfileAsync(
        string stockCode,
        SaveGreenGrocerProductCaseProfileRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureProfileStorageEnabled();

        var normalizedStockCode = NormalizeStockCode(stockCode);
        var inputMode = GetCanonicalMode(request.InputMode, ValidInputModes);
        var conversionMode = GetCanonicalMode(request.ConversionMode, ValidConversionModes);
        var normalizedRequest = request with
        {
            InputMode = inputMode,
            ConversionMode = conversionMode
        };
        ValidateProfileRequest(normalizedRequest);
        var severeMultiplier = ResolveSevereOverDeliveryMultiplier();
        if (normalizedRequest.OverDeliveryTolerancePercent >= (severeMultiplier - 1d) * 100d)
        {
            throw new ArgumentException(
                "Over delivery tolerance must stay below the configured critical delivery limit.");
        }

        var stockInfo = await GetStockInfoAsync(normalizedStockCode, cancellationToken);
        EnsureGreenGrocerStock(stockInfo, normalizedRequest.IsActive);

        var profile = await authDbContext.GreenGrocerProductCaseProfiles
            .FirstOrDefaultAsync(item => item.StockCode == normalizedStockCode, cancellationToken);

        if (profile is null)
        {
            profile = new GreenGrocerProductCaseProfile(
                Guid.NewGuid(),
                normalizedStockCode,
                normalizedRequest.InputMode,
                normalizedRequest.ConversionMode,
                changedByUserId,
                clock.UtcNow);
            await authDbContext.GreenGrocerProductCaseProfiles.AddAsync(profile, cancellationToken);
        }

        profile.Update(
            normalizedRequest.IsActive,
            normalizedRequest.InputMode,
            normalizedRequest.ConversionMode,
            normalizedRequest.ManualKgPerCase,
            normalizedRequest.ManualUnitsPerCase,
            normalizedRequest.MinExpectedKgPerCase,
            normalizedRequest.MaxExpectedKgPerCase,
            normalizedRequest.AverageWindowDays,
            normalizedRequest.MinAverageRecordCount,
            normalizedRequest.MinAverageCaseCount,
            normalizedRequest.MaxCoefficientOfVariation,
            normalizedRequest.RequiresManualApproval,
            normalizedRequest.AllowOrderLinking,
            normalizedRequest.OverDeliveryTolerancePercent,
            normalizedRequest.Notes,
            changedByUserId,
            clock.UtcNow);

        await authDbContext.SaveChangesAsync(cancellationToken);

        return MapProfile(profile, stockInfo);
    }

    public async Task DeleteProfileAsync(
        string stockCode,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureProfileStorageEnabled();

        var normalizedStockCode = NormalizeStockCode(stockCode);
        var profile = await authDbContext.GreenGrocerProductCaseProfiles
            .FirstOrDefaultAsync(item => item.StockCode == normalizedStockCode, cancellationToken)
            ?? throw new KeyNotFoundException($"Green grocer product case profile was not found: {normalizedStockCode}");

        profile.Update(
            false,
            profile.InputMode,
            profile.ConversionMode,
            profile.ManualKgPerCase,
            profile.ManualUnitsPerCase,
            profile.MinExpectedKgPerCase,
            profile.MaxExpectedKgPerCase,
            profile.AverageWindowDays,
            profile.MinAverageRecordCount,
            profile.MinAverageCaseCount,
            profile.MaxCoefficientOfVariation,
            profile.RequiresManualApproval,
            profile.AllowOrderLinking,
            profile.OverDeliveryTolerancePercent,
            profile.Notes,
            changedByUserId,
            clock.UtcNow);

        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<GreenGrocerProductCaseResolutionDto> PreviewResolutionAsync(
        GreenGrocerProductCaseResolutionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (request.InputQuantity <= 0)
        {
            throw new ArgumentException("Input quantity must be greater than zero.", nameof(request.InputQuantity));
        }

        var sourceWarehouseNo = ResolveSourceWarehouseNo();
        if (request.SourceWarehouseNo != sourceWarehouseNo)
        {
            throw new ArgumentException(
                $"Green grocer case resolution can only be used for source warehouse {sourceWarehouseNo}.");
        }

        var stockCode = NormalizeStockCode(request.StockCode);
        var stockInfo = await GetStockInfoAsync(stockCode, cancellationToken);
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var profile = options.CurrentValue.AuthDbProfileStorageEnabled
            ? await authDbContext.GreenGrocerProductCaseProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.StockCode == stockCode && item.IsActive, cancellationToken)
            : null;

        var warnings = new List<string>();
        var errors = new List<string>();

        if (!IsGreenGrocerModel(stockInfo.ModelCode))
        {
            errors.Add("Urun 56 MANAV DEPO model kodlari icinde degil.");
        }

        if (stockInfo.IsPassive)
        {
            errors.Add("Pasif urun icin manav kasa profili kullanilamaz.");
        }

        if (stockInfo.IsDelisted)
        {
            errors.Add("DLS urun manav kasa siparisine eklenemez.");
        }

        var result = profile is not null
            ? await ResolveWithProfileAsync(stockInfo, profile, request.InputQuantity, orderDate, warnings, errors, cancellationToken)
            : await ResolveAutomaticallyAsync(stockInfo, request.InputQuantity, orderDate, warnings, errors, cancellationToken);

        return result with
        {
            Warnings = warnings,
            Errors = errors,
            IsUsable = errors.Count == 0 && result.IsUsable
        };
    }

    private void EnsureEnabled()
    {
        if (!options.CurrentValue.Enabled)
        {
            throw new InvalidOperationException("Green grocer product case resolution is disabled by configuration.");
        }
    }

    private void EnsureProfileStorageEnabled()
    {
        if (!options.CurrentValue.AuthDbProfileStorageEnabled)
        {
            throw new InvalidOperationException(
                "Green grocer product case profile storage is disabled by configuration.");
        }
    }

    private async Task<GreenGrocerProductCaseResolutionDto> ResolveWithProfileAsync(
        StockInfo stockInfo,
        GreenGrocerProductCaseProfile profile,
        double inputQuantity,
        DateTime orderDate,
        List<string> warnings,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var inputMode = NormalizeMode(profile.InputMode);
        var conversionMode = NormalizeMode(profile.ConversionMode);
        var labelCalculation = conversionMode == GreenGrocerProductCaseModes.ConversionLabelAverageKgPerCase
            ? await EvaluateLabelAverageAsync(
                stockInfo.StockCode,
                orderDate,
                new GreenGrocerCaseCalculationSettings(
                    FullWindowDays: Math.Max(profile.AverageWindowDays, 7),
                    MaxCoefficientOfVariation: profile.MaxCoefficientOfVariation,
                    MinExpectedKgPerCase: profile.MinExpectedKgPerCase,
                    MaxExpectedKgPerCase: profile.MaxExpectedKgPerCase),
                cancellationToken)
            : null;

        var result = conversionMode switch
        {
            GreenGrocerProductCaseModes.ConversionLabelAverageKgPerCase =>
                BuildLabelAverageResolution(
                    stockInfo,
                    inputQuantity,
                    inputMode,
                    conversionMode,
                    profile.AllowOrderLinking,
                    profile.RequiresManualApproval,
                    labelCalculation!,
                    warnings,
                    errors),

            GreenGrocerProductCaseModes.ConversionManualKgPerCase =>
                ResolveManualKg(stockInfo, inputQuantity, inputMode, conversionMode, profile, warnings, errors),

            GreenGrocerProductCaseModes.ConversionFixedUnitsPerCase =>
                ResolveFixedUnits(stockInfo, inputQuantity, inputMode, conversionMode, profile, warnings, errors),

            GreenGrocerProductCaseModes.ConversionDirectQuantity =>
                CreateResolution(
                    stockInfo,
                    inputQuantity,
                    inputMode,
                    conversionMode,
                    NormalizeMicroUnit(stockInfo.Unit1),
                    inputQuantity,
                    null,
                    null,
                    GreenGrocerProductCaseModes.AverageSourceDirect,
                    null,
                    null,
                    null,
                    null,
                    GreenGrocerProductCaseModes.ConfidenceMedium,
                    profile.RequiresManualApproval,
                    profile.AllowOrderLinking,
                    true),

            GreenGrocerProductCaseModes.ConversionManualOnly =>
                ResolveManualOnly(stockInfo, inputQuantity, inputMode, conversionMode, profile, errors),

            GreenGrocerProductCaseModes.ConversionBlocked =>
                ResolveBlocked(stockInfo, inputQuantity, inputMode, conversionMode, profile, errors),

            _ => throw new ArgumentException($"Unsupported conversion mode: {profile.ConversionMode}")
        };

        return result with
        {
            OverDeliveryTolerancePercent = Round(profile.OverDeliveryTolerancePercent),
            SevereOverDeliveryMultiplier = ResolveSevereOverDeliveryMultiplier()
        };
    }

    private async Task<GreenGrocerProductCaseResolutionDto> ResolveAutomaticallyAsync(
        StockInfo stockInfo,
        double inputQuantity,
        DateTime orderDate,
        List<string> warnings,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (stockInfo.ModelCode == "23")
        {
            warnings.Add("Model 23 sarf/kasa malzemesidir; manav urun siparisinden ayri degerlendirilmelidir.");
            return CreateResolution(
                stockInfo,
                inputQuantity,
                GreenGrocerProductCaseModes.InputSarf,
                GreenGrocerProductCaseModes.ConversionDirectQuantity,
                NormalizeMicroUnit(stockInfo.Unit1),
                inputQuantity,
                null,
                null,
                GreenGrocerProductCaseModes.AverageSourceDirect,
                null,
                null,
                null,
                null,
                GreenGrocerProductCaseModes.ConfidenceMedium,
                false,
                false,
                true);
        }

        if (IsUnit(stockInfo.Unit1, "ADET"))
        {
            var unitFactor = GetSecondaryUnitMultiplier(stockInfo);
            if (unitFactor > 1)
            {
                warnings.Add(
                    "ADET urunun Mikro koli katsayisi otomatik uygulanmadi; girilen miktar ADET kabul edildi. " +
                    "Koli girisi gerekiyorsa urune ozel profil tanimlayin.");
            }

            return CreateResolution(
                stockInfo,
                inputQuantity,
                GreenGrocerProductCaseModes.InputPiece,
                GreenGrocerProductCaseModes.ConversionDirectQuantity,
                "ADET",
                inputQuantity,
                null,
                null,
                GreenGrocerProductCaseModes.AverageSourceDirect,
                null,
                null,
                null,
                null,
                GreenGrocerProductCaseModes.ConfidenceHigh,
                false,
                true,
                true);
        }

        if (IsUnit(stockInfo.Unit1, "KG"))
        {
            var calculation = await EvaluateLabelAverageAsync(
                stockInfo.StockCode,
                orderDate,
                GreenGrocerCaseCalculationSettings.Default,
                cancellationToken);

            return BuildLabelAverageResolution(
                stockInfo,
                inputQuantity,
                GreenGrocerProductCaseModes.InputCase,
                GreenGrocerProductCaseModes.ConversionLabelAverageKgPerCase,
                true,
                false,
                calculation,
                warnings,
                errors);
        }

        errors.Add("Urun birimi manav kasa cozumleme icin desteklenmiyor.");
        return CreateResolution(
            stockInfo,
            inputQuantity,
            GreenGrocerProductCaseModes.InputCase,
            GreenGrocerProductCaseModes.ConversionManualOnly,
            NormalizeMicroUnit(stockInfo.Unit1),
            0,
            null,
            null,
            GreenGrocerProductCaseModes.AverageSourceNone,
            null,
            null,
            null,
            null,
            GreenGrocerProductCaseModes.ConfidenceBlocked,
            true,
            false,
            false);
    }

    private async Task<GreenGrocerCaseCalculationResult> EvaluateLabelAverageAsync(
        string stockCode,
        DateTime orderDate,
        GreenGrocerCaseCalculationSettings settings,
        CancellationToken cancellationToken)
    {
        var observations = await _labelStatsReader.ReadObservationsAsync(
            stockCode,
            orderDate,
            Math.Max(settings.FullWindowDays, DefaultAverageWindowDays),
            cancellationToken);

        return GreenGrocerCaseAverageCalculator.Evaluate(observations, orderDate, settings);
    }

    private GreenGrocerProductCaseResolutionDto BuildLabelAverageResolution(
        StockInfo stockInfo,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        bool allowOrderLinking,
        bool profileRequiresManualApproval,
        GreenGrocerCaseCalculationResult calculation,
        List<string> warnings,
        List<string> errors)
    {
        warnings.AddRange(calculation.Warnings);

        var requiresManualApproval = profileRequiresManualApproval || calculation.RequiresManualApproval;

        if (!calculation.IsUsable || calculation.AverageKgPerCase is null or <= 0d)
        {
            warnings.AddRange(calculation.Errors);
            warnings.Add(
                "Guncel kasa KG ortalamasi hesaplanamadi; girilen miktar Mikro ana biriminde dogrudan kullanildi.");

            return CreateResolution(
                stockInfo,
                inputQuantity,
                inputMode,
                GreenGrocerProductCaseModes.ConversionDirectQuantity,
                "KG",
                inputQuantity,
                null,
                null,
                GreenGrocerProductCaseModes.AverageSourceNone,
                calculation.RecordCount,
                calculation.CaseCount,
                calculation.CoefficientOfVariation,
                calculation.LatestLabelDate,
                GreenGrocerProductCaseModes.ConfidenceLow,
                false,
                false,
                true,
                calculation.CalculationTier,
                calculation.AverageWindowDaysUsed,
                calculation.LabelAgeDays,
                calculation.PriorAverageKgPerCase,
                calculation.DriftPercent,
                calculation.DriftDetected,
                calculation.AlternativeAverageKgPerCase);
        }

        errors.AddRange(calculation.Errors);

        return CreateResolution(
            stockInfo,
            inputQuantity,
            inputMode,
            conversionMode,
            "KG",
            inputQuantity * calculation.AverageKgPerCase.Value,
            calculation.AverageKgPerCase,
            null,
            GreenGrocerProductCaseModes.AverageSourceLabelHistory,
            calculation.RecordCount,
            calculation.CaseCount,
            calculation.CoefficientOfVariation,
            calculation.LatestLabelDate,
            calculation.Confidence,
            requiresManualApproval,
            allowOrderLinking,
            true,
            calculation.CalculationTier,
            calculation.AverageWindowDaysUsed,
            calculation.LabelAgeDays,
            calculation.PriorAverageKgPerCase,
            calculation.DriftPercent,
            calculation.DriftDetected,
            calculation.AlternativeAverageKgPerCase);
    }

    private GreenGrocerProductCaseResolutionDto ResolveManualKg(
        StockInfo stockInfo,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        GreenGrocerProductCaseProfile profile,
        List<string> warnings,
        List<string> errors)
    {
        if (profile.ManualKgPerCase is null or <= 0)
        {
            errors.Add("Manuel kg/kasa profili icin manualKgPerCase zorunludur.");
            return ResolveManualOnly(stockInfo, inputQuantity, inputMode, GreenGrocerProductCaseModes.ConversionManualOnly, profile, errors);
        }

        if (profile.MinExpectedKgPerCase.HasValue && profile.ManualKgPerCase.Value < profile.MinExpectedKgPerCase.Value)
        {
            warnings.Add("Manuel kg/kasa profil minimum beklentisinin altinda.");
        }

        if (profile.MaxExpectedKgPerCase.HasValue && profile.ManualKgPerCase.Value > profile.MaxExpectedKgPerCase.Value)
        {
            warnings.Add("Manuel kg/kasa profil maksimum beklentisinin ustunde.");
        }

        return CreateResolution(
            stockInfo,
            inputQuantity,
            inputMode,
            conversionMode,
            "KG",
            inputQuantity * profile.ManualKgPerCase.Value,
            profile.ManualKgPerCase,
            null,
            GreenGrocerProductCaseModes.AverageSourceManualProfile,
            null,
            null,
            null,
            null,
            GreenGrocerProductCaseModes.ConfidenceMedium,
            profile.RequiresManualApproval,
            profile.AllowOrderLinking,
            true);
    }

    private GreenGrocerProductCaseResolutionDto ResolveFixedUnits(
        StockInfo stockInfo,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        GreenGrocerProductCaseProfile profile,
        List<string> warnings,
        List<string> errors)
    {
        var unitsPerCase = profile.ManualUnitsPerCase ?? GetSecondaryUnitMultiplier(stockInfo);

        if (unitsPerCase <= 1)
        {
            errors.Add("Sabit koli/adet cevrimi icin manualUnitsPerCase veya Mikro birim2 katsayisi gereklidir.");
            return ResolveManualOnly(stockInfo, inputQuantity, inputMode, GreenGrocerProductCaseModes.ConversionManualOnly, profile, errors);
        }

        if (profile.ManualUnitsPerCase.HasValue)
        {
            warnings.Add("Koli/adet cevrimi manuel profil katsayisi ile hesaplandi.");
        }

        return CreateResolution(
            stockInfo,
            inputQuantity,
            inputMode,
            conversionMode,
            "ADET",
            inputQuantity * unitsPerCase,
            null,
            unitsPerCase,
            profile.ManualUnitsPerCase.HasValue
                ? GreenGrocerProductCaseModes.AverageSourceManualProfile
                : GreenGrocerProductCaseModes.AverageSourceStockUnitFactor,
            null,
            null,
            null,
            null,
            GreenGrocerProductCaseModes.ConfidenceHigh,
            profile.RequiresManualApproval,
            profile.AllowOrderLinking,
            true);
    }

    private GreenGrocerProductCaseResolutionDto ResolveManualOnly(
        StockInfo stockInfo,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        GreenGrocerProductCaseProfile profile,
        List<string> errors)
    {
        errors.Add("Bu urun manuel karar gerektiriyor; otomatik miktar hesaplanmadi.");
        return CreateResolution(
            stockInfo,
            inputQuantity,
            inputMode,
            conversionMode,
            NormalizeMicroUnit(stockInfo.Unit1),
            0,
            null,
            null,
            GreenGrocerProductCaseModes.AverageSourceNone,
            null,
            null,
            null,
            null,
            GreenGrocerProductCaseModes.ConfidenceBlocked,
            true,
            profile.AllowOrderLinking,
            false);
    }

    private GreenGrocerProductCaseResolutionDto ResolveBlocked(
        StockInfo stockInfo,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        GreenGrocerProductCaseProfile profile,
        List<string> errors)
    {
        errors.Add("Bu urun manav kasa siparisinde engelli.");
        return CreateResolution(
            stockInfo,
            inputQuantity,
            inputMode,
            conversionMode,
            NormalizeMicroUnit(stockInfo.Unit1),
            0,
            null,
            null,
            GreenGrocerProductCaseModes.AverageSourceNone,
            null,
            null,
            null,
            null,
            GreenGrocerProductCaseModes.ConfidenceBlocked,
            profile.RequiresManualApproval,
            false,
            false);
    }

    private GreenGrocerProductCaseResolutionDto CreateResolution(
        StockInfo stockInfo,
        double inputQuantity,
        string inputMode,
        string conversionMode,
        string microUnit,
        double estimatedQuantity,
        double? averageKgPerCase,
        double? unitsPerCase,
        string averageSource,
        int? averageRecordCount,
        int? averageCaseCount,
        double? coefficientOfVariation,
        DateTime? latestLabelDate,
        string confidence,
        bool requiresManualApproval,
        bool isOrderLinkable,
        bool isUsable,
        string? calculationTier = null,
        int? averageWindowDaysUsed = null,
        int? labelAgeDays = null,
        double? priorAverageKgPerCase = null,
        double? driftPercent = null,
        bool driftDetected = false,
        double? alternativeAverageKgPerCase = null) =>
        new(
            stockInfo.StockCode,
            stockInfo.StockName,
            stockInfo.ModelCode,
            GetModelName(stockInfo.ModelCode),
            stockInfo.Unit1,
            stockInfo.Unit2,
            Round(GetSecondaryUnitMultiplier(stockInfo)),
            Round(inputQuantity),
            inputMode,
            conversionMode,
            microUnit,
            Round(estimatedQuantity),
            RoundOrNull(averageKgPerCase),
            RoundOrNull(unitsPerCase),
            averageSource,
            averageRecordCount,
            averageCaseCount,
            RoundOrNull(coefficientOfVariation),
            latestLabelDate,
            confidence,
            requiresManualApproval && options.CurrentValue.RequireOrderUserManualApproval,
            isOrderLinkable &&
            options.CurrentValue.OrderLinkingEnabled,
            ResolveDefaultOverDeliveryTolerancePercent(),
            ResolveSevereOverDeliveryMultiplier(),
            isUsable,
            [],
            [],
            calculationTier,
            averageWindowDaysUsed,
            labelAgeDays,
            RoundOrNull(priorAverageKgPerCase),
            RoundPercentOrNull(driftPercent),
            driftDetected,
            RoundOrNull(alternativeAverageKgPerCase));

    private async Task<StockInfo> GetStockInfoAsync(string stockCode, CancellationToken cancellationToken)
    {
        var normalized = NormalizeStockCode(stockCode);
        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_kod.Trim() == normalized)
            .Select(item => new StockInfo(
                item.sto_kod.Trim(),
                (item.sto_kisa_ismi ?? item.sto_isim ?? string.Empty).Trim(),
                (item.sto_model_kodu ?? string.Empty).Trim(),
                (item.sto_birim1_ad ?? string.Empty).Trim(),
                (item.sto_birim2_ad ?? string.Empty).Trim(),
                item.sto_birim2_katsayi ?? 0d,
                item.sto_pasif_fl == true,
                (item.sto_kisa_ismi ?? string.Empty).StartsWith("DLS") ||
                (item.sto_isim ?? string.Empty).StartsWith("DLS")))
            .FirstOrDefaultAsync(cancellationToken);

        return stock ?? throw new KeyNotFoundException($"Stock card was not found: {normalized}");
    }

    private async Task<IReadOnlyDictionary<string, StockInfo>> GetStockInfosAsync(
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = stockCodes
            .Select(NormalizeStockCode)
            .Where(stockCode => stockCode.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, StockInfo>(StringComparer.OrdinalIgnoreCase);
        }

        var stocks = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => normalizedCodes.Contains(item.sto_kod.Trim()))
            .Select(item => new StockInfo(
                item.sto_kod.Trim(),
                (item.sto_kisa_ismi ?? item.sto_isim ?? string.Empty).Trim(),
                (item.sto_model_kodu ?? string.Empty).Trim(),
                (item.sto_birim1_ad ?? string.Empty).Trim(),
                (item.sto_birim2_ad ?? string.Empty).Trim(),
                item.sto_birim2_katsayi ?? 0d,
                item.sto_pasif_fl == true,
                (item.sto_kisa_ismi ?? string.Empty).StartsWith("DLS") ||
                (item.sto_isim ?? string.Empty).StartsWith("DLS")))
            .ToListAsync(cancellationToken);

        return stocks.ToDictionary(item => item.StockCode, StringComparer.OrdinalIgnoreCase);
    }

    private static GreenGrocerProductCaseProfileDto MapProfile(
        GreenGrocerProductCaseProfile profile,
        StockInfo? stockInfo)
    {
        stockInfo ??= new StockInfo(
            profile.StockCode,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0d,
            false,
            false);

        return new GreenGrocerProductCaseProfileDto(
            profile.Id,
            profile.StockCode,
            stockInfo.StockName,
            stockInfo.ModelCode,
            GetModelName(stockInfo.ModelCode),
            stockInfo.Unit1,
            stockInfo.Unit2,
            Round(GetSecondaryUnitMultiplier(stockInfo)),
            profile.IsActive,
            profile.InputMode,
            profile.ConversionMode,
            RoundOrNull(profile.ManualKgPerCase),
            RoundOrNull(profile.ManualUnitsPerCase),
            RoundOrNull(profile.MinExpectedKgPerCase),
            RoundOrNull(profile.MaxExpectedKgPerCase),
            profile.AverageWindowDays,
            profile.MinAverageRecordCount,
            profile.MinAverageCaseCount,
            Round(profile.MaxCoefficientOfVariation),
            profile.RequiresManualApproval,
            profile.AllowOrderLinking,
            Round(profile.OverDeliveryTolerancePercent),
            profile.Notes,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
    }

    private static void EnsureGreenGrocerStock(StockInfo stockInfo, bool activatingProfile)
    {
        if (!IsGreenGrocerModel(stockInfo.ModelCode))
        {
            throw new ArgumentException("Product case profile can only be saved for model codes 10, 11, 12 or 23.");
        }

        if (activatingProfile &&
            (stockInfo.IsPassive || stockInfo.IsDelisted))
        {
            throw new ArgumentException("Passive or DLS products can not have an active green grocer case profile.");
        }
    }

    private double ResolveDefaultOverDeliveryTolerancePercent()
    {
        var configured = options.CurrentValue.DefaultOverDeliveryTolerancePercent;
        return configured is >= 0d and <= 1000d
            ? Round(configured)
            : DefaultOverDeliveryTolerancePercent;
    }

    private int ResolveSourceWarehouseNo()
    {
        var configured = options.CurrentValue.SourceWarehouseNo;
        return configured > 0 ? configured : DefaultGreenGrocerWarehouseNo;
    }

    private double ResolveSevereOverDeliveryMultiplier()
    {
        var configured = options.CurrentValue.SevereOverDeliveryMultiplier;
        return configured > 1d ? Round(configured) : 1.5d;
    }

    private static string GetCanonicalMode(string mode, IReadOnlyCollection<string> validModes)
    {
        return validModes.FirstOrDefault(
            validMode => string.Equals(validMode, NormalizeMode(mode), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unsupported mode: {mode}");
    }

    private static void ValidateProfileRequest(SaveGreenGrocerProductCaseProfileRequest request)
    {
        if (request.AverageWindowDays is < 1 or > 365)
        {
            throw new ArgumentException("Average window days must be between 1 and 365.");
        }

        if (request.MinAverageRecordCount < 0 || request.MinAverageCaseCount < 0)
        {
            throw new ArgumentException("Average thresholds can not be negative.");
        }

        if (request.MaxCoefficientOfVariation is < 0 or > 10)
        {
            throw new ArgumentException("Maximum coefficient of variation must be between 0 and 10.");
        }

        if (request.OverDeliveryTolerancePercent is < 0 or > 1000)
        {
            throw new ArgumentException("Over delivery tolerance percent must be between 0 and 1000.");
        }

        if (request.ConversionMode == GreenGrocerProductCaseModes.ConversionManualKgPerCase &&
            request.ManualKgPerCase is null or <= 0)
        {
            throw new ArgumentException("manualKgPerCase is required for ManualKgPerCase conversion mode.");
        }

        if (request.ConversionMode == GreenGrocerProductCaseModes.ConversionFixedUnitsPerCase &&
            request.ManualUnitsPerCase is <= 0)
        {
            // Mikro unit2 factor can still be used, so this is not always an error.
            return;
        }
    }

    private static bool IsGreenGrocerModel(string modelCode) =>
        GreenGrocerModelCodes.Contains(modelCode, StringComparer.OrdinalIgnoreCase);

    private static bool IsUnit(string value, string expected) =>
        string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMicroUnit(string value) =>
        string.IsNullOrWhiteSpace(value) ? "ADET" : value.Trim().ToUpperInvariant();

    private static string NormalizeStockCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Stock code is required.", nameof(value));
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > 25)
        {
            throw new ArgumentException("Stock code can not exceed 25 characters.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeSearch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeMode(string value) => value.Trim();

    private static int NormalizeTake(int take)
    {
        if (take <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(take, MaxTake);
    }

    private static string GetModelName(string modelCode) =>
        modelCode switch
        {
            "10" => "Meyve",
            "11" => "Sebze",
            "12" => "Yesillik",
            "23" => "Manav Sarf",
            _ => string.Empty
        };

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static double? RoundOrNull(double? value) => value.HasValue ? Round(value.Value) : null;

    private static double? RoundPercentOrNull(double? value) =>
        value.HasValue ? Math.Round(value.Value, 1, MidpointRounding.AwayFromZero) : null;

    private static double GetSecondaryUnitMultiplier(StockInfo stockInfo) =>
        string.IsNullOrWhiteSpace(stockInfo.Unit2)
            ? 0d
            : UnitMultiplierNormalizer.Normalize(stockInfo.Unit2Factor);

    private sealed record StockInfo(
        string StockCode,
        string StockName,
        string ModelCode,
        string Unit1,
        string Unit2,
        double Unit2Factor,
        bool IsPassive,
        bool IsDelisted);
}
