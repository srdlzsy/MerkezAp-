using System.ServiceModel;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AxataMain = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main;
using AxataSku = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Main.WMSServiceCore.Models.SKUMaster;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataProductSynchronizationService(
    MikroDbContext mikroDbContext,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : IAxataProductSynchronizationService
{
    private const string CompanyCode = "01";
    private const string OperationName = "addSKUMaster";
    private const string DefaultUnitCode = "AD";
    private const int DefaultTake = 100;
    private const int MaxTake = 100000;
    private const int DispatchBatchSize = 100;

    public async Task<AxataProductSynchronizationPreviewDto> PreviewAsync(
        string? productCode,
        int? take,
        CancellationToken cancellationToken)
    {
        var normalizedProductCode = NormalizeCode(productCode);
        var products = await ReadProductsAsync(
            string.IsNullOrWhiteSpace(normalizedProductCode) ? [] : [normalizedProductCode],
            NormalizeTake(take),
            cancellationToken);

        return new AxataProductSynchronizationPreviewDto(
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(normalizedProductCode) ? null : normalizedProductCode,
            await CountProductsAsync(normalizedProductCode, cancellationToken),
            products.Count,
            products.Select(ToPreviewItem).ToArray(),
            [
                "Mikro'daki aktif stok, tum gecerli barkodlar ve tanimli birimler AXATA addSKUMaster paketine donusturuldu.",
                "Bu endpoint veri yazmaz. Canli aktarim icin products/dispatch veya products/{productCode}/dispatch kullanilir."
            ]);
    }

    public async Task<AxataProductSynchronizationExecuteDto> DispatchAsync(
        AxataProductSynchronizationDispatchRequest request,
        CancellationToken cancellationToken)
    {
        var requestedCodes = request.ProductCodes
            .Select(NormalizeCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var products = await ReadProductsAsync(
            requestedCodes,
            requestedCodes.Length > 0 ? requestedCodes.Length : NormalizeTake(request.Take),
            cancellationToken);

        if (requestedCodes.Length > 0)
        {
            var foundCodes = products
                .Select(product => product.ProductCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingCode = requestedCodes.FirstOrDefault(code => !foundCodes.Contains(code));
            if (missingCode is not null)
            {
                throw new KeyNotFoundException(
                    $"Mikro'da aktif ve AXATA'ya uygun urun bulunamadi: {missingCode}.");
            }
        }

        if (products.Count == 0)
        {
            throw new KeyNotFoundException("AXATA'ya gonderilecek uygun Mikro urunu bulunamadi.");
        }

        var configuration = GetRequiredConfiguration();
        var results = new List<AxataProductSynchronizationResultDto>(products.Count);

        foreach (var batch in products.Chunk(DispatchBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AxataMain.AxataServicePoolClient? client = null;

            try
            {
                client = CreateClient(configuration.EndpointUrl);
                var response = await client
                    .addSKUMasterAsync(
                        new AxataMain.addSKUMaster_Req(
                            configuration.Username,
                            configuration.Password,
                            batch.Select(ToWcfProduct).ToArray()))
                    .WaitAsync(cancellationToken);
                CloseClient(client);
                client = null;

                var isSuccess = response.state == 0;
                foreach (var product in batch)
                {
                    var processResult = response.processResult?.FirstOrDefault(item =>
                        string.Equals(
                            NormalizeCode(item.EntityCode),
                            product.ProductCode,
                            StringComparison.OrdinalIgnoreCase));
                    var productSuccess = isSuccess;
                    results.Add(new AxataProductSynchronizationResultDto(
                        product.ProductCode,
                        productSuccess,
                        response.state,
                        FirstNonEmpty(
                            processResult?.LogMessage,
                            response.message,
                            productSuccess
                                ? "AXATA urun master aktarimi basarili."
                                : "AXATA urun master aktarimi basarisiz."),
                        product.Barcodes.Count,
                        product.Units.Count));
                }

                if (!isSuccess && !request.ContinueOnError)
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                if (client is not null)
                {
                    AbortClient(client);
                }

                results.AddRange(batch.Select(product => new AxataProductSynchronizationResultDto(
                    product.ProductCode,
                    false,
                    null,
                    exception.Message,
                    product.Barcodes.Count,
                    product.Units.Count)));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
        }

        return new AxataProductSynchronizationExecuteDto(
            DateTime.UtcNow,
            OperationName,
            configuration.EndpointUrl,
            products.Count,
            results.Count(result => result.IsSuccess),
            results.Count(result => !result.IsSuccess),
            results,
            [
                $"Urunler {DispatchBatchSize} kayitlik paketlerle AXATA {OperationName} operasyonuna gonderildi.",
                "Her paket master, barkod ve birim listelerini birlikte tasir.",
                request.ContinueOnError
                    ? "Bir paket hata verse bile sonraki paketlere devam edildi."
                    : "Ilk hatali pakette aktarim durduruldu."
            ]);
    }

    private async Task<int> CountProductsAsync(
        string productCode,
        CancellationToken cancellationToken)
    {
        var query = mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock =>
                stock.sto_iptal != true &&
                !(stock.sto_pasif_fl ?? false) &&
                stock.sto_kod != null);
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            query = query.Where(stock => stock.sto_kod == productCode);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<ProductSynchronizationRecord>> ReadProductsAsync(
        IReadOnlyCollection<string> productCodes,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = productCodes
            .Select(NormalizeCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var stockQuery = mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock =>
                stock.sto_iptal != true &&
                !(stock.sto_pasif_fl ?? false) &&
                stock.sto_kod != null);
        if (normalizedCodes.Length > 0)
        {
            stockQuery = stockQuery.Where(stock => normalizedCodes.Contains(stock.sto_kod));
        }

        var stocks = await stockQuery
            .OrderBy(stock => stock.sto_kod)
            .Take(Math.Min(MaxTake, Math.Max(1, take)))
            .Select(stock => new ProductStockRow(
                stock.sto_kod,
                stock.sto_isim ?? string.Empty,
                stock.sto_kisa_ismi ?? string.Empty,
                stock.sto_paket_kodu,
                stock.sto_cins,
                stock.sto_toplam_rafomru,
                stock.sto_birim1_ad,
                stock.sto_birim1_katsayi,
                stock.sto_birim1_agirlik,
                stock.sto_birim1_en,
                stock.sto_birim1_boy,
                stock.sto_birim1_yukseklik,
                stock.sto_birim2_ad,
                stock.sto_birim2_katsayi,
                stock.sto_birim2_agirlik,
                stock.sto_birim2_en,
                stock.sto_birim2_boy,
                stock.sto_birim2_yukseklik,
                stock.sto_birim3_ad,
                stock.sto_birim3_katsayi,
                stock.sto_birim3_agirlik,
                stock.sto_birim3_en,
                stock.sto_birim3_boy,
                stock.sto_birim3_yukseklik,
                stock.sto_birim4_ad,
                stock.sto_birim4_katsayi,
                stock.sto_birim4_agirlik,
                stock.sto_birim4_en,
                stock.sto_birim4_boy,
                stock.sto_birim4_yukseklik,
                stock.sto_kasa_tarti_fl ?? false,
                (stock.sto_satis_dursun ?? 0) != 0,
                (stock.sto_siparis_dursun ?? 0) != 0,
                (stock.sto_malkabul_dursun ?? 0) != 0))
            .ToArrayAsync(cancellationToken);
        var stockCodes = stocks.Select(stock => stock.ProductCode).ToArray();
        var barcodes = stockCodes.Length == 0
            ? []
            : await mikroDbContext.BARKOD_TANIMLARIs
                .AsNoTracking()
                .Where(barcode =>
                    barcode.bar_iptal != true &&
                    barcode.bar_stokkodu != null &&
                    stockCodes.Contains(barcode.bar_stokkodu) &&
                    barcode.bar_kodu != null &&
                    barcode.bar_kodu != string.Empty)
                .OrderByDescending(barcode => barcode.bar_master ?? false)
                .ThenBy(barcode => barcode.bar_birimpntr ?? 1)
                .ThenBy(barcode => barcode.bar_kodu)
                .Select(barcode => new ProductBarcodeRow(
                    barcode.bar_stokkodu ?? string.Empty,
                    barcode.bar_kodu ?? string.Empty,
                    barcode.bar_birimpntr ?? 1,
                    barcode.bar_master ?? false))
                .ToArrayAsync(cancellationToken);
        var barcodesByStock = barcodes
            .GroupBy(barcode => barcode.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(barcode => barcode.Barcode, StringComparer.OrdinalIgnoreCase)
                    .Select(barcodeGroup => barcodeGroup.First())
                    .ToArray() as IReadOnlyCollection<ProductBarcodeRow>,
                StringComparer.OrdinalIgnoreCase);

        return stocks
            .Select(stock =>
            {
                var units = BuildUnits(stock);
                var productBarcodes = barcodesByStock.GetValueOrDefault(stock.ProductCode)
                    ?? Array.Empty<ProductBarcodeRow>();
                return new ProductSynchronizationRecord(
                    stock.ProductCode,
                    FirstNonEmpty(stock.ProductName, stock.ShortName, stock.ProductCode),
                    units.First().UnitCode,
                    NormalizeAxataCode(stock.PackageCode, 2),
                    stock.TypeCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    stock.ShelfLife ?? 0,
                    stock.ScaleProduct,
                    stock.SaleBlocked,
                    stock.OrderBlocked,
                    stock.GoodsAcceptanceBlocked,
                    units,
                    productBarcodes
                        .Select(barcode => new ProductSynchronizationBarcode(
                            barcode.Barcode,
                            ResolveBarcodeUnit(units, barcode.UnitPointer),
                            barcode.IsMaster))
                        .Append(new ProductSynchronizationBarcode(
                            stock.ProductCode,
                            units.First().UnitCode,
                            false))
                        .GroupBy(barcode => barcode.Barcode, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToArray());
            })
            .ToArray();
    }

    private static IReadOnlyCollection<ProductSynchronizationUnit> BuildUnits(ProductStockRow stock)
    {
        var candidates = new[]
        {
            new ProductSynchronizationUnit(
                1,
                NormalizeUnitCode(stock.Unit1Name),
                NormalizeFactor(stock.Unit1Factor),
                stock.Unit1Weight,
                stock.Unit1Width,
                stock.Unit1Length,
                stock.Unit1Height,
                true),
            new ProductSynchronizationUnit(
                2,
                NormalizeUnitCode(stock.Unit2Name),
                NormalizeFactor(stock.Unit2Factor),
                stock.Unit2Weight,
                stock.Unit2Width,
                stock.Unit2Length,
                stock.Unit2Height,
                false),
            new ProductSynchronizationUnit(
                3,
                NormalizeUnitCode(stock.Unit3Name),
                NormalizeFactor(stock.Unit3Factor),
                stock.Unit3Weight,
                stock.Unit3Width,
                stock.Unit3Length,
                stock.Unit3Height,
                false),
            new ProductSynchronizationUnit(
                4,
                NormalizeUnitCode(stock.Unit4Name),
                NormalizeFactor(stock.Unit4Factor),
                stock.Unit4Weight,
                stock.Unit4Width,
                stock.Unit4Length,
                stock.Unit4Height,
                false)
        };
        var units = candidates
            .Where(unit => !string.IsNullOrWhiteSpace(unit.UnitCode))
            .GroupBy(unit => unit.UnitCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (units.Count == 0)
        {
            units.Add(new ProductSynchronizationUnit(
                1,
                DefaultUnitCode,
                1d,
                null,
                null,
                null,
                null,
                true));
        }
        else if (!units[0].IsDefault)
        {
            units[0] = units[0] with { IsDefault = true };
        }

        return units;
    }

    private static AxataProductSynchronizationItemDto ToPreviewItem(ProductSynchronizationRecord product)
    {
        var payload = ToPayload(product);
        return new AxataProductSynchronizationItemDto(
            product.ProductCode,
            product.ProductName,
            product.MainUnit,
            product.Barcodes.Count,
            product.Barcodes.Select(barcode => barcode.Barcode).ToArray(),
            product.Units.Count,
            JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options));
    }

    private static ProductSynchronizationPayload ToPayload(ProductSynchronizationRecord product) =>
        new(
            BuildMasterPayload(product),
            product.Barcodes
                .Select(barcode => new ProductBarcodePayload(
                    CompanyCode,
                    product.ProductCode,
                    barcode.Barcode,
                    barcode.IsMaster,
                    barcode.UnitCode))
                .ToArray(),
            product.Units
                .Select(unit => new ProductUnitPayload(
                    CompanyCode,
                    product.ProductCode,
                    unit.UnitCode,
                    product.MainUnit,
                    unit.Factor,
                    unit.IsDefault,
                    unit.Width,
                    unit.Length,
                    unit.Height,
                    unit.Weight))
                .ToArray());

    private static ProductMasterPayload BuildMasterPayload(ProductSynchronizationRecord product)
    {
        var baseUnit = product.Units
            .Where(unit => !unit.IsDefault)
            .OrderBy(unit => unit.Pointer)
            .FirstOrDefault()
            ?? product.Units.First();

        return new ProductMasterPayload(
            CompanyCode,
            product.ProductCode,
            NormalizeAxataCode(product.ProductName, 20),
            product.ProductName,
            product.MainUnit,
            FirstNonEmpty(product.PackageCode, baseUnit.UnitCode),
            baseUnit.Factor,
            product.TypeCode,
            product.ShelfLife,
            product.Barcodes.FirstOrDefault(barcode => barcode.IsMaster)?.Barcode
            ?? product.Barcodes.FirstOrDefault()?.Barcode
            ?? string.Empty,
            product.ScaleProduct,
            product.SaleBlocked,
            product.OrderBlocked,
            product.GoodsAcceptanceBlocked);
    }

    private static AxataSku.SKUMaster ToWcfProduct(ProductSynchronizationRecord product)
    {
        var payload = ToPayload(product);
        return new AxataSku.SKUMaster
        {
            ENT004 = new AxataMain.ENT004
            {
                S04SKOD = payload.Master.CompanyCode,
                S04MKOD = payload.Master.ProductCode,
                S04KTAN = payload.Master.ProductName,
                S04UTAN = payload.Master.ProductDescription,
                S04SNFK = payload.Master.TypeCode,
                S04MKBR = payload.Master.MainUnit,
                S04MBBR = payload.Master.BaseUnit,
                S04BKOR = (decimal)payload.Master.BaseUnitFactor,
                S04PSTAN = 1m,
                S04FIFO = 1,
                S04KULBRM = 0,
                S04GTKON = 1,
                S04IKOD = 0,
                S04LOT = "0",
                S04LOT2 = 0,
                S04LOT3 = 0,
                S04SERI = 0,
                S04SERI2 = 0,
                S04SERI3 = 0,
                S04ROMUR = payload.Master.ShelfLife,
                S04PALTIP = "EU",
                S04OND = 0,
                S04SERIURET = 0,
                S04GTIN = payload.Master.DefaultBarcode,
                S04DSIM = 0,
                S04CATI = 0,
                S04ENT1 = payload.Master.SaleBlocked ? "1" : "0",
                S04ENT2 = payload.Master.OrderBlocked ? "1" : "0",
                S04ENT3 = payload.Master.GoodsAcceptanceBlocked ? "1" : "0"
            },
            ENT003_List = payload.Barcodes
                .Select(barcode => new AxataMain.ENT003
                {
                    S03SKOD = barcode.CompanyCode,
                    S03MKOD = barcode.ProductCode,
                    S03BCODE = barcode.Barcode,
                    S03ISDEF = barcode.IsDefault,
                    S03UNIT = barcode.UnitCode
                })
                .ToArray(),
            ENT004_UNIT_List = payload.Units
                .Select(unit => new AxataMain.ENT004_UNIT
                {
                    S04SKOD = unit.CompanyCode,
                    S04MKOD = unit.ProductCode,
                    S04BKOD = unit.UnitCode,
                    S04AKOD = unit.MainUnit,
                    S04CARP = (float)unit.Factor,
                    S04TYPE = 0,
                    S04GKUL = 1,
                    S04IKUL = 1,
                    S04CKUL = 1,
                    S04DKUL = 1,
                    S04DEFB = unit.IsDefault ? (byte)1 : (byte)0,
                    S04EN = ToDecimal(unit.Width),
                    S04BOY = ToDecimal(unit.Length),
                    S04YUK = ToDecimal(unit.Height),
                    S04NAGR = ToDecimal(unit.Weight)
                })
                .ToArray(),
            ENT004PL_List =
            [
                new AxataMain.ENT004PL
                {
                    S04SKOD = payload.Master.CompanyCode,
                    S04MKOD = payload.Master.ProductCode,
                    S04PALTIP = "EU",
                    S04PSTD = 100000m,
                    S04DEPO = "01"
                }
            ],
            ENT004_PROP_List = []
        };
    }

    private ProductSynchronizationConfiguration GetRequiredConfiguration()
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled in configuration.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.MainEndpointUrl))
        {
            throw new InvalidOperationException("AXATA main endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username) ||
            string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException("AXATA username/password is not configured.");
        }

        return new ProductSynchronizationConfiguration(
            currentOptions.MainEndpointUrl.Trim(),
            currentOptions.Username,
            currentOptions.Password);
    }

    private static AxataMain.AxataServicePoolClient CreateClient(string endpointUrl) =>
        new(
            AxataMain.AxataServicePoolClient.EndpointConfiguration.BasicHttpBinding_IAxataServicePool,
            endpointUrl);

    private static void CloseClient(ICommunicationObject client)
    {
        if (client.State == CommunicationState.Faulted)
        {
            client.Abort();
            return;
        }

        client.Close();
    }

    private static void AbortClient(ICommunicationObject client)
    {
        if (client.State != CommunicationState.Closed)
        {
            client.Abort();
        }
    }

    private static int NormalizeTake(int? take) =>
        Math.Min(MaxTake, Math.Max(1, take ?? DefaultTake));

    private static string ResolveBarcodeUnit(
        IReadOnlyCollection<ProductSynchronizationUnit> units,
        byte unitPointer)
    {
        var normalizedPointer = Math.Max(1, (int)unitPointer);
        return units.FirstOrDefault(unit => unit.Pointer == normalizedPointer)?.UnitCode
               ?? units.First().UnitCode;
    }

    private static string NormalizeCode(string? value) =>
        value?.Trim() ?? string.Empty;

    private static string NormalizeUnitCode(string? value) =>
        NormalizeAxataCode(value, 2);

    private static double NormalizeFactor(double? value) =>
        value is { } factor && factor != 0d ? Math.Abs(factor) : 1d;

    private static string NormalizeAxataCode(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static decimal? ToDecimal(double? value) =>
        value.HasValue ? (decimal)value.Value : null;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

internal sealed record ProductSynchronizationConfiguration(
    string EndpointUrl,
    string Username,
    string Password);

internal sealed record ProductStockRow(
    string ProductCode,
    string ProductName,
    string ShortName,
    string? PackageCode,
    byte? TypeCode,
    short? ShelfLife,
    string? Unit1Name,
    double? Unit1Factor,
    double? Unit1Weight,
    double? Unit1Width,
    double? Unit1Length,
    double? Unit1Height,
    string? Unit2Name,
    double? Unit2Factor,
    double? Unit2Weight,
    double? Unit2Width,
    double? Unit2Length,
    double? Unit2Height,
    string? Unit3Name,
    double? Unit3Factor,
    double? Unit3Weight,
    double? Unit3Width,
    double? Unit3Length,
    double? Unit3Height,
    string? Unit4Name,
    double? Unit4Factor,
    double? Unit4Weight,
    double? Unit4Width,
    double? Unit4Length,
    double? Unit4Height,
    bool ScaleProduct,
    bool SaleBlocked,
    bool OrderBlocked,
    bool GoodsAcceptanceBlocked);

internal sealed record ProductBarcodeRow(
    string ProductCode,
    string Barcode,
    byte UnitPointer,
    bool IsMaster);

internal sealed record ProductSynchronizationRecord(
    string ProductCode,
    string ProductName,
    string MainUnit,
    string PackageCode,
    string TypeCode,
    short ShelfLife,
    bool ScaleProduct,
    bool SaleBlocked,
    bool OrderBlocked,
    bool GoodsAcceptanceBlocked,
    IReadOnlyCollection<ProductSynchronizationUnit> Units,
    IReadOnlyCollection<ProductSynchronizationBarcode> Barcodes);

internal sealed record ProductSynchronizationUnit(
    int Pointer,
    string UnitCode,
    double Factor,
    double? Weight,
    double? Width,
    double? Length,
    double? Height,
    bool IsDefault);

internal sealed record ProductSynchronizationBarcode(
    string Barcode,
    string UnitCode,
    bool IsMaster);

internal sealed record ProductSynchronizationPayload(
    ProductMasterPayload Master,
    IReadOnlyCollection<ProductBarcodePayload> Barcodes,
    IReadOnlyCollection<ProductUnitPayload> Units);

internal sealed record ProductMasterPayload(
    string CompanyCode,
    string ProductCode,
    string ProductName,
    string ProductDescription,
    string MainUnit,
    string BaseUnit,
    double BaseUnitFactor,
    string TypeCode,
    short ShelfLife,
    string DefaultBarcode,
    bool ScaleProduct,
    bool SaleBlocked,
    bool OrderBlocked,
    bool GoodsAcceptanceBlocked);

internal sealed record ProductBarcodePayload(
    string CompanyCode,
    string ProductCode,
    string Barcode,
    bool IsDefault,
    string UnitCode);

internal sealed record ProductUnitPayload(
    string CompanyCode,
    string ProductCode,
    string UnitCode,
    string MainUnit,
    double Factor,
    bool IsDefault,
    double? Width,
    double? Length,
    double? Height,
    double? Weight);
