using System.Globalization;
using System.Text;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

internal sealed class OperationsFileGenerationService(
    MikroDbContext mikroDbContext,
    FurpaDbContext furpaDbContext,
    IOptions<OperationsExportOptions> options,
    ILogger<OperationsFileGenerationService> logger)
{
    private static readonly Encoding FileEncoding = Encoding.GetEncoding(1254);

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
                    product.Barcode.PadRight(20, ' ') +
                    NormalizeAscii(product.ProductName).PadRight(20, ' ') +
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
                    NormalizeAscii(cashierName).PadRight(20, ' ') +
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
            where !(stock.sto_pasif_fl ?? false) &&
                  (stock.sto_satis_dursun ?? 0) == 0 &&
                  (stock.sto_kasa_tarti_fl ?? false)
            let barcode = mikroDbContext.BARKOD_TANIMLARIs
                .AsNoTracking()
                .Where(item => item.bar_stokkodu == stock.sto_kod)
                .OrderByDescending(item => item.bar_master ?? false)
                .ThenBy(item => item.bar_birimpntr ?? 0)
                .ThenByDescending(item => item.bar_create_date)
                .Select(item => item.bar_kodu)
                .FirstOrDefault()
            let price = mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs
                .AsNoTracking()
                .Where(item =>
                    item.sfiyat_stokkod == stock.sto_kod &&
                    item.sfiyat_deposirano == warehouseNo &&
                    item.sfiyat_birim_pntr == 1)
                .OrderBy(item => item.sfiyat_listesirano ?? int.MaxValue)
                .ThenByDescending(item => item.sfiyat_lastup_date ?? item.sfiyat_create_date)
                .Select(item => item.sfiyat_fiyati)
                .FirstOrDefault()
            where barcode != null
            orderby stock.sto_plu_no, stock.sto_kod
            select new ScaleProductRow(
                stock.sto_kod,
                stock.sto_isim ?? string.Empty,
                barcode ?? string.Empty,
                price ?? 0d,
                stock.sto_plu_no,
                stock.sto_toplam_rafomru ?? 0))
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<ProductFileRow>> ListProductFileRowsAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from stock in mikroDbContext.STOKLARs.AsNoTracking()
            where !(stock.sto_pasif_fl ?? false)
            let barcodeRow = mikroDbContext.BARKOD_TANIMLARIs
                .AsNoTracking()
                .Where(item => item.bar_stokkodu == stock.sto_kod)
                .OrderByDescending(item => item.bar_master ?? false)
                .ThenBy(item => item.bar_birimpntr ?? 0)
                .ThenByDescending(item => item.bar_create_date)
                .Select(item => new
                {
                    item.bar_kodu,
                    item.bar_icerigi
                })
                .FirstOrDefault()
            let price = mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs
                .AsNoTracking()
                .Where(item =>
                    item.sfiyat_stokkod == stock.sto_kod &&
                    item.sfiyat_deposirano == warehouseNo &&
                    item.sfiyat_birim_pntr == 1)
                .OrderBy(item => item.sfiyat_listesirano ?? int.MaxValue)
                .ThenByDescending(item => item.sfiyat_lastup_date ?? item.sfiyat_create_date)
                .Select(item => item.sfiyat_fiyati)
                .FirstOrDefault()
            where barcodeRow != null && barcodeRow.bar_kodu != null
            select new ProductFileRow(
                stock.sto_kod,
                stock.sto_plu_no,
                barcodeRow!.bar_kodu ?? string.Empty,
                stock.sto_isim ?? string.Empty,
                price ?? 0d,
                Convert.ToByte(stock.sto_perakende_vergi ?? 0),
                stock.sto_birim1_ad ?? string.Empty,
                barcodeRow.bar_icerigi == 1))
            .ToArrayAsync(cancellationToken);

        return rows
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .ToArray();
    }

    private string EnsureLocalDirectory(int warehouseNo, Guid jobId, string operationFolder)
    {
        var basePath = string.IsNullOrWhiteSpace(options.Value.BasePath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "OperationsExports")
            : options.Value.BasePath.Trim();

        var directory = Path.Combine(basePath, warehouseNo.ToString(CultureInfo.InvariantCulture), operationFolder, jobId.ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
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

    private static string FormatScaleName(string productName, int width)
    {
        var normalized = NormalizeAscii(productName);
        return normalized.Length > 19
            ? normalized[..19].PadRight(width)
            : normalized.PadRight(width);
    }

    private static string FormatUnitName(string unitName)
    {
        var normalized = NormalizeAscii(unitName);
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

    private sealed record ProductFileRow(
        string ProductCode,
        int PluNo,
        string Barcode,
        string ProductName,
        double Price,
        byte RetailSaleTaxRate,
        string UnitName,
        bool BarcodeContent);

    private sealed record IndexedProductRow(int IndexNumber, ProductFileRow Product);
}
