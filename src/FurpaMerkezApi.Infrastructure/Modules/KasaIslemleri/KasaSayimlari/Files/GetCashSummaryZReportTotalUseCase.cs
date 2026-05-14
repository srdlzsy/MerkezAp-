using System.Globalization;
using System.Text.RegularExpressions;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Files;

public sealed class GetCashSummaryZReportTotalUseCase(IConfiguration configuration)
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

        var basePath = configuration["KasaSayimlari:ZReportBasePath"]
            ?? configuration["ZReports:BasePath"];

        if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
        {
            return -1d;
        }

        var matchingFile = Directory.EnumerateFiles(basePath, "*.ZRAP*", SearchOption.AllDirectories)
            .Select(path => new { Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) })
            .Where(item =>
                item.Name.Contains(request.ZReportNo.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) &&
                item.Name.Contains(request.CashNo.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) &&
                item.Name.Contains(request.DocumentSerie, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Path)
            .FirstOrDefault();

        if (matchingFile is null)
        {
            matchingFile = Directory.EnumerateFiles(basePath, "*.ZRAP*", SearchOption.AllDirectories)
                .FirstOrDefault(path =>
                    path.Contains(request.ZReportNo.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) &&
                    path.Contains(request.CashNo.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase));
        }

        if (matchingFile is null)
        {
            return -1d;
        }

        var content = await File.ReadAllTextAsync(matchingFile, cancellationToken);
        var match = Regex.Match(
            content,
            @"NET\s*CIRO\s*[:=]?\s*([0-9\.,]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return -1d;
        }

        return TryParseAmount(match.Groups[1].Value, out var totalValue)
            ? totalValue
            : -1d;
    }

    private static bool TryParseAmount(string input, out double value)
    {
        var normalized = input.Trim();

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
