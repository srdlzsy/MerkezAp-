using System.Globalization;
using System.Text.RegularExpressions;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Files;

public sealed class GetCashSummaryZReportTotalUseCase(FurpaDbContext furpaDbContext)
    : IGetCashSummaryZReportTotalUseCase
{
    public async Task<double> ExecuteAsync(
        ZReportValueRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.ZReportNo < 0 || request.CashNo <= 0)
        {
            throw new ArgumentException("Z report no and cash no must be valid.");
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        var branchNo = ParseBranchNoFromDocumentSerie(request.DocumentSerie);
        if (branchNo is null)
        {
            return -1d;
        }

        var branchDetail = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BranchNo == branchNo.Value, cancellationToken);

        if (branchDetail is null ||
            string.IsNullOrWhiteSpace(branchDetail.BranchIpAddress) ||
            string.IsNullOrWhiteSpace(branchDetail.PoskonFolderPath))
        {
            return -1d;
        }

        var directory = BuildUncDirectory(branchDetail.BranchIpAddress, branchDetail.PoskonFolderPath);
        var filePath = Path.Combine(directory, BuildZReportFileName(request.ZReportNo, request.CashNo));
        if (!File.Exists(filePath))
        {
            return -1d;
        }

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var totalLine = lines.FirstOrDefault(IsNetTurnoverLine);
        if (totalLine is null)
        {
            return -1d;
        }

        var starIndex = totalLine.LastIndexOf('*');
        if (starIndex < 0 || starIndex == totalLine.Length - 1)
        {
            return -1d;
        }

        return TryParseAmount(totalLine[(starIndex + 1)..], out var totalValue)
            ? totalValue
            : -1d;
    }

    private static int? ParseBranchNoFromDocumentSerie(string documentSerie)
    {
        var trimmed = documentSerie.Trim();
        if (!trimmed.StartsWith('F'))
        {
            return null;
        }

        var dotIndex = trimmed.IndexOf('.', StringComparison.Ordinal);
        var branchText = dotIndex > 1
            ? trimmed[1..dotIndex]
            : trimmed[1..];

        return int.TryParse(branchText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var branchNo)
            ? branchNo
            : null;
    }

    private static string BuildUncDirectory(string ipAddress, string folderPath)
    {
        var trimmedIpAddress = ipAddress
            .Trim()
            .Trim('\\')
            .Trim('/');
        var trimmedFolder = folderPath
            .Trim()
            .Replace('/', '\\')
            .Trim('\\')
            .Trim('/');

        return $@"\\{trimmedIpAddress}\{trimmedFolder}";
    }

    private static string BuildZReportFileName(int zReportNo, int cashNo) =>
        "ZRAP" +
        zReportNo.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0') +
        "." +
        cashNo.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

    private static bool IsNetTurnoverLine(string line)
    {
        var normalized = line
            .TrimStart()
            .Replace('İ', 'I')
            .Replace('ı', 'i');

        return normalized.StartsWith("NET CIRO", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains('*');
    }

    private static bool TryParseAmount(string input, out double value)
    {
        var match = Regex.Match(input, @"[-+]?[0-9\.,]+", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            value = 0d;
            return false;
        }

        var normalized = match.Value.Trim();

        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (normalized.Contains(','))
        {
            normalized = normalized.Replace(',', '.');
        }

        return double.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }
}
