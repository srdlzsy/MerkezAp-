using System.Globalization;
using System.Text;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

internal sealed class OperationsFileGenerationService(
    MikroDbContext mikroDbContext,
    FurpaDbContext furpaDbContext,
    IOptions<OperationsExportOptions> options,
    IConfiguration configuration,
    ILogger<OperationsFileGenerationService> logger)
{
    private static readonly Encoding FileEncoding = Encoding.GetEncoding(1254);
    private static readonly string[] PromotionCodeColumns =
    [
        "PromotionCode",
        "PromosyonKodu",
        "PROMOSYON_KODU",
        "PROMOSYONKODU",
        "ProCode",
        "ProKod",
        "ProKodu",
        "subeProKod",
        "pro_kod",
        "pro_kodu"
    ];
    private static readonly string[] BranchNoColumns =
    [
        "BranchNo",
        "SubeNo",
        "MagazaNo",
        "Sube",
        "sube_no",
        "magaza_no",
        "BranchCode",
        "branch_no",
        "subeProSubeKod",
        "WarehouseNo",
        "DepoNo"
    ];
    private static readonly string[] ExpirationDateColumns =
    [
        "ExpirationDate",
        "ExpireDate",
        "EndDate",
        "BitisTarihi",
        "SonTarih",
        "ProBitTarihi",
        "pro_bitis_tarihi"
    ];

    public async Task<OperationGenerationResult> GenerateAsync(
        OperationFileKind kind,
        int warehouseNo,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        return kind switch
        {
            OperationFileKind.ScalesFile => await GenerateScalesFileAsync(warehouseNo, jobId, cancellationToken),
            OperationFileKind.ProductBarcodePluNoFile => await GenerateProductFilesAsync(warehouseNo, jobId, cancellationToken),
            OperationFileKind.CashierFile => await GenerateCashierFilesAsync(warehouseNo, jobId, cancellationToken),
            OperationFileKind.PromoFile => await GeneratePromoFilesAsync(warehouseNo, jobId, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported operation file kind: {kind}.")
        };
    }

    private async Task<OperationGenerationResult> GenerateScalesFileAsync(
        int warehouseNo,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var branchDetail = await GetRequiredBranchDetailAsync(warehouseNo, cancellationToken);
        var products = await ListScaleProductsAsync(warehouseNo, cancellationToken);

        if (products.Count == 0)
        {
            throw new InvalidOperationException("No scale products were found for the selected warehouse.");
        }

        var exportDirectory = EnsureLocalDirectory(warehouseNo, jobId, "scalesfile");
        GeneratedOperationFileDto generatedFile = branchDetail.ScalesType switch
        {
            0 => await WriteCas16ScaleFileAsync(exportDirectory, branchDetail, products, cancellationToken),
            1 => await WriteCas500ScaleFileAsync(exportDirectory, branchDetail, products, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported scales type '{branchDetail.ScalesType}' for warehouse {warehouseNo}.")
        };

        return new OperationGenerationResult(
            "Terazi dosyasi olusturuldu.",
            [generatedFile]);
    }

    private async Task<OperationGenerationResult> GenerateProductFilesAsync(
        int warehouseNo,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var branchDetail = await GetOptionalBranchDetailAsync(warehouseNo, cancellationToken);
        var cashRegisters = await ListCashRegistersAsync(warehouseNo, cancellationToken);
        var products = await ListProductFileRowsAsync(warehouseNo, cancellationToken);

        if (products.Count == 0)
        {
            throw new InvalidOperationException("No products were found for product barcode/PLU export.");
        }

        var orderedForIndex = products
            .OrderBy(item => item.PluNo)
            .ThenBy(item => item.ProductCode, StringComparer.OrdinalIgnoreCase)
            .Select((item, index) => new IndexedProductRow(index + 1, item))
            .ToArray();

        var exportDirectory = EnsureLocalDirectory(warehouseNo, jobId, "productbarcodeplunofile");
        var generatedFiles = new List<GeneratedOperationFileDto>
        {
            await WriteProductDatFileAsync(exportDirectory, branchDetail, orderedForIndex, cancellationToken),
            await WriteBarcodeIndexFileAsync(exportDirectory, branchDetail, orderedForIndex, cancellationToken),
            await WritePluIndexFileAsync(exportDirectory, branchDetail, orderedForIndex, cancellationToken)
        };

        generatedFiles.AddRange(await WriteMessageFilesAsync(
            exportDirectory,
            branchDetail,
            cashRegisters,
            "1071",
            cancellationToken));

        return new OperationGenerationResult(
            "URUN.DAT, BARKOD.IDX ve PLUNO.IDX dosyalari olusturuldu.",
            generatedFiles);
    }

    private async Task<OperationGenerationResult> GenerateCashierFilesAsync(
        int warehouseNo,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var branchDetail = await GetOptionalBranchDetailAsync(warehouseNo, cancellationToken);
        var cashRegisters = await ListCashRegistersAsync(warehouseNo, cancellationToken);
        var cashiers = await furpaDbContext.Cashiers
            .AsNoTracking()
            .Where(item => item.CashierState)
            .OrderBy(item => item.CashierCode)
            .ToArrayAsync(cancellationToken);
        var authorizationFiles = await furpaDbContext.AuthorizationFiles
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        if (cashiers.Length == 0)
        {
            throw new InvalidOperationException("No active cashiers were found for cashier export.");
        }

        var exportDirectory = EnsureLocalDirectory(warehouseNo, jobId, "cashierfile");
        var generatedFiles = new List<GeneratedOperationFileDto>
        {
            await WriteCashierDatFileAsync(exportDirectory, branchDetail, cashiers, cancellationToken),
            await WriteAuthorizationDatFileAsync(exportDirectory, branchDetail, authorizationFiles, cancellationToken)
        };

        generatedFiles.AddRange(await WriteMessageFilesAsync(
            exportDirectory,
            branchDetail,
            cashRegisters,
            "1140001000000000000000010000000000000000000000",
            cancellationToken));

        return new OperationGenerationResult(
            "KASIYER.DAT ve YETKI.DAT dosyalari olusturuldu.",
            generatedFiles);
    }

    private async Task<OperationGenerationResult> GeneratePromoFilesAsync(
        int warehouseNo,
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var branchDetail = await GetOptionalBranchDetailAsync(warehouseNo, cancellationToken);
        var cashRegisters = await ListCashRegistersAsync(warehouseNo, cancellationToken);
        var noPromoPlus = await ListNoPromoPlusAsync(cancellationToken);
        var groupAndSpecialRows = await ListGroupAndSpecialRowsAsync(cancellationToken);
        var promotions = await ListPromotionRowsAsync(warehouseNo, cancellationToken);
        var gibTaxNumbers = await TryListGibTaxNumbersAsync(cancellationToken);

        var exportDirectory = EnsureLocalDirectory(warehouseNo, jobId, "promofile");
        var generatedFiles = new List<GeneratedOperationFileDto>
        {
            await WritePromoDatFileAsync(exportDirectory, branchDetail, promotions, cancellationToken),
            await WritePluListFileAsync(exportDirectory, branchDetail, "NOPROMO.DAT", noPromoPlus, cancellationToken),
            await WritePluListFileAsync(exportDirectory, branchDetail, "NOCEK.DAT", noPromoPlus, cancellationToken),
            await WritePluListFileAsync(exportDirectory, branchDetail, "NOYEMEK.DAT", noPromoPlus, cancellationToken),
            await WriteGroupSpecialFileAsync(exportDirectory, branchDetail, "GRUP.DAT", groupAndSpecialRows, cancellationToken),
            await WriteGroupSpecialFileAsync(exportDirectory, branchDetail, "OZELKOD.DAT", groupAndSpecialRows, cancellationToken),
            await WriteEInvoiceTaxNumberFileAsync(exportDirectory, branchDetail, gibTaxNumbers, cancellationToken)
        };

        generatedFiles.AddRange(await WriteMessageFilesAsync(
            exportDirectory,
            branchDetail,
            cashRegisters,
            "1140000010000000000000010000001101000000000000111",
            cancellationToken));

        return new OperationGenerationResult(
            "PROMO.DAT ve yardimci promosyon dosyalari olusturuldu.",
            generatedFiles);
    }

    private async Task<GeneratedOperationFileDto> WriteCas16ScaleFileAsync(
        string exportDirectory,
        BranchDetailEntity branchDetail,
        IReadOnlyCollection<ScaleProductRow> products,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "Terazi.plu");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var product in products)
            {
                var plu = GeneratePluStringFromBarcode(product.Barcode, product.PluNo);
                var prefix = product.Barcode.Length >= 2
                    ? product.Barcode[..2]
                    : product.Barcode.PadLeft(2, '0');

                await writer.WriteLineAsync(
                    $"{plu.PadRight(4)}\t{FormatScaleName(product.ProductName, 28)}\t{FormatScaleName(product.ProductName, 28)}\t" +
                    $"{GenerateCas16PriceString(product.Price)}\t000\t0000\t{plu.PadLeft(6, '0')}\t{prefix}\t111\t");
            }
        }

        return await BuildGeneratedFileAsync(
            localPath,
            branchDetail,
            branchDetail.BranchScalesFolderPath,
            cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteCas500ScaleFileAsync(
        string exportDirectory,
        BranchDetailEntity branchDetail,
        IReadOnlyCollection<ScaleProductRow> products,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "ART_STM.txt");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var product in products)
            {
                await writer.WriteLineAsync(
                    $"{product.PluNo.ToString(CultureInfo.InvariantCulture)}  {product.Barcode.PadLeft(8, '0')}" +
                    $"     {FormatScaleName(product.ProductName, 24)}{GenerateCas500PriceString(product.Price)}   00 " +
                    $"{product.ShelfLife.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')}");
            }
        }

        return await BuildGeneratedFileAsync(
            localPath,
            branchDetail,
            branchDetail.BranchScalesFolderPath,
            cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteProductDatFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<IndexedProductRow> products,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "URUN.DAT");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var item in products)
            {
                var product = item.Product;
                await writer.WriteLineAsync(
                    "1" +
                    product.PluNo.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0') +
                    FixedField(product.Barcode, 20) +
                    FixedField(NormalizeTurkishAscii(product.ProductName), 20) +
                    product.Price.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(9, '0') +
                    product.RetailSaleTaxRate.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') +
                    FormatUnitName(product.UnitName) +
                    (product.BarcodeContent ? "E" : "H"));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteBarcodeIndexFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<IndexedProductRow> products,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "BARKOD.IDX");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var item in products.OrderBy(product => product.Product.Barcode, StringComparer.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync(
                    item.Product.Barcode.PadRight(20, ' ') +
                    item.IndexNumber.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0'));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WritePluIndexFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<IndexedProductRow> products,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "PLUNO.IDX");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var item in products.OrderBy(product => product.Product.PluNo))
            {
                await writer.WriteLineAsync(
                    item.Product.PluNo.ToString(CultureInfo.InvariantCulture).PadRight(6, '0') +
                    item.IndexNumber.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0'));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteCashierDatFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<CashierEntity> cashiers,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "KASIYER.DAT");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var cashier in cashiers)
            {
                var cashierName = FormatCashierName(cashier.CashierName);
                await writer.WriteLineAsync(
                    "1" +
                    cashier.CashierCode.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0') +
                    FixedField(NormalizeTurkishAscii(cashierName), 20) +
                    cashier.CashierPassword +
                    cashier.CashierAuthorization);
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteAuthorizationDatFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<AuthorizationFileEntity> authorizationFiles,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "YETKI.DAT");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            await writer.WriteLineAsync(BuildAuthorizationLine("Z", authorizationFiles.Select(item => item.Z)));
            await writer.WriteLineAsync(BuildAuthorizationLine("R", authorizationFiles.Select(item => item.R)));
            await writer.WriteLineAsync(BuildAuthorizationLine("X", authorizationFiles.Select(item => item.X)));
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WritePromoDatFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<PromotionRow> promotions,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "PROMO.DAT");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var promotion in promotions.Where(item => item.PromotionType == "P1").OrderBy(item => item.PluNo))
            {
                await writer.WriteLineAsync(BuildP1Line(promotion));
            }

            foreach (var promotion in promotions.Where(item => item.PromotionType == "P2").OrderBy(item => item.ProductPluNo))
            {
                await writer.WriteLineAsync(BuildP2Line(promotion));
            }

            foreach (var promotion in promotions.Where(item => item.PromotionType == "P3").OrderBy(item => item.PluNo))
            {
                await writer.WriteLineAsync(BuildP3Line(promotion));
            }

            foreach (var promotion in promotions.Where(item => item.PromotionType == "P8").OrderBy(item => item.PluNo))
            {
                await writer.WriteLineAsync(BuildP8Line(promotion));
            }

            foreach (var promotion in promotions.Where(item => item.PromotionType == "P9").OrderBy(item => item.PluNo))
            {
                await writer.WriteLineAsync(BuildP9FirstLine(promotion));
                await writer.WriteLineAsync(BuildP9SecondLine(promotion));
            }

            foreach (var promotion in promotions.Where(item => item.PromotionType == "PM").OrderBy(item => item.ProTotalShipping))
            {
                await writer.WriteLineAsync(BuildPmLine(promotion));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WritePluListFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        string fileName,
        IReadOnlyCollection<int> pluNos,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, fileName);
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var pluNo in pluNos.OrderBy(item => item))
            {
                await writer.WriteLineAsync(pluNo.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0'));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteGroupSpecialFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        string fileName,
        IReadOnlyCollection<GroupSpecialProductRow> rows,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, fileName);
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var row in rows.OrderBy(item => item.PluNo))
            {
                await writer.WriteLineAsync(
                    row.PluNo.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0') +
                    "," +
                    FixedField(row.QualityControlCode, 4));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<GeneratedOperationFileDto> WriteEInvoiceTaxNumberFileAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<string> taxNumbers,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(exportDirectory, "EFATVNO.DAT");
        await using (var writer = new StreamWriter(localPath, false, FileEncoding))
        {
            foreach (var taxNumber in taxNumbers.OrderBy(item => item, StringComparer.Ordinal))
            {
                await writer.WriteLineAsync(FixedField(taxNumber, 15));
            }
        }

        return await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PosGenelFolderPath, cancellationToken);
    }

    private async Task<IReadOnlyCollection<GeneratedOperationFileDto>> WriteMessageFilesAsync(
        string exportDirectory,
        BranchDetailEntity? branchDetail,
        IReadOnlyCollection<CashRegistryDetailEntity> cashRegisters,
        string content,
        CancellationToken cancellationToken)
    {
        if (cashRegisters.Count == 0)
        {
            return Array.Empty<GeneratedOperationFileDto>();
        }

        var files = new List<GeneratedOperationFileDto>();
        foreach (var cashRegister in cashRegisters.OrderBy(item => item.CashRegisterNo))
        {
            var fileName = $"MESAJ.{cashRegister.CashRegisterNo.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0')}";
            var localPath = Path.Combine(exportDirectory, fileName);

            await File.WriteAllTextAsync(localPath, content + Environment.NewLine, cancellationToken);
            files.Add(await BuildGeneratedFileAsync(localPath, branchDetail, branchDetail?.PoskonFolderPath, cancellationToken));
        }

        return files;
    }

    private async Task<GeneratedOperationFileDto> BuildGeneratedFileAsync(
        string localPath,
        BranchDetailEntity? branchDetail,
        string? targetFolderPath,
        CancellationToken cancellationToken)
    {
        var networkPath = await TryCopyToNetworkAsync(localPath, branchDetail, targetFolderPath, cancellationToken);
        return new GeneratedOperationFileDto(
            Path.GetFileName(localPath),
            localPath,
            networkPath);
    }

    private async Task<string?> TryCopyToNetworkAsync(
        string localPath,
        BranchDetailEntity? branchDetail,
        string? folderPath,
        CancellationToken cancellationToken)
    {
        if (branchDetail is null ||
            string.IsNullOrWhiteSpace(branchDetail.BranchIpAddress) ||
            string.IsNullOrWhiteSpace(folderPath))
        {
            return null;
        }

        var targetDirectory = BuildUncDirectory(branchDetail.BranchIpAddress, folderPath);
        Directory.CreateDirectory(targetDirectory);

        var destinationPath = Path.Combine(targetDirectory, Path.GetFileName(localPath));
        await using var sourceStream = File.Open(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destinationStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        logger.LogInformation("Copied operation export file to {DestinationPath}.", destinationPath);
        return destinationPath;
    }

    private async Task<BranchDetailEntity> GetRequiredBranchDetailAsync(int warehouseNo, CancellationToken cancellationToken) =>
        await GetOptionalBranchDetailAsync(warehouseNo, cancellationToken)
        ?? throw new KeyNotFoundException(
            $"Branch detail was not found for warehouse {warehouseNo}. Scales export requires branch configuration.");

    private Task<BranchDetailEntity?> GetOptionalBranchDetailAsync(int warehouseNo, CancellationToken cancellationToken) =>
        furpaDbContext.BranchDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BranchNo == warehouseNo, cancellationToken);

    private async Task<IReadOnlyCollection<CashRegistryDetailEntity>> ListCashRegistersAsync(
        int warehouseNo,
        CancellationToken cancellationToken) =>
        await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == warehouseNo)
            .OrderBy(item => item.CashRegisterNo)
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyCollection<ScaleProductRow>> ListScaleProductsAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from stock in mikroDbContext.STOKLARs.AsNoTracking()
            join price in mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs.AsNoTracking()
                on stock.sto_kod equals price.sfiyat_stokkod
            join barcode in mikroDbContext.BARKOD_TANIMLARIs.AsNoTracking()
                on stock.sto_kod equals barcode.bar_stokkodu
            where barcode.bar_kodu != null &&
                  (barcode.bar_kodu.StartsWith("27") || barcode.bar_kodu.StartsWith("29")) &&
                  barcode.bar_kodu.Length == 7 &&
                  (barcode.bar_birimpntr ?? 0) == 1 &&
                  price.sfiyat_deposirano == warehouseNo &&
                  (price.sfiyat_fiyati ?? 0d) > 0d &&
                  (price.sfiyat_listesirano ?? 0) == 1 &&
                  !(stock.sto_pasif_fl ?? false) &&
                  (stock.sto_satis_dursun ?? 0) == 0
            orderby stock.sto_plu_no, stock.sto_kod
            select new ScaleProductRow(
                stock.sto_kod,
                stock.sto_isim ?? string.Empty,
                barcode.bar_kodu ?? string.Empty,
                price.sfiyat_fiyati ?? 0d,
                stock.sto_plu_no,
                stock.sto_RafOmru ?? stock.sto_toplam_rafomru ?? 0))
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<int>> ListNoPromoPlusAsync(CancellationToken cancellationToken) =>
        await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => (item.sto_perakende_vergi ?? 0) == 4)
            .OrderBy(item => item.sto_plu_no)
            .Select(item => item.sto_plu_no)
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyCollection<GroupSpecialProductRow>> ListGroupAndSpecialRowsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_mkod_artik != null && item.sto_mkod_artik != string.Empty && item.sto_mkod_artik != "0")
            .OrderBy(item => item.sto_plu_no)
            .Select(item => new GroupSpecialProductRow(
                item.sto_plu_no,
                item.sto_mkod_artik ?? string.Empty))
            .ToArrayAsync(cancellationToken);

        return rows;
    }

    private async Task<IReadOnlyCollection<PromotionRow>> ListPromotionRowsAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var connectionString = GetRequiredConnectionString(
            "MaydayConnection",
            "MaydayMarketConnection",
            "MaydaYMarketConnection");

        var promotionRows = await ReadSqlRowsAsync(
            connectionString,
            "SELECT * FROM PROMOSYON_TANIMLARI",
            cancellationToken);
        var branchRows = await ReadSqlRowsAsync(
            connectionString,
            "SELECT * FROM PROMOSYON_SUBELER",
            cancellationToken);

        if (branchRows.Count > 0 &&
            (!RowsContainAnyColumn(branchRows, BranchNoColumns) ||
             !RowsContainAnyColumn(branchRows, PromotionCodeColumns)))
        {
            throw new InvalidOperationException(
                "PROMOSYON_SUBELER rows were found, but BranchNo/PromotionCode columns could not be mapped. " +
                $"Available columns: {FormatAvailableColumns(branchRows)}.");
        }

        var branchPromotionCodes = branchRows
            .Where(row => ReadInt(row, BranchNoColumns) == warehouseNo)
            .Select(row => ReadString(row, PromotionCodeColumns))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.Now;
        var promotions = new List<PromotionRow>();
        var filterByBranch = branchRows.Count > 0;

        foreach (var row in promotionRows)
        {
            var promotionCode = ReadString(row, PromotionCodeColumns);
            if (string.IsNullOrWhiteSpace(promotionCode) ||
                (filterByBranch && !branchPromotionCodes.Contains(promotionCode)))
            {
                continue;
            }

            var expirationDate = ReadDate(row, ExpirationDateColumns);
            var proPassive = ReadInt(row, "ProPassive", "proPassive", "ProPasif", "pro_pasif", "Passive", "pasif");
            if ((expirationDate.HasValue && expirationDate.Value < now) ||
                (proPassive.HasValue && proPassive.Value != 0))
            {
                continue;
            }

            promotions.Add(new PromotionRow(
                PromotionCode: promotionCode,
                PromotionType: ReadString(row, "PromotionType", "ProType", "ProTypeCode", "ProTip", "ProTipi", "pro_tipi", "PromosyonTipi", "PromosyonTuru", "Tip").ToUpperInvariant(),
                ProFlag: ReadString(row, "ProFlag", "proFlag", "ProBayrak", "pro_flag"),
                PromotionCustomerCode: ReadString(row, "PromotionCustomerCode", "CustomerCode", "MusteriKodu", "CariKodu", "PromMusteriKodu"),
                StartDate: ReadDate(row, "StartDate", "BaslangicTarihi", "StartTime", "ProBasTarihi", "pro_baslangic_tarihi") ?? now,
                ExpirationDate: expirationDate ?? now,
                PluNo: ReadInt(row, "PluNo", "PLUNo", "Plu", "ProPluNo", "ProUrunPluNo", "plu_no", "sto_plu_no"),
                ProLimitAmount: ReadDouble(row, "ProLimitAmount", "LimitAmount", "LimitTutar", "ProLimitTutar", "pro_limit_amount"),
                DiscountRate: ReadDouble(row, "DiscountRate", "IndirimOrani", "Pro\u0130ndirimOrani", "ProOzelKodIndirimOrani", "ProUyg\u0130ndirimOrani", "discount_rate"),
                DiscountAmount: ReadDouble(row, "DiscountAmount", "IndirimTutari", "Pro\u0130ndirimTutari", "ProUyg\u0130ndirimTutari", "discount_amount"),
                QuantityToApplied: ReadInt(row, "QuantityToApplied", "AppliedQuantity", "UygulanacakMiktar", "ProUygulanacakAdet", "ProUygUrunAdedi"),
                ProductPluNo: ReadInt(row, "ProductPluNo", "ProductPLUNo", "UrunPluNo", "ProUygulanacakPluNo", "ProUygUrunPluNo", "ProUrunPluNo"),
                MaxQuantity: ReadInt(row, "MaxQuantity", "MaksimumMiktar", "ProMaxMiktar"),
                PluNoToGiven: ReadInt(row, "PluNoToGiven", "VerilecekPluNo", "ProVerilecekUrunPluNo"),
                QuantityToGiven: ReadInt(row, "QuantityToGiven", "VerilecekMiktar", "ProVerilecekUrunAdedi", "ProVerilecekUrunMiktari"),
                PriceToGiven: ReadDouble(row, "PriceToGiven", "VerilecekFiyat", "ProVerilecekUrunFiyati", "ProUrunFiyati", "ProStokGenelFiyat"),
                SpecialCodeReceived: ReadString(row, "SpecialCodeReceived", "AlinanOzelKod", "ProAlinanUrunOzelKodu"),
                QuantityToReceived: ReadInt(row, "QuantityToReceived", "AlinanMiktar", "ProAlinacakUrunAdedi", "ProAlinacakUrunlerinAdetleri"),
                DiscountSpecialCode: ReadString(row, "DiscountSpecialCode", "IndirimOzelKod", "Pro\u0130ndirimliUrunOzelKodu"),
                DiscountAmountUpToPrice: ReadDouble(row, "DiscountAmountUpToPrice", "FiyataKadarIndirimTutari", "ProFiyatKadarIndirimMiktari"),
                ProDescription: ReadString(row, "ProDescription", "Description", "Aciklama", "ProAciklama", "ProPromosyonAciklama", "ProFisMesaj1"),
                SpecialCodeToBeTaken: ReadString(row, "SpecialCodeToBeTaken", "AlinacakOzelKod", "ProAlinacakUrunOzelKodu"),
                QuantityToBeTaken: ReadInt(row, "QuantityToBeTaken", "AlinacakMiktar", "ProAlinacakUrunlerinAdetleri", "ProAlinacakUrunAdedi"),
                TotalAmountToBeTaken: ReadDouble(row, "TotalAmountToBeTaken", "AlinacakToplamTutar", "ProAlinacakUrunlerinToplamTutari"),
                DiscountAmountPriceToBeEarned: ReadDouble(row, "DiscountAmountPriceToBeEarned", "KazanilacakIndirimTutari", "ProKazanilacak\u0130ndirimTutari"),
                ProductGroupNoReceived: ReadString(row, "ProductGroupNoReceived", "AlinanUrunGrupNo", "ProVerilekUrunGrupNo", "ProGrupNo"),
                AmountToGiven: ReadDouble(row, "AmountToGiven", "VerilecekTutar", "ProVerilecekUrunTutari", "ProAlisverisToplami", "ProAraToplam", "ProToplamAlisveris"),
                DiscountRateToApplied: ReadDouble(row, "DiscountRateToApplied", "UygulanacakIndirimOrani", "ProUyg\u0130ndirimOrani", "ProOzelKodIndirimOrani"),
                ProTotalShipping: ReadDouble(row, "ProTotalShipping", "ToplamSira", "ProToplamAlisveris", "ProAlisverisToplami", "pro_total_shipping"),
                ProName: ReadString(row, "ProName", "Name", "PromosyonAdi", "ProBaslik", "ProPromosyonAciklama"),
                ProPMType: ReadString(row, "ProPMType", "PMType", "PmTipi", "ProPMTip")));
        }

        return promotions;
    }

    private async Task<IReadOnlyCollection<string>> TryListGibTaxNumbersAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ListGibTaxNumbersAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or SqlException)
        {
            logger.LogWarning(
                exception,
                "GIB tax numbers could not be read for EFATVNO.DAT. Continuing promo export with an empty EFATVNO.DAT file.");
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyCollection<string>> ListGibTaxNumbersAsync(CancellationToken cancellationToken)
    {
        var connectionString = GetRequiredConnectionString(
            "UyumConnection",
            "UYUMConnection",
            "UyumDbConnection");

        var rows = await ReadSqlRowsAsync(
            connectionString,
            "SELECT * FROM dbo.CarilerGib",
            cancellationToken);

        return rows
            .Select(row => ReadString(row, "VergiNumarasi", "TaxNumber", "TaxNo", "VergiNo", "vkn"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<ProductFileRow>> ListProductFileRowsAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from stock in mikroDbContext.STOKLARs.AsNoTracking()
            join price in mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs.AsNoTracking()
                on stock.sto_kod equals price.sfiyat_stokkod
            join barcode in mikroDbContext.BARKOD_TANIMLARIs.AsNoTracking()
                on stock.sto_kod equals barcode.bar_stokkodu
            where barcode.bar_kodu != null &&
                  (barcode.bar_birimpntr ?? 0) == 1 &&
                  price.sfiyat_deposirano == warehouseNo &&
                  (price.sfiyat_fiyati ?? 0d) > 0d &&
                  (price.sfiyat_listesirano ?? 0) == 1 &&
                  !(stock.sto_pasif_fl ?? false) &&
                  (stock.sto_satis_dursun ?? 0) == 0
            orderby stock.sto_plu_no, stock.sto_kod, barcode.bar_kodu
            select new ProductFileRow(
                stock.sto_kod,
                stock.sto_plu_no,
                barcode.bar_kodu ?? string.Empty,
                stock.sto_isim ?? string.Empty,
                price.sfiyat_fiyati ?? 0d,
                stock.sto_perakende_vergi ?? 0,
                stock.sto_birim1_ad ?? string.Empty,
                barcode.bar_icerigi == 1))
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .ToArray();
    }

    private string GetRequiredConnectionString(params string[] names)
    {
        foreach (var name in names)
        {
            var connectionString = configuration.GetConnectionString(name);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        throw new InvalidOperationException(
            $"Required connection string was not found. Expected one of: {string.Join(", ", names)}.");
    }

    private static async Task<IReadOnlyCollection<IReadOnlyDictionary<string, object?>>> ReadSqlRowsAsync(
        string connectionString,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 180
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = await reader.IsDBNullAsync(index, cancellationToken)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static bool RowsContainAnyColumn(
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyCollection<string> names) =>
        rows.Any(row => names.Any(row.ContainsKey));

    private static string ReadString(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static double? ReadDouble(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (value is decimal decimalValue)
            {
                return (double)decimalValue;
            }

            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTime? ReadDate(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string FormatAvailableColumns(
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> rows) =>
        rows.FirstOrDefault() is { } firstRow
            ? string.Join(", ", firstRow.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

    private string EnsureLocalDirectory(int warehouseNo, Guid jobId, string operationFolder)
    {
        var directory = OperationsExportPathResolver.ResolveOperationDirectory(
            options.Value,
            warehouseNo,
            jobId,
            operationFolder);

        try
        {
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidOperationException(
                $"Operations export directory is not writable by the application identity: {directory}. " +
                "Grant write/modify permission to the IIS application pool identity or configure OperationsExport:BasePath to a writable folder.",
                exception);
        }
    }

    private static string BuildAuthorizationLine(string prefix, IEnumerable<bool> flags)
    {
        var builder = new StringBuilder(prefix);
        builder.Append(',');

        var index = 0;
        foreach (var flag in flags)
        {
            index++;
            builder.Append(flag ? '*' : '-');

            if (index % 8 == 0)
            {
                builder.Append(',');
            }
        }

        return builder.ToString().TrimEnd(',');
    }

    private static string BuildP1Line(PromotionRow info) =>
        string.Join(
            ",",
            "P1",
            FormatPromoFlag(info),
            FormatAmount(info.ProLimitAmount),
            FormatInt(info.DiscountRate, 2),
            FormatAmount(info.DiscountAmount),
            FormatCustomerCode(info),
            "H",
            FormatPromoDates(info),
            FormatInt(info.PromotionCode, 5));

    private static string BuildP2Line(PromotionRow info)
    {
        var maxQuantity = !info.MaxQuantity.HasValue || info.MaxQuantity <= 0 ? "--" : "1";
        return string.Join(
            ",",
            "P2",
            FormatPromoFlag(info),
            FormatInt(info.QuantityToApplied, 2),
            FormatInt(info.ProductPluNo, 6),
            FormatInt(info.DiscountRate, 2),
            FormatAmount(info.DiscountAmount),
            maxQuantity,
            FormatCustomerCode(info),
            FormatPromoDates(info),
            FormatInt(info.PromotionCode, 5));
    }

    private static string BuildP3Line(PromotionRow info) =>
        string.Join(
            ",",
            "P3",
            FormatPromoFlag(info),
            FormatInt(info.PluNo, 6),
            FormatInt(info.MaxQuantity, 2),
            FormatInt(info.PluNoToGiven, 6),
            FormatInt(info.QuantityToGiven, 2),
            FormatAmount(info.PriceToGiven),
            FormatCustomerCode(info),
            FormatPromoDates(info),
            FormatInt(info.PromotionCode, 5));

    private static string BuildP8Line(PromotionRow info) =>
        string.Join(
            ",",
            "P8",
            FormatPromoFlag(info),
            FixedField(info.SpecialCodeReceived, 4),
            FormatInt(info.QuantityToReceived, 2),
            FixedField(info.DiscountSpecialCode, 4),
            FormatInt(info.DiscountAmountUpToPrice, 2),
            FormatCustomerCode(info),
            FormatPromoDates(info),
            FormatInt(info.PromotionCode, 5));

    private static string BuildP9FirstLine(PromotionRow info) =>
        string.Join(
            ",",
            "P9",
            FormatPromoFlag(info),
            FormatInt(info.PromotionCode, 4),
            FixedField(info.ProDescription, 25),
            FixedField(info.SpecialCodeToBeTaken, 4),
            FormatInt(info.QuantityToBeTaken, 2),
            FormatAmount(info.TotalAmountToBeTaken),
            FormatAmount(info.DiscountAmountPriceToBeEarned),
            FormatCustomerCode(info),
            FormatPromoDates(info));

    private static string BuildP9SecondLine(PromotionRow info) =>
        string.Join(
            ",",
            "P9",
            FormatPromoFlag(info),
            FormatInt(info.PromotionCode, 4),
            FixedField(info.ProDescription, 25),
            FixedField(info.ProductGroupNoReceived, 4),
            FormatInt(info.QuantityToBeTaken, 2),
            FormatAmount(info.AmountToGiven),
            FormatAmount(info.DiscountRateToApplied),
            FormatCustomerCode(info),
            FormatPromoDates(info));

    private static string BuildPmLine(PromotionRow info) =>
        string.Join(
            ",",
            "PM",
            FormatPromoFlag(info),
            FormatAmount(info.AmountToGiven),
            FixedField(info.ProName, 20),
            FixedField(info.SpecialCodeReceived, 4),
            FormatAmount(info.PriceToGiven),
            info.ProPMType,
            FormatCustomerCode(info),
            FormatPromoDates(info),
            FormatInt(info.PromotionCode, 5));

    private static string FormatPromoFlag(PromotionRow info) =>
        info.ProFlag == "0" ? "M" : "H";

    private static string FormatCustomerCode(PromotionRow info) =>
        string.IsNullOrWhiteSpace(info.PromotionCustomerCode)
            ? string.Empty
            : info.PromotionCustomerCode + "*";

    private static string FormatPromoDates(PromotionRow info) =>
        $"{info.StartDate:dd-MM-yyyy HH:mm:ss} {info.ExpirationDate:dd-MM-yyyy HH:mm:ss}";

    private static string FormatAmount(double? value, int width = 10) =>
        value.GetValueOrDefault()
            .ToString("F", CultureInfo.InvariantCulture)
            .PadLeft(width, '0');

    private static string FormatInt(double? value, int width) =>
        Convert.ToInt32(value.GetValueOrDefault(), CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(width, '0');

    private static string FormatInt(int? value, int width) =>
        value.GetValueOrDefault()
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(width, '0');

    private static string FormatInt(string value, int width) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')
            : FixedField(value, width);

    private static string FormatCashierName(string cashierName)
    {
        var normalizedName = cashierName.Trim();
        if (normalizedName.Contains(' ', StringComparison.Ordinal))
        {
            var parts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                normalizedName = $"{parts[0]} {parts[1][0]}";
            }
        }

        return normalizedName.Length > 20
            ? normalizedName[..20]
            : normalizedName;
    }

    private static string NormalizeAscii(string value) =>
        value
            .Replace('Ç', 'C')
            .Replace('Ğ', 'G')
            .Replace('İ', 'I')
            .Replace('Ö', 'O')
            .Replace('Ş', 'S')
            .Replace('Ü', 'U')
            .Replace('ç', 'c')
            .Replace('ğ', 'g')
            .Replace('ı', 'i')
            .Replace('ö', 'o')
            .Replace('ş', 's')
            .Replace('ü', 'u');

    private static string NormalizeTurkishAscii(string value) =>
        value
            .Replace('\u00C7', 'C')
            .Replace('\u011E', 'G')
            .Replace('\u0130', 'I')
            .Replace('\u00D6', 'O')
            .Replace('\u015E', 'S')
            .Replace('\u00DC', 'U')
            .Replace('\u00E7', 'c')
            .Replace('\u011F', 'g')
            .Replace('\u0131', 'i')
            .Replace('\u00F6', 'o')
            .Replace('\u015F', 's')
            .Replace('\u00FC', 'u');

    private static string FixedField(string value, int width)
    {
        var normalized = value.Trim();
        return normalized.Length > width
            ? normalized[..width]
            : normalized.PadRight(width, ' ');
    }

    private static string FormatScaleName(string productName, int width)
    {
        var normalized = NormalizeTurkishAscii(productName);
        return normalized.Length > 19
            ? normalized[..19].PadRight(width)
            : normalized.PadRight(width);
    }

    private static string FormatUnitName(string unitName)
    {
        var normalized = NormalizeTurkishAscii(unitName);
        return normalized.Length > 4
            ? normalized[..4]
            : normalized.PadRight(4, ' ');
    }

    private static string GeneratePluStringFromBarcode(string barcode, int pluNo)
    {
        if (!string.IsNullOrWhiteSpace(barcode) &&
            barcode.Length >= 4 &&
            int.TryParse(barcode[^4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPlu))
        {
            return parsedPlu.ToString(CultureInfo.InvariantCulture);
        }

        return pluNo.ToString(CultureInfo.InvariantCulture);
    }

    private static string GenerateCas16PriceString(double price) =>
        price
            .ToString("0.00", CultureInfo.InvariantCulture)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .PadLeft(6, '0')
            .PadRight(8, ' ');

    private static string GenerateCas500PriceString(double price) =>
        price
            .ToString("0.00", CultureInfo.InvariantCulture)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .PadLeft(8, '0');

    private static string BuildUncDirectory(string ipAddress, string folderPath)
    {
        var trimmedFolder = folderPath
            .Trim()
            .Trim('\\')
            .Trim('/');

        return $@"\\{ipAddress.Trim()}\{trimmedFolder}";
    }

    internal sealed record OperationGenerationResult(
        string Message,
        IReadOnlyCollection<GeneratedOperationFileDto> Files);

    private sealed record ScaleProductRow(
        string ProductCode,
        string ProductName,
        string Barcode,
        double Price,
        int PluNo,
        short ShelfLife);

    private sealed record GroupSpecialProductRow(
        int PluNo,
        string QualityControlCode);

    private sealed record PromotionRow(
        string PromotionCode,
        string PromotionType,
        string ProFlag,
        string PromotionCustomerCode,
        DateTime StartDate,
        DateTime ExpirationDate,
        int? PluNo,
        double? ProLimitAmount,
        double? DiscountRate,
        double? DiscountAmount,
        int? QuantityToApplied,
        int? ProductPluNo,
        int? MaxQuantity,
        int? PluNoToGiven,
        int? QuantityToGiven,
        double? PriceToGiven,
        string SpecialCodeReceived,
        int? QuantityToReceived,
        string DiscountSpecialCode,
        double? DiscountAmountUpToPrice,
        string ProDescription,
        string SpecialCodeToBeTaken,
        int? QuantityToBeTaken,
        double? TotalAmountToBeTaken,
        double? DiscountAmountPriceToBeEarned,
        string ProductGroupNoReceived,
        double? AmountToGiven,
        double? DiscountRateToApplied,
        double? ProTotalShipping,
        string ProName,
        string ProPMType);

    private sealed record ProductFileRow(
        string ProductCode,
        int PluNo,
        string Barcode,
        string ProductName,
        double Price,
        int RetailSaleTaxRate,
        string UnitName,
        bool BarcodeContent);

    private sealed record IndexedProductRow(int IndexNumber, ProductFileRow Product);
}
