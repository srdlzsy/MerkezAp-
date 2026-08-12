using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaHareketAktarimi;

[ApiController]
[Route("api/kasa-islemleri/kasa-hareket-aktarimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaHareketAktarimiController(IKasaHareketAktarimiService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kasa-hareket-aktarimi";
    private const string MenuName = "KasaHareketAktarimi";
    private const string ListPolicy = "kasa-islemleri.kasa-hareket-aktarimi.list";
    private const string DetailPolicy = "kasa-islemleri.kasa-hareket-aktarimi.detail";
    private const string CreatePolicy = "kasa-islemleri.kasa-hareket-aktarimi.create";
    private const string UpdatePolicy = "kasa-islemleri.kasa-hareket-aktarimi.update";

    [HttpGet("subeler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaHareketBranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<KasaHareketBranchDto>>> ListBranches(
        CancellationToken cancellationToken) =>
        Ok(await service.ListBranchesAsync(cancellationToken));

    [HttpGet("subeler/{branchNo:int}/kasalar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaHareketCashRegisterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<KasaHareketCashRegisterDto>>> ListCashRegisters(
        int branchNo,
        CancellationToken cancellationToken) =>
        Ok(await service.ListCashRegistersAsync(branchNo, cancellationToken));

    [HttpPost("hareketler/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketImportResultDto>> ImportMovements(
        [FromBody] KasaHareketImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportMovementsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPost("iptal-belgeleri/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketImportResultDto>> ImportCancelMovements(
        [FromBody] KasaHareketImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportCancelMovementsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPost("zamanli-aktarim/calistir")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketImportResultDto>> RunScheduledImport(
        [FromBody] KasaHareketScheduledImportHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.RunScheduledImportAsync(
            new KasaHareketScheduledImportRequest(
                request.Date,
                request.AddDay,
                request.FileRootPath,
                request.SkipExisting,
                request.DryRun),
            cancellationToken));

    [HttpDelete("staging")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> DeleteStaging(
        [FromBody] KasaHareketDeleteStagingHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteStagingMovementsAsync(
            new KasaHareketDeleteStagingRequest(
                request.Date!.Value,
                request.BranchNo,
                request.CashRegisterNo),
            cancellationToken));

    [HttpPost("mikro/aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> TransferToMikro(
        [FromBody] KasaHareketMikroTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.TransferMovementsToMikroAsync(
            new KasaHareketMikroTransferRequest(request.Date!.Value, request.BranchNo),
            cancellationToken));

    [HttpDelete("mikro")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> DeleteFromMikro(
        [FromBody] KasaHareketMikroTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteMovementsFromMikroAsync(
            new KasaHareketMikroTransferRequest(request.Date!.Value, request.BranchNo),
            cancellationToken));

    [HttpPost("mikro/aralik-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> TransferRangeToMikro(
        [FromBody] KasaHareketMikroTransferRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.TransferMovementRangeToMikroAsync(
            new KasaHareketMikroTransferRangeRequest(request.StartDate!.Value, request.EndDate!.Value),
            cancellationToken));

    [HttpGet("rapor")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<KasaHareketReportRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<KasaHareketReportRowDto>>> Report(
        [FromQuery] KasaHareketReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        return Ok(await service.GetReportAsync(
            new KasaHareketReportRequest(request.Date!.Value, branchNo, request.CashRegisterNo),
            cancellationToken));
    }

    [HttpGet("rapor/ozet")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketReportSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketReportSummaryDto>> ReportSummary(
        [FromQuery] KasaHareketReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        return Ok(await service.GetReportSummaryAsync(
            new KasaHareketReportRequest(request.Date!.Value, branchNo, request.CashRegisterNo),
            cancellationToken));
    }

    [HttpGet("rapor/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportExcel(
        [FromQuery] KasaHareketReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var rows = await service.GetReportAsync(
            new KasaHareketReportRequest(request.Date!.Value, branchNo, request.CashRegisterNo),
            cancellationToken);

        return CsvFile(
            BuildReportCsv(rows),
            $"kasa-hareket-rapor-{request.Date!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("icmal-karsilastirma")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketCashSummaryComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketCashSummaryComparisonDto>> CashSummaryComparison(
        [FromQuery] KasaHareketCashSummaryComparisonHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        return Ok(await service.GetCashSummaryComparisonAsync(
            new KasaHareketCashSummaryComparisonRequest(
                request.Date!.Value,
                branchNo,
                request.CashRegisterNo,
                request.Tolerance ?? 0.01m),
            cancellationToken));
    }

    [HttpGet("icmal-karsilastirma/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CashSummaryComparisonExcel(
        [FromQuery] KasaHareketCashSummaryComparisonHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var comparison = await service.GetCashSummaryComparisonAsync(
            new KasaHareketCashSummaryComparisonRequest(
                request.Date!.Value,
                branchNo,
                request.CashRegisterNo,
                request.Tolerance ?? 0.01m),
            cancellationToken);

        return CsvFile(
            BuildCashSummaryComparisonCsv(comparison.Rows),
            $"kasa-hareket-icmal-karsilastirma-{request.Date!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("icmal-karsilastirma/detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketDetailDto>> CashSummaryComparisonDetail(
        [FromQuery] KasaHareketDetailHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        return Ok(await service.GetDetailAsync(
            new KasaHareketDetailRequest(
                request.Date!.Value,
                branchNo!.Value,
                request.CashRegisterNo!.Value,
                request.ReceiptTake ?? 500),
            cancellationToken));
    }

    private static FileContentResult CsvFile(string csv, string fileName)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csv);
        var bytes = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, bytes, preamble.Length, content.Length);

        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };
    }

    private static string BuildReportCsv(IReadOnlyCollection<KasaHareketReportRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Tarih;SubeNo;SubeAdi;KasaNo;NetTutar;GiderPusulasi;Cek;ZRaporu");

        foreach (var row in rows)
        {
            builder
                .Append(Csv(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(';')
                .Append(row.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(row.BranchName)).Append(';')
                .Append(row.CashRegisterNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(FormatDecimal(row.NetAmount)).Append(';')
                .Append(FormatDecimal(row.Expense)).Append(';')
                .Append(FormatDecimal(row.CheckAmount)).Append(';')
                .Append(FormatDecimal(row.Difference))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildCashSummaryComparisonCsv(
        IReadOnlyCollection<KasaHareketCashSummaryComparisonRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Tarih;SubeNo;SubeAdi;KasaNo;AktarimNetTutar;AktarimGiderPusulasi;AktarimCek;AktarimZRaporu;IcmalToplam;IcmalBelgeSayisi;Fark;Durum");

        foreach (var row in rows)
        {
            builder
                .Append(Csv(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(';')
                .Append(row.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(row.BranchName)).Append(';')
                .Append(row.CashRegisterNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(FormatDecimal(row.MovementNetAmount)).Append(';')
                .Append(FormatDecimal(row.MovementExpense)).Append(';')
                .Append(FormatDecimal(row.MovementCheckAmount)).Append(';')
                .Append(FormatDecimal(row.MovementZReportAmount)).Append(';')
                .Append(FormatDecimal(row.CashSummaryAmount)).Append(';')
                .Append(row.CashSummaryDocumentCount.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(FormatDecimal(row.DifferenceAmount)).Append(';')
                .Append(Csv(row.StatusName))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Csv(string value)
    {
        var normalized = value.Replace("\"", "\"\"");
        return normalized.Contains(';') || normalized.Contains('"') || normalized.Contains('\r') || normalized.Contains('\n')
            ? $"\"{normalized}\""
            : normalized;
    }
}

public sealed class KasaHareketImportHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    public IReadOnlyCollection<int>? Branches { get; init; }

    public IReadOnlyCollection<int>? CashRegisters { get; init; }

    [StringLength(400)]
    public string? FileRootPath { get; init; }

    public bool SkipExisting { get; init; } = true;

    public bool DryRun { get; init; }

    public KasaHareketImportRequest ToApplicationRequest() =>
        new(
            StartDate!.Value,
            EndDate!.Value,
            Branches,
            CashRegisters,
            FileRootPath,
            SkipExisting,
            DryRun);
}

public sealed class KasaHareketScheduledImportHttpRequest
{
    public DateTime? Date { get; init; }

    [Range(-30, 30)]
    public int? AddDay { get; init; }

    [StringLength(400)]
    public string? FileRootPath { get; init; }

    public bool SkipExisting { get; init; } = true;

    public bool DryRun { get; init; }
}

public sealed class KasaHareketDeleteStagingHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }
}

public sealed class KasaHareketMikroTransferHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketMikroTransferRangeHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }
}

public sealed class KasaHareketReportHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }
}

public sealed class KasaHareketCashSummaryComparisonHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal? Tolerance { get; init; }
}

public sealed class KasaHareketDetailHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required]
    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }

    [Range(0, 5000)]
    public int? ReceiptTake { get; init; } = 500;
}
