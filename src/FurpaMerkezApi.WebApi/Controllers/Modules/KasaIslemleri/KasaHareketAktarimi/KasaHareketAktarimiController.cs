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
            new KasaHareketMikroTransferRequest(request.Date!.Value, request.BranchNo, request.EndDate),
            cancellationToken));

    [HttpDelete("mikro")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(KasaHareketProcedureResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketProcedureResultDto>> DeleteFromMikro(
        [FromBody] KasaHareketMikroTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteMovementsFromMikroAsync(
            new KasaHareketMikroTransferRequest(request.Date!.Value, request.BranchNo, request.EndDate),
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

    [HttpGet("durum")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketTransferStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketTransferStatusDto>> TransferStatus(
        [FromQuery] KasaHareketTransferStatusHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetTransferStatusAsync(
            new KasaHareketTransferStatusRequest(
                request.StartDate!.Value,
                request.EndDate!.Value,
                branchNo,
                request.CashRegisterNo,
                request.CashRegisterNos),
            cancellationToken));
    }

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

    [HttpGet("satis-karsilastirma")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketSalesComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketSalesComparisonDto>> SalesComparison(
        [FromQuery] KasaHareketSalesComparisonHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetSalesComparisonAsync(
            new KasaHareketSalesComparisonRequest(request.Date!.Value, branchNo),
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

    [HttpGet("satis-mutabakat-raporu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketSatisMutabakatReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketSatisMutabakatReportDto>> SatisMutabakatReport(
        [FromQuery] KasaHareketSalesComparisonHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetSatisMutabakatReportAsync(
            new KasaHareketSatisMutabakatRequest(request.Date!.Value, branchNo),
            cancellationToken));
    }

    [HttpGet("satis-mutabakat-raporu/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SatisMutabakatReportExcel(
        [FromQuery] KasaHareketSalesComparisonHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var report = await service.GetSatisMutabakatReportAsync(
            new KasaHareketSatisMutabakatRequest(request.Date!.Value, branchNo),
            cancellationToken);

        return CsvFile(
            BuildSatisMutabakatCsv(report.Rows),
            $"satis-mutabakat-raporu-{request.Date!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("hareket-dosyasi/bilgi")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketDosyaBilgiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketDosyaBilgiDto>> GetHareketDosyaBilgi(
        [FromQuery] KasaHareketDosyaBilgiHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        if (branchNo is null or <= 0)
        {
            return ValidationProblem("SubeNo zorunludur.");
        }
        if (request.CashRegisterNo is null or < 0 or > 999)
        {
            return ValidationProblem("KasaNo 0 ile 999 arasında olmalıdır.");
        }

        return Ok(await service.GetHareketDosyaBilgisiAsync(
            new KasaHareketDosyaBilgiRequest(branchNo.Value, request.CashRegisterNo.Value, request.Date),
            cancellationToken));
    }

    [HttpPost("hareket-dosyasi/kopyala")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(KasaHareketDosyaKopyalaResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketDosyaKopyalaResultDto>> KopyalaHareketDosyasi(
        [FromBody] KasaHareketDosyaKopyalaHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, UpdatePolicy);
        if (branchNo is null or <= 0)
        {
            return ValidationProblem("SubeNo zorunludur.");
        }
        if (request.CashRegisterNo is null or < 0 or > 999)
        {
            return ValidationProblem("KasaNo 0 ile 999 arasında olmalıdır.");
        }

        return Ok(await service.KopyalaHareketDosyasiAsync(
            new KasaHareketDosyaKopyalaRequest(branchNo.Value, request.CashRegisterNo.Value, request.Date, request.Overwrite),
            cancellationToken));
    }

    [HttpGet("hareket-dosyasi/icerik")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketDosyaIcerikDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketDosyaIcerikDto>> OkuHareketDosyasiIcerigi(
        [FromQuery] KasaHareketDosyaIcerikHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        if (branchNo is null or <= 0)
        {
            return ValidationProblem("SubeNo zorunludur.");
        }
        if (request.CashRegisterNo is null or < 0 or > 999)
        {
            return ValidationProblem("KasaNo 0 ile 999 arasında olmalıdır.");
        }

        return Ok(await service.OkuHareketDosyasiIcerigiAsync(
            new KasaHareketDosyaIcerikRequest(branchNo.Value, request.CashRegisterNo.Value, request.Date, request.TargetLocation, request.MaxLines),
            cancellationToken));
    }

    [HttpGet("kasa-z-detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketKasaZDetayReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketKasaZDetayReportDto>> KasaZDetay(
        [FromQuery] KasaHareketKasaZDetayHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        if (branchNo is null or <= 0)
        {
            return ValidationProblem("SubeNo zorunludur.");
        }

        return Ok(await service.GetKasaZDetayReportAsync(
            new KasaHareketKasaZDetayRequest(request.Date!.Value, branchNo.Value),
            cancellationToken));
    }

    [HttpGet("birlik-kart-raporu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketBirlikKartReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketBirlikKartReportDto>> BirlikKartReport(
        [FromQuery] KasaHareketBirlikKartHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetBirlikKartReportAsync(
            new KasaHareketBirlikKartRequest(request.Date!.Value, branchNo),
            cancellationToken));
    }

    [HttpGet("birlik-kart-raporu/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BirlikKartReportExcel(
        [FromQuery] KasaHareketBirlikKartHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var report = await service.GetBirlikKartReportAsync(
            new KasaHareketBirlikKartRequest(request.Date!.Value, branchNo),
            cancellationToken);

        return CsvFile(
            BuildBirlikKartCsv(report.Rows),
            $"birlik-kart-raporu-{request.Date!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("belge-iptal-raporu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketFisIptalReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketFisIptalReportDto>> BelgeIptalReport(
        [FromQuery] KasaHareketFisIptalHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetFisIptalReportAsync(
            new KasaHareketFisIptalRequest(request.StartDate!.Value, request.EndDate!.Value, branchNo),
            cancellationToken));
    }

    [HttpGet("belge-iptal-raporu/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BelgeIptalReportExcel(
        [FromQuery] KasaHareketFisIptalHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var report = await service.GetFisIptalReportAsync(
            new KasaHareketFisIptalRequest(request.StartDate!.Value, request.EndDate!.Value, branchNo),
            cancellationToken);

        return CsvFile(
            BuildFisIptalCsv(report.Rows),
            $"belge-iptal-raporu-{request.StartDate!.Value:yyyyMMdd}-{request.EndDate!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("belge-iptal-raporu/detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketFisIptalDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketFisIptalDetailDto>> BelgeIptalDetail(
        [FromQuery] Guid faturaGuid,
        CancellationToken cancellationToken)
    {
        if (faturaGuid == Guid.Empty)
        {
            return ValidationProblem("faturaGuid parametresi zorunludur.");
        }

        return Ok(await service.GetFisIptalDetailAsync(faturaGuid, cancellationToken));
    }

    [HttpGet("gider-pusulasi-raporu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketGiderPusulasiReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketGiderPusulasiReportDto>> GiderPusulasiReport(
        [FromQuery] KasaHareketGiderPusulasiHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetGiderPusulasiReportAsync(
            new KasaHareketGiderPusulasiRequest(request.StartDate!.Value, request.EndDate!.Value, branchNo),
            cancellationToken));
    }

    [HttpGet("gider-pusulasi-raporu/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GiderPusulasiReportExcel(
        [FromQuery] KasaHareketGiderPusulasiHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var report = await service.GetGiderPusulasiReportAsync(
            new KasaHareketGiderPusulasiRequest(request.StartDate!.Value, request.EndDate!.Value, branchNo),
            cancellationToken);

        return CsvFile(
            BuildGiderPusulasiCsv(report.Rows),
            $"gider-pusulasi-raporu-{request.StartDate!.Value:yyyyMMdd}-{request.EndDate!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("gider-pusulasi-raporu/detay")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(KasaHareketGiderPusulasiDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketGiderPusulasiDetailDto>> GiderPusulasiDetail(
        [FromQuery] Guid faturaGuid,
        CancellationToken cancellationToken)
    {
        if (faturaGuid == Guid.Empty)
        {
            return ValidationProblem("faturaGuid parametresi zorunludur.");
        }

        return Ok(await service.GetGiderPusulasiDetailAsync(faturaGuid, cancellationToken));
    }

    [HttpGet("trendyol-raporu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketTrendyolReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketTrendyolReportDto>> TrendyolReport(
        [FromQuery] KasaHareketTrendyolHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, ListPolicy);
        return Ok(await service.GetTrendyolReportAsync(
            new KasaHareketTrendyolRequest(request.Date!.Value, branchNo),
            cancellationToken));
    }

    [HttpGet("trendyol-raporu/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TrendyolReportExcel(
        [FromQuery] KasaHareketTrendyolHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = User.ResolveWarehouseScopeForPolicy(request.BranchNo, DetailPolicy);
        var report = await service.GetTrendyolReportAsync(
            new KasaHareketTrendyolRequest(request.Date!.Value, branchNo),
            cancellationToken);

        return CsvFile(
            BuildTrendyolCsv(report.Rows),
            $"trendyol-raporu-{request.Date!.Value:yyyyMMdd}.csv");
    }

    [HttpGet("nakit-raporu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(KasaHareketNakitReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KasaHareketNakitReportDto>> NakitReport(
        [FromQuery] KasaHareketNakitHttpRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetNakitReportAsync(
            new KasaHareketNakitRequest(request.Date!.Value),
            cancellationToken));
    }

    [HttpGet("nakit-raporu/excel")]
    [Authorize(Policy = DetailPolicy)]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> NakitReportExcel(
        [FromQuery] KasaHareketNakitHttpRequest request,
        CancellationToken cancellationToken)
    {
        var report = await service.GetNakitReportAsync(
            new KasaHareketNakitRequest(request.Date!.Value),
            cancellationToken);

        return CsvFile(
            BuildNakitCsv(report.Rows),
            $"nakit-raporu-{request.Date!.Value:yyyyMMdd}.csv");
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

    private static string BuildSatisMutabakatCsv(
        IReadOnlyCollection<KasaHareketSatisMutabakatRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Tarih;SubeNo;SubeAdi;AraTabloTutar;GiderPusulasiTutar;GPSHaricTutar;AktarilanTutar;Nakit;KrediKarti;YemekKarti;HediyeKarti;ZRaporuTutari;GercekKasaFarki;Durum;FarkSebebi");

        foreach (var row in rows)
        {
            builder
                .Append(Csv(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(';')
                .Append(row.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(row.BranchName)).Append(';')
                .Append(FormatDecimal(row.AraTabloTutar)).Append(';')
                .Append(FormatDecimal(row.GiderPusulasiTutar)).Append(';')
                .Append(FormatDecimal(row.GPSHaricTutar)).Append(';')
                .Append(FormatDecimal(row.AktarilanTutar)).Append(';')
                .Append(FormatDecimal(row.Nakit)).Append(';')
                .Append(FormatDecimal(row.KrediKarti)).Append(';')
                .Append(FormatDecimal(row.YemekKarti)).Append(';')
                .Append(FormatDecimal(row.HediyeKarti)).Append(';')
                .Append(FormatDecimal(row.ZRaporuTutari)).Append(';')
                .Append(FormatDecimal(row.GercekKasaFarki)).Append(';')
                .Append(Csv(row.Durum)).Append(';')
                .Append(Csv(row.FarkSebebi))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildBirlikKartCsv(IReadOnlyCollection<KasaHareketBirlikKartRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SubeNo;SubeAdi;KasaMaliBellekKodu;KasaKodu;Tutar;CekNumarasi");
        foreach (var r in rows)
        {
            builder
                .Append(r.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.BranchName)).Append(';')
                .Append(Csv(r.KasaMaliBellekKodu)).Append(';')
                .Append(r.CashRegisterNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(FormatDecimal(r.Tutar)).Append(';')
                .Append(Csv(r.CekNumarasi))
                .AppendLine();
        }
        return builder.ToString();
    }

    private static string BuildFisIptalCsv(IReadOnlyCollection<KasaHareketFisIptalRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Bolge;SubeNo;SubeAdi;Tarih;Saat;ZNo;FisNo;KasaNo;Tutar;KasiyerAdi;KasiyerKodu;FaturaGuid");
        foreach (var r in rows)
        {
            builder
                .Append(Csv(r.RegionCode)).Append(';')
                .Append(r.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.BranchName)).Append(';')
                .Append(Csv(r.Tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(';')
                .Append(Csv(r.Saat)).Append(';')
                .Append(r.ZNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(r.FisNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(r.KasaNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(FormatDecimal(r.Tutar)).Append(';')
                .Append(Csv(r.CashierName)).Append(';')
                .Append(r.CashierCode.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(r.FaturaGuid.ToString())
                .AppendLine();
        }
        return builder.ToString();
    }

    private static string BuildGiderPusulasiCsv(IReadOnlyCollection<KasaHareketGiderPusulasiRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SubeNo;SubeAdi;Tarih;Saat;FisNo;KasaNo;KasiyerKodu;KasiyerAdi;Tutar;FaturaGuid");
        foreach (var r in rows)
        {
            builder
                .Append(r.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.BranchName)).Append(';')
                .Append(Csv(r.Tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(';')
                .Append(Csv(r.Saat)).Append(';')
                .Append(r.FisNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(r.KasaNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(r.CashierCode.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.CashierName)).Append(';')
                .Append(FormatDecimal(r.Tutar)).Append(';')
                .Append(r.FaturaGuid.ToString())
                .AppendLine();
        }
        return builder.ToString();
    }

    private static string BuildTrendyolCsv(IReadOnlyCollection<KasaHareketTrendyolRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SubeNo;SubeAdi;Tarih;FisNo;KasaNo;KasiyerAdi;Tutar");
        foreach (var r in rows)
        {
            builder
                .Append(r.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.BranchName)).Append(';')
                .Append(Csv(r.Tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).Append(';')
                .Append(r.FisNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(r.KasaNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.CashierName)).Append(';')
                .Append(FormatDecimal(r.Tutar))
                .AppendLine();
        }
        return builder.ToString();
    }

    private static string BuildNakitCsv(IReadOnlyCollection<KasaHareketNakitRowDto> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SubeNo;SubeAdi;NakitTutar;Aciklama");
        foreach (var r in rows)
        {
            builder
                .Append(r.BranchNo.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(r.BranchName)).Append(';')
                .Append(FormatDecimal(r.NakitTutar)).Append(';')
                .Append(Csv(r.Description))
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

    public DateTime? EndDate { get; init; }

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

public sealed class KasaHareketTransferStatusHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }

    public IReadOnlyCollection<int>? CashRegisterNos { get; init; }
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

public sealed class KasaHareketSalesComparisonHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
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

public sealed class KasaHareketDosyaBilgiHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required]
    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }

    public DateTime? Date { get; init; }
}

public sealed class KasaHareketDosyaKopyalaHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required]
    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }

    public DateTime? Date { get; init; }

    public bool Overwrite { get; init; } = true;
}

public sealed class KasaHareketDosyaIcerikHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [Required]
    [Range(0, 999)]
    public int? CashRegisterNo { get; init; }

    public DateTime? Date { get; init; }

    [StringLength(20)]
    public string? TargetLocation { get; init; } = "destination";

    [Range(0, 1000000)]
    public int? MaxLines { get; init; }
}

public sealed class KasaHareketKasaZDetayHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketBirlikKartHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketFisIptalHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketGiderPusulasiHttpRequest
{
    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketTrendyolHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class KasaHareketNakitHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }
}
