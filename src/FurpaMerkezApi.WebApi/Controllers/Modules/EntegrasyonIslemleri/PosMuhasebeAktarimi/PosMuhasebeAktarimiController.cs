using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;

[ApiController]
[Route("api/entegrasyon-islemleri/pos-muhasebe-aktarimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PosMuhasebeAktarimiController
    : ModuleMenuControllerBase
{
    private const string ModuleCode = "entegrasyon-islemleri";
    private const string ModuleName = "EntegrasyonIslemleri";
    private const string MenuCode = "pos-muhasebe-aktarimi";
    private const string MenuName = "PosMuhasebeAktarimi";
    private const string ListPolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.list";
    private const string DetailPolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.detail";
    private const string CreatePolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.create";
    private const string UpdatePolicy = "entegrasyon-islemleri.pos-muhasebe-aktarimi.update";

    public PosMuhasebeAktarimiController()
        : base(ModuleCode, ModuleName, MenuCode, MenuName)
    {
    }

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Overview() =>
        ListNotImplemented(ListPolicy);

    [HttpGet("z-raporlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ListZReports(
        [FromQuery] PosAccountingDateRangeHttpRequest request) =>
        ListNotImplemented(ListPolicy);

    [HttpGet("z-raporlari/{reportId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> GetZReportDetail(Guid reportId) =>
        DetailNotImplemented(DetailPolicy, reportId.ToString());

    [HttpPost("z-raporlari/ice-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ImportZReports(
        [FromBody] ImportZReportsHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPost("z-raporlari/erpye-gonder")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> SendZReportsToErp(
        [FromBody] PosAccountingTransferHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpDelete("z-raporlari")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> DeleteZReports(
        [FromBody] PosAccountingDeleteHttpRequest request) =>
        UpdateNotImplemented(UpdatePolicy, "z-raporlari");

    [HttpGet("pos-faturalar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ListPosInvoices(
        [FromQuery] PosAccountingDateRangeHttpRequest request) =>
        ListNotImplemented(ListPolicy);

    [HttpGet("pos-faturalar/{invoiceId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> GetPosInvoiceDetail(Guid invoiceId) =>
        DetailNotImplemented(DetailPolicy, invoiceId.ToString());

    [HttpPost("pos-faturalar/ice-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ImportPosInvoices(
        [FromBody] ImportPosDocumentsHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPost("pos-faturalar/erpye-gonder")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> SendPosInvoicesToErp(
        [FromBody] PosAccountingTransferHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPut("pos-faturalar/{invoiceId:guid}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> UpdatePosInvoice(
        Guid invoiceId,
        [FromBody] UpdatePosAccountingDocumentHttpRequest request) =>
        UpdateNotImplemented(UpdatePolicy, invoiceId.ToString());

    [HttpDelete("pos-faturalar")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> DeletePosInvoices(
        [FromBody] PosAccountingDeleteHttpRequest request) =>
        UpdateNotImplemented(UpdatePolicy, "pos-faturalar");

    [HttpGet("gider-pusulalari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ListExpenseNotes(
        [FromQuery] PosAccountingDateRangeHttpRequest request) =>
        ListNotImplemented(ListPolicy);

    [HttpGet("gider-pusulalari/{expenseId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> GetExpenseNoteDetail(Guid expenseId) =>
        DetailNotImplemented(DetailPolicy, expenseId.ToString());

    [HttpPost("gider-pusulalari/ice-aktar")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ImportExpenseNotes(
        [FromBody] ImportPosDocumentsHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPost("gider-pusulalari/erpye-gonder")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> SendExpenseNotesToErp(
        [FromBody] PosAccountingTransferHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPut("gider-pusulalari/{expenseId:guid}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> UpdateExpenseNote(
        Guid expenseId,
        [FromBody] UpdatePosAccountingDocumentHttpRequest request) =>
        UpdateNotImplemented(UpdatePolicy, expenseId.ToString());

    [HttpDelete("gider-pusulalari")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> DeleteExpenseNotes(
        [FromBody] PosAccountingDeleteHttpRequest request) =>
        UpdateNotImplemented(UpdatePolicy, "gider-pusulalari");

    [HttpGet("kasa-eslemeleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> ListCashRegisterMappings(
        [FromQuery] CashRegisterBranchMappingListHttpRequest request) =>
        ListNotImplemented(ListPolicy);

    [HttpPost("kasa-eslemeleri")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> CreateCashRegisterMapping(
        [FromBody] CashRegisterBranchMappingHttpRequest request) =>
        CreateNotImplemented(CreatePolicy);

    [HttpPut("kasa-eslemeleri/{mappingId:guid}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> UpdateCashRegisterMapping(
        Guid mappingId,
        [FromBody] CashRegisterBranchMappingHttpRequest request) =>
        UpdateNotImplemented(UpdatePolicy, mappingId.ToString());
}

public sealed class PosAccountingDateRangeHttpRequest
{
    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public bool OnlyPending { get; init; } = true;
}

public sealed class ImportZReportsHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? BusinessDate { get; init; }

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

    [Required]
    public DateTime? BusinessDate { get; init; }

    public bool IncludePreviouslyImported { get; init; }

    public bool OverwriteExisting { get; init; }
}

public sealed class PosAccountingTransferHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<Guid> DocumentIds { get; init; } = Array.Empty<Guid>();

    public bool ContinueOnError { get; init; } = true;
}

public sealed class PosAccountingDeleteHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<Guid> DocumentIds { get; init; } = Array.Empty<Guid>();
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
