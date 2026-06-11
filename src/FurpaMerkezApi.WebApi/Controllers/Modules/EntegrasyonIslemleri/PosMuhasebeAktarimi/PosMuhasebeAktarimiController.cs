using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;

[ApiController]
[Route("api/entegrasyon-islemleri/pos-muhasebe-aktarimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PosMuhasebeAktarimiController(IPosMuhasebeAktarimiService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "entegrasyon-islemleri";
    private const string ModuleName = "EntegrasyonIslemleri";
    private const string MenuCode = "pos-muhasebe-aktarimi";
    private const string MenuName = "PosMuhasebeAktarimi";
    private const string ListPolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.list";
    private const string DetailPolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.detail";
    private const string CreatePolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.create";
    private const string UpdatePolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(PosAccountingOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingOverviewDto>> Overview(
        [FromQuery] PosAccountingDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetOverviewAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("z-raporlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ZReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ZReportListItemDto>>> ListZReports(
        [FromQuery] PosAccountingDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListZReportsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("z-raporlari/{totalId:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ZReportDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZReportDetailDto>> GetZReportDetail(
        int totalId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetZReportDetailAsync(totalId, cancellationToken));

    [HttpPost("z-raporlari/ice-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(PosAccountingImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingImportResultDto>> ImportZReports(
        [FromBody] ImportZReportsHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportZReportsAsync(
            new ImportZReportsRequest(
                request.WarehouseNo,
                request.BusinessDate,
                request.ReportPath,
                request.ImportMode,
                request.SourceCode,
                request.OverwriteExisting),
            cancellationToken));

    [HttpPost("z-raporlari/erpye-gonder")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(PosAccountingBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingBatchResultDto>> SendZReportsToErp(
        [FromBody] PosAccountingTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SendZReportsToErpAsync(
            new PosAccountingTransferRequest(request.ResolveDocumentIds(request.TotalIds), request.ContinueOnError),
            cancellationToken));

    [HttpDelete("z-raporlari")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(PosAccountingBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingBatchResultDto>> DeleteZReports(
        [FromBody] PosAccountingDeleteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteZReportsAsync(
            new PosAccountingDeleteRequest(request.ResolveDocumentIds(request.TotalIds)),
            cancellationToken));

    [HttpGet("pos-faturalar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BranchInvoiceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BranchInvoiceListItemDto>>> ListPosInvoices(
        [FromQuery] PosAccountingDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListPosInvoicesAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("pos-faturalar/{invoiceId:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(BranchInvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchInvoiceDetailDto>> GetPosInvoiceDetail(
        int invoiceId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetPosInvoiceDetailAsync(invoiceId, cancellationToken));

    [HttpPost("pos-faturalar/ice-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(PosAccountingImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingImportResultDto>> ImportPosInvoices(
        [FromBody] ImportPosDocumentsHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportPosInvoicesAsync(
            new ImportPosDocumentsRequest(
                request.WarehouseNo,
                request.GetBusinessDate(),
                request.IncludePreviouslyImported,
                request.OverwriteExisting),
            cancellationToken));

    [HttpPost("pos-faturalar/erpye-gonder")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(PosAccountingBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingBatchResultDto>> SendPosInvoicesToErp(
        [FromBody] PosAccountingTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SendPosInvoicesToErpAsync(
            new PosAccountingTransferRequest(request.ResolveDocumentIds(request.InvoiceIds), request.ContinueOnError),
            cancellationToken));

    [HttpPut("pos-faturalar/{invoiceId:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(BranchInvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchInvoiceDetailDto>> UpdatePosInvoice(
        int invoiceId,
        [FromBody] UpdatePosAccountingDocumentHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdatePosInvoiceAsync(
            new UpdatePosInvoiceRequest(
                invoiceId,
                ParseOptionalInt(request.DocumentNo, nameof(request.DocumentNo)),
                request.CustomerTaxNo,
                request.PaymentType),
            cancellationToken));

    [HttpDelete("pos-faturalar")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(PosAccountingBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingBatchResultDto>> DeletePosInvoices(
        [FromBody] PosAccountingDeleteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeletePosInvoicesAsync(
            new PosAccountingDeleteRequest(request.ResolveDocumentIds(request.InvoiceIds)),
            cancellationToken));

    [HttpGet("gider-pusulalari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ExpenseNoteListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ExpenseNoteListItemDto>>> ListExpenseNotes(
        [FromQuery] PosAccountingDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListExpenseNotesAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("gider-pusulalari/{expenseId:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ExpenseNoteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseNoteDetailDto>> GetExpenseNoteDetail(
        int expenseId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetExpenseNoteDetailAsync(expenseId, cancellationToken));

    [HttpPost("gider-pusulalari/ice-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(PosAccountingImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingImportResultDto>> ImportExpenseNotes(
        [FromBody] ImportPosDocumentsHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ImportExpenseNotesAsync(
            new ImportPosDocumentsRequest(
                request.WarehouseNo,
                request.GetBusinessDate(),
                request.IncludePreviouslyImported,
                request.OverwriteExisting),
            cancellationToken));

    [HttpPost("gider-pusulalari/erpye-gonder")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(PosAccountingBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingBatchResultDto>> SendExpenseNotesToErp(
        [FromBody] PosAccountingTransferHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SendExpenseNotesToErpAsync(
            new PosAccountingTransferRequest(request.ResolveDocumentIds(request.ExpenseIds), request.ContinueOnError),
            cancellationToken));

    [HttpPut("gider-pusulalari/{expenseId:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ExpenseNoteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseNoteDetailDto>> UpdateExpenseNote(
        int expenseId,
        [FromBody] UpdatePosAccountingDocumentHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateExpenseNoteAsync(
            new UpdateExpenseNoteRequest(
                expenseId,
                request.BranchNo,
                request.DocumentNo,
                request.PaymentType),
            cancellationToken));

    [HttpDelete("gider-pusulalari")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(PosAccountingBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PosAccountingBatchResultDto>> DeleteExpenseNotes(
        [FromBody] PosAccountingDeleteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.DeleteExpenseNotesAsync(
            new PosAccountingDeleteRequest(request.ResolveDocumentIds(request.ExpenseIds)),
            cancellationToken));

    [HttpGet("kasa-eslemeleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterBranchMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterBranchMappingDto>>> ListCashRegisterMappings(
        [FromQuery] CashRegisterBranchMappingListHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListCashRegisterMappingsAsync(
            new CashRegisterBranchMappingFilterRequest(request.BranchNo, request.CashRegisterNo),
            cancellationToken));

    [HttpPost("kasa-eslemeleri")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CashRegisterBranchMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterBranchMappingDto>> CreateCashRegisterMapping(
        [FromBody] CashRegisterBranchMappingHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.CreateCashRegisterMappingAsync(
            new UpsertCashRegisterBranchMappingRequest(
                null,
                request.CashRegisterNo,
                request.BranchNo!.Value),
            cancellationToken));

    [HttpPut("kasa-eslemeleri/{mappingId:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CashRegisterBranchMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashRegisterBranchMappingDto>> UpdateCashRegisterMapping(
        int mappingId,
        [FromBody] CashRegisterBranchMappingHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateCashRegisterMappingAsync(
            new UpsertCashRegisterBranchMappingRequest(
                mappingId,
                request.CashRegisterNo,
                request.BranchNo!.Value),
            cancellationToken));

    private static int? ParseOptionalInt(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"{parameterName} must be a valid integer.", parameterName);
    }
}

public sealed class PosAccountingDateRangeHttpRequest
{
    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public bool OnlyPending { get; init; } = true;

    public PosAccountingFilterRequest ToApplicationRequest() =>
        new(StartDate, EndDate, WarehouseNo, OnlyPending);
}

public sealed class ImportZReportsHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? BusinessDate { get; init; }

    [StringLength(400)]
    public string? ReportPath { get; init; }

    [StringLength(50)]
    public string? ImportMode { get; init; }

    [StringLength(100)]
    public string? SourceCode { get; init; }

    public bool OverwriteExisting { get; init; }
}

public sealed class ImportPosDocumentsHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? BusinessDate { get; init; }

    public DateTime? DateToGet { get; init; }

    public bool IncludePreviouslyImported { get; init; }

    public bool OverwriteExisting { get; init; }

    public DateTime GetBusinessDate() =>
        (BusinessDate ?? DateToGet)?.Date
        ?? throw new ArgumentException("BusinessDate or DateToGet is required.", nameof(BusinessDate));
}

public sealed class PosAccountingTransferHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? DocumentIds { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? TotalIds { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? InvoiceIds { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? ExpenseIds { get; init; }

    public bool ContinueOnError { get; init; } = true;

    public IReadOnlyCollection<int> ResolveDocumentIds(IReadOnlyCollection<int>? preferredIds)
    {
        var ids = preferredIds is { Count: > 0 }
            ? preferredIds
            : DocumentIds;

        return ids is { Count: > 0 }
            ? ids
            : throw new ArgumentException("At least one document id is required.", nameof(DocumentIds));
    }
}

public sealed class PosAccountingDeleteHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? DocumentIds { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? TotalIds { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? InvoiceIds { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<int>? ExpenseIds { get; init; }

    public IReadOnlyCollection<int> ResolveDocumentIds(IReadOnlyCollection<int>? preferredIds)
    {
        var ids = preferredIds is { Count: > 0 }
            ? preferredIds
            : DocumentIds;

        return ids is { Count: > 0 }
            ? ids
            : throw new ArgumentException("At least one document id is required.", nameof(DocumentIds));
    }
}

public sealed class UpdatePosAccountingDocumentHttpRequest
{
    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(50)]
    public string? CustomerTaxNo { get; init; }

    [StringLength(30)]
    public string? PaymentType { get; init; }

    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [StringLength(250)]
    public string? Description { get; init; }
}

public sealed class CashRegisterBranchMappingListHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [StringLength(40)]
    public string? CashRegisterNo { get; init; }
}

public sealed class CashRegisterBranchMappingHttpRequest
{
    [Required]
    [StringLength(40, MinimumLength = 1)]
    public string CashRegisterNo { get; init; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }

    [StringLength(100)]
    public string? BranchName { get; init; }

    [StringLength(100)]
    public string? Description { get; init; }
}
