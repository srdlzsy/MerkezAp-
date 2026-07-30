using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ResolveBarcode;

public sealed class ResolveBarcodeUseCase(MikroDbContext mikroDbContext) : IResolveBarcodeUseCase
{
    public async Task<BarcodeResolutionDto> ExecuteAsync(
        BarcodeResolutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var rawBarcode = NormalizeOrNull(request.Barcode)
            ?? throw new ArgumentException("Barcode is required.", nameof(request.Barcode));
        var lookup = BarcodeLookupNormalizer.Normalize(rawBarcode);
        var screenCode = NormalizeOrNull(request.ScreenCode);
        var operationType = NormalizeOperationType(request.OperationType) ?? NormalizeOperationType(screenCode);
        var supplierCode = NormalizeOrNull(request.SupplierCode);
        var targetWarehouseNo = request.TargetWarehouseNo is > 0 ? request.TargetWarehouseNo : null;
        var warnings = new List<string>();
        var errors = new List<string>();

        if (request.TargetWarehouseNo is <= 0)
        {
            warnings.Add("Target warehouse no must be greater than zero. Target warehouse check was skipped.");
        }

        if (lookup.IsBarcodeCheckDigitInvalid())
        {
            warnings.Add("EAN-13 check digit appears invalid.");
        }

        var barcodeMatch = await FindBarcodeRowAsync(lookup.LookupBarcode, cancellationToken);
        if (barcodeMatch is null && !string.Equals(lookup.LookupBarcode, lookup.OriginalBarcode, StringComparison.Ordinal))
        {
            barcodeMatch = await FindBarcodeRowAsync(lookup.OriginalBarcode, cancellationToken);
        }

        string? stockCode = null;
        string? resolutionSource = null;
        var matchedUnitPointer = 1;
        string? matchedBarcode = null;

        if (barcodeMatch is not null)
        {
            stockCode = NormalizeOrNull(barcodeMatch.StockCode);
            matchedUnitPointer = Math.Max((int)barcodeMatch.UnitPointer.GetValueOrDefault(1), 1);
            matchedBarcode = barcodeMatch.Barcode;
            resolutionSource = lookup.IsVariableWeightBarcode &&
                               string.Equals(barcodeMatch.Barcode, lookup.LookupBarcode, StringComparison.Ordinal)
                ? "variable-weight"
                : "barcode";
        }
        else
        {
            var stockSeed = await FindStockSeedAsync(lookup, cancellationToken);
            if (stockSeed is null)
            {
                errors.Add("Barkod veya stok referansi sistemde bulunamadi.");
                return CreateMissingResponse(
                    lookup,
                    request.WarehouseNo,
                    screenCode,
                    operationType,
                    targetWarehouseNo,
                    supplierCode,
                    "not-found",
                    null,
                    null,
                    null,
                    "Barkod veya stok referansi sistemde bulunamadi.",
                    warnings,
                    errors);
            }

            stockCode = stockSeed.StockCode;
            resolutionSource = stockSeed.Source;
            matchedBarcode = resolutionSource == "stock-code" ? null : lookup.LookupBarcode;
        }

        if (string.IsNullOrWhiteSpace(stockCode))
        {
            errors.Add("Barkod kaydi bulundu ancak bagli stok kodu bos geldi.");
            return CreateMissingResponse(
                lookup,
                request.WarehouseNo,
                screenCode,
                operationType,
                targetWarehouseNo,
                supplierCode,
                resolutionSource,
                null,
                matchedBarcode,
                matchedUnitPointer,
                "Barkod kaydi bulundu ancak bagli stok kodu bos geldi.",
                warnings,
                errors);
        }

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_kod == stockCode)
            .Select(item => new StockSnapshot(
                item.sto_kod,
                item.sto_isim,
                item.sto_kuresel_urun_numarasi,
                item.sto_sat_cari_kod,
                item.sto_model_kodu,
                item.sto_birim1_ad,
                item.sto_birim1_katsayi,
                item.sto_birim2_ad,
                item.sto_birim2_katsayi,
                item.sto_birim3_ad,
                item.sto_birim3_katsayi,
                item.sto_birim4_ad,
                item.sto_birim4_katsayi,
                item.sto_satis_dursun,
                item.sto_siparis_dursun,
                item.sto_malkabul_dursun,
                item.sto_pasif_fl))
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            errors.Add("Stok karti bulunamadi.");
            return CreateMissingResponse(
                lookup,
                request.WarehouseNo,
                screenCode,
                operationType,
                targetWarehouseNo,
                supplierCode,
                resolutionSource,
                stockCode,
                matchedBarcode,
                matchedUnitPointer,
                "Stok karti bulunamadi.",
                warnings,
                errors);
        }

        var warehouseDetail = await mikroDbContext.STOK_DEPO_DETAYLARIs
            .AsNoTracking()
            .Where(item => item.sdp_depo_kod == stockCode && item.sdp_depo_no == request.WarehouseNo)
            .Select(item => new WarehouseDetailSnapshot(
                item.sdp_satisdursun,
                item.sdp_sipdursun,
                item.sdp_malkabuldursun,
                item.sdp_Pasif_fl,
                item.sdp_sat_cari_kod,
                item.sdp_UrunSorumlusuKodu))
            .FirstOrDefaultAsync(cancellationToken);

        var productBarcodes = await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(row =>
                row.bar_iptal != true &&
                row.bar_stokkodu == stockCode &&
                row.bar_kodu != null &&
                row.bar_kodu != string.Empty)
            .Select(row => new BarcodeRow(
                row.bar_kodu ?? string.Empty,
                row.bar_stokkodu,
                row.bar_birimpntr,
                row.bar_master ?? false,
                row.bar_icerigi))
            .ToListAsync(cancellationToken);

        var primaryBarcode = productBarcodes
            .Where(row => row.UnitPointer.GetValueOrDefault(1) == 1)
            .OrderByDescending(row => row.IsMaster)
            .ThenBy(row => row.Barcode, StringComparer.Ordinal)
            .FirstOrDefault()
            ?.Barcode
            ?? productBarcodes.FirstOrDefault()?.Barcode
            ?? stock.GlobalTradeItemNo;

        var caseBarcodeRow = productBarcodes
            .OrderByDescending(row => row.IsMaster)
            .ThenByDescending(row => GetUnitMultiplier(stock, row.UnitPointer))
            .FirstOrDefault(row => row.IsMaster || GetUnitMultiplier(stock, row.UnitPointer) > 1d);
        var caseBarcode = caseBarcodeRow?.Barcode;
        var unitsPerCase = caseBarcodeRow is null ? null : GetUnitMultiplier(stock, caseBarcodeRow.UnitPointer);
        var matchedUnitName = GetUnitName(stock, matchedUnitPointer);
        var matchedUnitMultiplier = GetUnitMultiplier(stock, matchedUnitPointer);
        var isCaseBarcode = barcodeMatch is not null &&
                            (barcodeMatch.IsMaster || matchedUnitMultiplier.GetValueOrDefault(1d) > 1d);
        var isPrimaryBarcode = barcodeMatch is not null &&
                               !isCaseBarcode &&
                               string.Equals(barcodeMatch.Barcode, primaryBarcode, StringComparison.Ordinal);
        var isAlternativeBarcode = barcodeMatch is not null &&
                                   !isCaseBarcode &&
                                   !isPrimaryBarcode &&
                                   !lookup.IsVariableWeightBarcode;
        var barcodeKind = ResolveBarcodeKind(
            lookup,
            resolutionSource,
            isPrimaryBarcode,
            isCaseBarcode,
            isAlternativeBarcode);

        var defaultSupplierCode = NormalizeOrNull(warehouseDetail?.SupplierCode)
                                  ?? NormalizeOrNull(stock.DefaultSupplierCode);
        var defaultSupplierName = defaultSupplierCode is null
            ? null
            : await mikroDbContext.CARI_HESAPLARs
                .AsNoTracking()
                .Where(customer => customer.cari_kod == defaultSupplierCode)
                .Select(customer => customer.cari_unvan1)
                .FirstOrDefaultAsync(cancellationToken);

        var targetWarehouse = await EvaluateTargetWarehouseAsync(
            targetWarehouseNo,
            stock.ModelCode,
            cancellationToken);
        AddTargetWarnings(targetWarehouse, warnings);

        var shouldEnforceTargetWarehouse = BarcodeResolutionOperationRules.ShouldEnforceTargetWarehouse(operationType);
        var shouldCheckPurchaseRequirement = BarcodeResolutionOperationRules.ShouldCheckPurchaseRequirement(
            operationType,
            supplierCode);
        var shouldEnforcePurchaseRequirement = BarcodeResolutionOperationRules.ShouldEnforcePurchaseRequirement(operationType);
        var hasPurchaseRequirement = await EvaluatePurchaseRequirementAsync(
            stock.StockCode,
            supplierCode,
            shouldCheckPurchaseRequirement,
            cancellationToken);

        var purchaseRequirementReason = hasPurchaseRequirement switch
        {
            true when supplierCode is not null => "Secili tedarikci icin satinalma sarti bulundu.",
            true => "Urun icin satinalma sarti bulundu.",
            false when supplierCode is not null => "Secili tedarikci icin satinalma sarti bulunamadi.",
            false => "Urun icin satinalma sarti bulunamadi.",
            _ => null
        };

        var price = await FindSalesPriceAsync(
            stock.StockCode,
            request.WarehouseNo,
            matchedUnitPointer,
            cancellationToken);

        var salesBlockCode = warehouseDetail?.SalesBlockCode ?? stock.SalesBlockCode;
        var orderBlockCode = warehouseDetail?.OrderBlockCode ?? stock.OrderBlockCode;
        var goodsAcceptanceBlockCode = warehouseDetail?.GoodsAcceptanceBlockCode ?? stock.GoodsAcceptanceBlockCode;
        var isSalesBlocked = IsBlocked(salesBlockCode);
        var isOrderBlocked = IsBlocked(orderBlockCode);
        var isGoodsAcceptanceBlocked = IsBlocked(goodsAcceptanceBlockCode);
        var isPassive = warehouseDetail?.IsPassive ?? stock.IsPassive.GetValueOrDefault();
        var isBlocked = isPassive || isSalesBlocked || isOrderBlocked || isGoodsAcceptanceBlocked;
        var isExcludedForNonRefund = request.IsRefund == false &&
                                     string.Equals(NormalizeOrNull(stock.ModelCode), "99", StringComparison.Ordinal) &&
                                     StartsWithDls(stock.StockName);

        var screenUsability = EvaluateScreenUsability(
            screenCode,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked,
            isPassive);
        var operationDecision = EvaluateOperationUsability(
            operationType,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked,
            isPassive,
            shouldEnforceTargetWarehouse ? targetWarehouse.IsAllowed : null,
            hasPurchaseRequirement,
            shouldEnforcePurchaseRequirement,
            isExcludedForNonRefund);

        if (!operationDecision.IsUsable)
        {
            errors.Add(operationDecision.Reason);
        }

        return CreateResponse(
            true,
            lookup,
            request.WarehouseNo,
            screenCode,
            operationType,
            targetWarehouseNo,
            supplierCode,
            resolutionSource,
            stock.StockCode,
            stock.StockName,
            matchedBarcode,
            primaryBarcode,
            caseBarcode,
            unitsPerCase,
            matchedUnitPointer,
            matchedUnitName,
            matchedUnitMultiplier,
            barcodeKind,
            isBlocked,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked,
            isPassive,
            screenUsability.IsUsable,
            isPrimaryBarcode,
            isCaseBarcode,
            isCaseBarcode ? matchedUnitMultiplier : null,
            isAlternativeBarcode,
            screenUsability.Reason,
            defaultSupplierCode,
            NormalizeOrNull(defaultSupplierName),
            targetWarehouse.IsAllowed,
            targetWarehouse.Reason,
            NormalizeOrNull(stock.ModelCode),
            targetWarehouse.ModelCodes,
            hasPurchaseRequirement,
            purchaseRequirementReason,
            price?.Price,
            price?.PriceTypeCode,
            operationDecision.IsUsable,
            operationDecision.Reason,
            warnings,
            errors);
    }

    private async Task<BarcodeRow?> FindBarcodeRowAsync(
        string barcode,
        CancellationToken cancellationToken) =>
        await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(row => row.bar_iptal != true && row.bar_kodu == barcode)
            .Select(row => new BarcodeRow(
                row.bar_kodu ?? string.Empty,
                row.bar_stokkodu,
                row.bar_birimpntr,
                row.bar_master ?? false,
                row.bar_icerigi))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<StockSeed?> FindStockSeedAsync(
        BarcodeLookupInfo lookup,
        CancellationToken cancellationToken) =>
        await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock =>
                stock.sto_kod == lookup.LookupBarcode ||
                stock.sto_kuresel_urun_numarasi == lookup.LookupBarcode ||
                stock.sto_kod == lookup.OriginalBarcode ||
                stock.sto_kuresel_urun_numarasi == lookup.OriginalBarcode)
            .Select(stock => new StockSeed(
                stock.sto_kod,
                stock.sto_kod == lookup.LookupBarcode || stock.sto_kod == lookup.OriginalBarcode
                    ? "stock-code"
                    : "gtin"))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<TargetWarehouseEvaluation> EvaluateTargetWarehouseAsync(
        int? targetWarehouseNo,
        string? productModelCode,
        CancellationToken cancellationToken)
    {
        if (!targetWarehouseNo.HasValue)
        {
            return TargetWarehouseEvaluation.NotRequested;
        }

        var modelCodeText = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no == targetWarehouseNo.Value)
            .Select(warehouse => warehouse.dep_barkod_yazici_yolu)
            .FirstOrDefaultAsync(cancellationToken);

        if (modelCodeText is null)
        {
            return new TargetWarehouseEvaluation(
                false,
                "Hedef depo bulunamadi veya model kod listesi okunamadi.",
                Array.Empty<string>());
        }

        var modelCodes = ParseModelCodes(modelCodeText);
        if (modelCodes.Count == 0)
        {
            return new TargetWarehouseEvaluation(
                null,
                "Hedef depo icin model kod listesi tanimli degil; hedef depo uygunlugu kontrol edilemedi.",
                modelCodes);
        }

        var normalizedProductModelCode = NormalizeOrNull(productModelCode);
        if (normalizedProductModelCode is null)
        {
            return new TargetWarehouseEvaluation(
                false,
                "Urun model kodu bos oldugu icin hedef depo uygunlugu saglanamadi.",
                modelCodes);
        }

        var isAllowed = modelCodes.Contains(normalizedProductModelCode, StringComparer.OrdinalIgnoreCase);
        return new TargetWarehouseEvaluation(
            isAllowed,
            isAllowed
                ? "Urun hedef deponun izinli model kodlari icinde."
                : "Urun hedef deponun izinli model kodlari icinde degil.",
            modelCodes);
    }

    private async Task<bool?> EvaluatePurchaseRequirementAsync(
        string stockCode,
        string? supplierCode,
        bool forceAnySupplierCheck,
        CancellationToken cancellationToken)
    {
        if (supplierCode is null && !forceAnySupplierCheck)
        {
            return null;
        }

        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM dbo.SATINALMA_SARTLARI WITH (NOLOCK)
                    WHERE sas_stok_kod = @stockCode
                      AND (@supplierCode IS NULL OR sas_cari_kod = @supplierCode)
                )
                THEN 1 ELSE 0 END;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 60;

            AddParameter(command, "@stockCode", stockCode, DbType.String);
            AddParameter(command, "@supplierCode", supplierCode, DbType.String);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && result is not DBNull && Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<PriceSnapshot?> FindSalesPriceAsync(
        string stockCode,
        int warehouseNo,
        int unitPointer,
        CancellationToken cancellationToken) =>
        await mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs
            .AsNoTracking()
            .Where(price =>
                price.sfiyat_stokkod == stockCode &&
                price.sfiyat_deposirano == warehouseNo &&
                price.sfiyat_fiyati != null &&
                (price.sfiyat_birim_pntr == unitPointer ||
                 price.sfiyat_birim_pntr == 1 ||
                 price.sfiyat_birim_pntr == null))
            .OrderBy(price => price.sfiyat_birim_pntr == unitPointer ? 0 : 1)
            .ThenBy(price => price.sfiyat_listesirano ?? int.MaxValue)
            .ThenByDescending(price => price.sfiyat_lastup_date ?? price.sfiyat_create_date)
            .Select(price => new PriceSnapshot(
                price.sfiyat_fiyati,
                price.sfiyat_listesirano))
            .FirstOrDefaultAsync(cancellationToken);

    private static BarcodeResolutionDto CreateMissingResponse(
        BarcodeLookupInfo lookup,
        int warehouseNo,
        string? screenCode,
        string? operationType,
        int? targetWarehouseNo,
        string? supplierCode,
        string? resolutionSource,
        string? stockCode,
        string? matchedBarcode,
        int? matchedUnitPointer,
        string reason,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<string> errors) =>
        CreateResponse(
            isFound: false,
            lookup: lookup,
            warehouseNo: warehouseNo,
            screenCode: screenCode,
            operationType: operationType,
            targetWarehouseNo: targetWarehouseNo,
            supplierCode: supplierCode,
            resolutionSource: resolutionSource,
            stockCode: stockCode,
            stockName: null,
            matchedBarcode: matchedBarcode,
            primaryBarcode: null,
            caseBarcode: null,
            unitsPerCase: null,
            matchedUnitPointer: matchedUnitPointer,
            matchedUnitName: null,
            matchedUnitMultiplier: null,
            barcodeKind: resolutionSource,
            isBlocked: false,
            isSalesBlocked: false,
            isOrderBlocked: false,
            isGoodsAcceptanceBlocked: false,
            isPassive: false,
            isUsableInScreen: false,
            isPrimaryBarcode: false,
            isCaseBarcode: false,
            matchedUnitsPerCase: null,
            isAlternativeBarcode: false,
            usabilityReason: reason,
            defaultSupplierCode: null,
            defaultSupplierName: null,
            isAllowedForTargetWarehouse: null,
            targetWarehouseReason: null,
            productModelCode: null,
            targetWarehouseModelCodes: null,
            hasPurchaseRequirement: null,
            purchaseRequirementReason: null,
            salesPrice: null,
            priceTypeCode: null,
            isUsableInOperation: false,
            operationDecision: reason,
            warnings: warnings,
            errors: errors);

    private static BarcodeResolutionDto CreateResponse(
        bool isFound,
        BarcodeLookupInfo lookup,
        int warehouseNo,
        string? screenCode,
        string? operationType,
        int? targetWarehouseNo,
        string? supplierCode,
        string? resolutionSource,
        string? stockCode,
        string? stockName,
        string? matchedBarcode,
        string? primaryBarcode,
        string? caseBarcode,
        double? unitsPerCase,
        int? matchedUnitPointer,
        string? matchedUnitName,
        double? matchedUnitMultiplier,
        string? barcodeKind,
        bool isBlocked,
        bool isSalesBlocked,
        bool isOrderBlocked,
        bool isGoodsAcceptanceBlocked,
        bool isPassive,
        bool isUsableInScreen,
        bool isPrimaryBarcode,
        bool isCaseBarcode,
        double? matchedUnitsPerCase,
        bool isAlternativeBarcode,
        string? usabilityReason,
        string? defaultSupplierCode,
        string? defaultSupplierName,
        bool? isAllowedForTargetWarehouse,
        string? targetWarehouseReason,
        string? productModelCode,
        IReadOnlyCollection<string>? targetWarehouseModelCodes,
        bool? hasPurchaseRequirement,
        string? purchaseRequirementReason,
        double? salesPrice,
        int? priceTypeCode,
        bool isUsableInOperation,
        string? operationDecision,
        IReadOnlyCollection<string> warnings,
        IReadOnlyCollection<string> errors) =>
        new(
            isFound,
            lookup.OriginalBarcode,
            warehouseNo,
            screenCode,
            resolutionSource,
            stockCode,
            stockName,
            matchedBarcode,
            primaryBarcode,
            caseBarcode,
            unitsPerCase,
            matchedUnitPointer,
            matchedUnitName,
            matchedUnitMultiplier,
            isBlocked,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked,
            isUsableInScreen,
            usabilityReason,
            defaultSupplierCode,
            defaultSupplierName,
            lookup.LookupBarcode,
            lookup.IsVariableWeightBarcode,
            lookup.EmbeddedQuantity,
            lookup.EmbeddedQuantityUnit,
            lookup.IsCheckDigitValid,
            barcodeKind,
            isPrimaryBarcode,
            isCaseBarcode,
            isAlternativeBarcode,
            matchedUnitsPerCase,
            operationType,
            targetWarehouseNo,
            isAllowedForTargetWarehouse,
            targetWarehouseReason,
            productModelCode,
            targetWarehouseModelCodes,
            supplierCode,
            hasPurchaseRequirement,
            purchaseRequirementReason,
            salesPrice,
            priceTypeCode,
            isPassive,
            isUsableInOperation,
            operationDecision,
            warnings,
            errors);

    private static OperationEvaluation EvaluateScreenUsability(
        string? screenCode,
        bool isSalesBlocked,
        bool isOrderBlocked,
        bool isGoodsAcceptanceBlocked,
        bool isPassive)
    {
        var normalizedScreenCode = NormalizeOperationType(screenCode);

        if (normalizedScreenCode is null)
        {
            return new OperationEvaluation(true, "Ekran baglami verilmedigi icin sadece blok bilgisi donduruldu.");
        }

        return EvaluateOperationUsability(
            normalizedScreenCode,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked,
            isPassive,
            null,
            null,
            false,
            false);
    }

    private static OperationEvaluation EvaluateOperationUsability(
        string? operationType,
        bool isSalesBlocked,
        bool isOrderBlocked,
        bool isGoodsAcceptanceBlocked,
        bool isPassive,
        bool? isAllowedForTargetWarehouse,
        bool? hasPurchaseRequirement,
        bool shouldEnforcePurchaseRequirement,
        bool isExcludedForNonRefund)
    {
        if (isPassive)
        {
            return new OperationEvaluation(false, "Urun pasif oldugu icin islemde kullanilamaz.");
        }

        if (isAllowedForTargetWarehouse == false)
        {
            return new OperationEvaluation(false, "Urun hedef depo icin uygun degil.");
        }

        if (shouldEnforcePurchaseRequirement && hasPurchaseRequirement == false)
        {
            return new OperationEvaluation(false, "Urun icin gerekli satinalma sarti bulunamadi.");
        }

        if (isExcludedForNonRefund)
        {
            return new OperationEvaluation(false, "DLS/99 urunler iade olmayan islemde kullanilamaz.");
        }

        return operationType switch
        {
            "receiving" => isGoodsAcceptanceBlocked
                ? new OperationEvaluation(false, "Urun mal kabul icin bloklu.")
                : new OperationEvaluation(true, "Urun mal kabul isleminde kullanilabilir."),

            "count" => new OperationEvaluation(true, "Urun sayim isleminde kullanilabilir."),

            "order" => isOrderBlocked
                ? new OperationEvaluation(false, "Urun siparis icin bloklu.")
                : new OperationEvaluation(true, "Urun siparis isleminde kullanilabilir."),

            "shipment" or "return" or "waste" => isSalesBlocked
                ? new OperationEvaluation(false, "Urun stok cikis, sevk, iade veya fire icin bloklu.")
                : new OperationEvaluation(true, "Urun stok cikis, sevk, iade veya fire isleminde kullanilabilir."),

            null => new OperationEvaluation(true, "Islem tipi verilmedigi icin genel blok bilgisi donduruldu."),

            _ => new OperationEvaluation(true, "Bu islem tipi icin ozel kullanilabilirlik kurali tanimli degil.")
        };
    }

    private static string? NormalizeOperationType(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();
        return normalized switch
        {
            "depo-mal-kabul" or "depo-mal-kabulleri" or "firma-mal-kabul" or "firma-mal-kabulleri" or
            "mal-kabul" or "mal-kabulleri" or "receiving" or "goods-receiving" => "receiving",

            "sayim" or "sayim-sonuclari" or "sayim-sonucu" or "count" or "inventory-count" => "count",

            "verilen-depo-siparisleri" or "verilen-firma-siparisleri" or "siparis" or "siparisler" or
            "order" or "purchase-order" or "warehouse-order" => "order",

            "giden-firma-sevkleri" or "giden-depolar-arasi-sevkler" or "sevk" or "sevkler" or
            "shipment" or "dispatch" => "shipment",

            "firma-iadeleri" or "giden-depo-iadeleri" or "iade" or "iadeler" or "return" => "return",

            "zayiat" or "zayiat-fisleri" or "fire" or "waste" or "masraf" or "masraf-fisleri" => "waste",

            _ => normalized
        };
    }

    private static string ResolveBarcodeKind(
        BarcodeLookupInfo lookup,
        string? resolutionSource,
        bool isPrimaryBarcode,
        bool isCaseBarcode,
        bool isAlternativeBarcode)
    {
        if (lookup.IsVariableWeightBarcode)
        {
            return "variable-weight";
        }

        if (isCaseBarcode)
        {
            return "case";
        }

        if (isPrimaryBarcode)
        {
            return "product";
        }

        if (isAlternativeBarcode)
        {
            return "alternative";
        }

        return resolutionSource ?? "unknown";
    }

    private static void AddTargetWarnings(
        TargetWarehouseEvaluation targetWarehouse,
        ICollection<string> warnings)
    {
        if (targetWarehouse.IsAllowed is null && targetWarehouse.Reason is not null)
        {
            warnings.Add(targetWarehouse.Reason);
        }
    }

    private static IReadOnlyCollection<string> ParseModelCodes(string value) =>
        value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool StartsWithDls(string? value) =>
        NormalizeOrNull(value)?.StartsWith("DLS", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsBlocked(byte? value) =>
        value.GetValueOrDefault() != 0;

    private static string? GetUnitName(StockSnapshot stock, int? unitPointer) =>
        Math.Max(unitPointer.GetValueOrDefault(1), 1) switch
        {
            1 => NormalizeOrNull(stock.Unit1Name),
            2 => NormalizeOrNull(stock.Unit2Name),
            3 => NormalizeOrNull(stock.Unit3Name),
            4 => NormalizeOrNull(stock.Unit4Name),
            _ => NormalizeOrNull(stock.Unit1Name)
        };

    private static double? GetUnitMultiplier(StockSnapshot stock, int? unitPointer) =>
        Math.Max(unitPointer.GetValueOrDefault(1), 1) switch
        {
            1 => NormalizeMultiplier(stock.Unit1Multiplier),
            2 => NormalizeMultiplier(stock.Unit2Multiplier),
            3 => NormalizeMultiplier(stock.Unit3Multiplier),
            4 => NormalizeMultiplier(stock.Unit4Multiplier),
            _ => NormalizeMultiplier(stock.Unit1Multiplier)
        };

    private static double? NormalizeMultiplier(double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value <= 0d ? 1d : value.Value;
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record BarcodeRow(
        string Barcode,
        string? StockCode,
        byte? UnitPointer,
        bool IsMaster,
        byte? ContentCode);

    private sealed record StockSeed(
        string StockCode,
        string Source);

    private sealed record StockSnapshot(
        string StockCode,
        string? StockName,
        string? GlobalTradeItemNo,
        string? DefaultSupplierCode,
        string? ModelCode,
        string? Unit1Name,
        double? Unit1Multiplier,
        string? Unit2Name,
        double? Unit2Multiplier,
        string? Unit3Name,
        double? Unit3Multiplier,
        string? Unit4Name,
        double? Unit4Multiplier,
        byte? SalesBlockCode,
        byte? OrderBlockCode,
        byte? GoodsAcceptanceBlockCode,
        bool? IsPassive);

    private sealed record WarehouseDetailSnapshot(
        byte? SalesBlockCode,
        byte? OrderBlockCode,
        byte? GoodsAcceptanceBlockCode,
        bool? IsPassive,
        string? SupplierCode,
        string? ProductManagerCode);

    private sealed record TargetWarehouseEvaluation(
        bool? IsAllowed,
        string? Reason,
        IReadOnlyCollection<string>? ModelCodes)
    {
        public static TargetWarehouseEvaluation NotRequested { get; } = new(null, null, null);
    }

    private sealed record PriceSnapshot(
        double? Price,
        int? PriceTypeCode);

    private sealed record OperationEvaluation(
        bool IsUsable,
        string Reason);
}

file static class BarcodeLookupInfoExtensions
{
    public static bool IsBarcodeCheckDigitInvalid(this BarcodeLookupInfo lookup) =>
        lookup.IsCheckDigitValid == false;
}

internal static class BarcodeResolutionOperationRules
{
    internal static bool ShouldEnforceTargetWarehouse(string? operationType) =>
        !string.Equals(operationType, "shipment", StringComparison.Ordinal);

    internal static bool ShouldCheckPurchaseRequirement(string? operationType, string? supplierCode) =>
        supplierCode is not null || operationType is "receiving" or "order";

    internal static bool ShouldEnforcePurchaseRequirement(string? operationType) =>
        operationType is "receiving" or "order";
}
