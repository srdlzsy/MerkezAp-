using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBasim;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.EtiketBasim;

[ApiController]
[Authorize]
[Route("api/kasa-islemleri/etiket-basim")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class EtiketBasimController(IEtiketBasimService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "etiket-basim";
    private const string MenuName = "EtiketBasim";
    private const string ListPolicy = ModuleCode + "." + MenuCode + ".list";
    private const string DetailPolicy = ModuleCode + "." + MenuCode + ".detail";
    private const string CreatePolicy = ModuleCode + "." + MenuCode + ".create";
    private const string UpdatePolicy = ModuleCode + "." + MenuCode + ".update";
    private const string DeletePolicy = ModuleCode + "." + MenuCode + ".delete";
    private const string TransferPolicy = ModuleCode + "." + MenuCode + ".transfer";

    [HttpGet("suppliers")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<EtiketBasimSupplierSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<EtiketBasimSupplierSuggestionDto>>> SearchSuppliers(
        [FromQuery] EtiketBasimReferenceSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchSuppliersAsync(
            new EtiketBasimReferenceSearchRequest(request.Query, request.Take),
            cancellationToken));

    [HttpGet("suppliers/by-name")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(EtiketBasimSupplierSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EtiketBasimSupplierSuggestionDto>> GetSupplierByName(
        [FromQuery, Required] string name,
        CancellationToken cancellationToken) =>
        Ok(await service.GetSupplierByNameAsync(name, cancellationToken));

    [HttpGet("stocks")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<EtiketBasimStockSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<EtiketBasimStockSuggestionDto>>> SearchStocks(
        [FromQuery] EtiketBasimStockSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchStocksAsync(
            new EtiketBasimStockSearchRequest(request.Query, request.Prefix, request.Take),
            cancellationToken));

    [HttpGet("stocks/by-name")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(EtiketBasimStockSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EtiketBasimStockSuggestionDto>> GetStockByName(
        [FromQuery, Required] string name,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockByNameAsync(name, cancellationToken));

    [HttpGet("stocks/{stockCode}")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(EtiketBasimStockSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EtiketBasimStockSuggestionDto>> GetStock(
        string stockCode,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockByCodeAsync(stockCode, cancellationToken));

    [HttpGet("acceptance-records")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<EtiketBasimAcceptanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<EtiketBasimAcceptanceRecordDto>>> ListAcceptanceRecords(
        [FromQuery] EtiketBasimDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAcceptanceRecordsAsync(request.GetRequiredDate(), cancellationToken));

    [HttpGet("acceptance-records/{id:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(EtiketBasimAcceptanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EtiketBasimAcceptanceRecordDto>> GetAcceptanceRecord(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetAcceptanceRecordAsync(id, cancellationToken));

    [HttpPost("acceptance-records/calculate")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(EtiketBasimCalculationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<EtiketBasimCalculationDto> Calculate(
        [FromBody] EtiketBasimCalculationHttpRequest request) =>
        Ok(service.Calculate(request.ToApplicationRequest()));

    [HttpPost("acceptance-records")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(EtiketBasimAcceptanceRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EtiketBasimAcceptanceRecordDto>> CreateAcceptanceRecord(
        [FromBody] SaveEtiketBasimAcceptanceRecordHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateAcceptanceRecordAsync(request.ToApplicationRequest(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("acceptance-records/{id:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(EtiketBasimAcceptanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EtiketBasimAcceptanceRecordDto>> UpdateAcceptanceRecord(
        int id,
        [FromBody] SaveEtiketBasimAcceptanceRecordHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateAcceptanceRecordAsync(id, request.ToApplicationRequest(), cancellationToken));

    [HttpDelete("acceptance-records/{id:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAcceptanceRecord(
        int id,
        CancellationToken cancellationToken)
    {
        await service.DeleteAcceptanceRecordAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("acceptance-records/{id:int}/label")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(EtiketBasimLabelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EtiketBasimLabelDto>> GetLabel(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetLabelAsync(id, cancellationToken));

    [HttpPost("labels/preview")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(EtiketBasimLabelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<EtiketBasimLabelDto> PreviewLabel(
        [FromBody] SaveEtiketBasimAcceptanceRecordHttpRequest request) =>
        Ok(service.PreviewLabel(request.ToApplicationRequest()));

    [HttpPost("micro/goods-receipts")]
    [Authorize(Policy = TransferPolicy)]
    [ProducesResponseType(typeof(EtiketBasimMicroTransferUnavailableDto), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<EtiketBasimMicroTransferUnavailableDto> TransferToMicro(
        [FromBody] EtiketBasimMicroTransferHttpRequest request)
    {
        var response = service.ExplainMicroTransferAvailability(
            new EtiketBasimMicroTransferRequest(request.Date!.Value, request.SupplierCode));

        return StatusCode(StatusCodes.Status501NotImplemented, response);
    }

    [HttpGet("reports/received-products")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<EtiketBasimReceivedProductReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<EtiketBasimReceivedProductReportItemDto>>> GetReceivedProductsReport(
        [FromQuery] EtiketBasimDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetReceivedProductsReportAsync(request.GetRequiredDate(), cancellationToken));

    [HttpGet("reports/depot-stock")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<EtiketBasimDepotStockReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<EtiketBasimDepotStockReportItemDto>>> GetDepotStockReport(
        [FromQuery] EtiketBasimDepotStockReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo ?? 56, ListPolicy);
        return Ok(await service.GetDepotStockReportAsync(
            warehouseNo,
            request.Date?.Date ?? DateTime.Today,
            cancellationToken));
    }
}

public sealed class EtiketBasimReferenceSearchHttpRequest
{
    [Required]
    [MinLength(2)]
    public string? Query { get; init; }

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class EtiketBasimStockSearchHttpRequest
{
    public string? Query { get; init; }

    [StringLength(10)]
    public string? Prefix { get; init; } = "MNV";

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class EtiketBasimDateHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    public DateTime GetRequiredDate() =>
        Date?.Date ?? throw new ArgumentException("Date is required.", nameof(Date));
}

public sealed class EtiketBasimDepotStockReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? Date { get; init; }
}

public sealed class EtiketBasimCalculationHttpRequest
{
    public decimal GrossWeight { get; init; }

    public decimal CaseTare { get; init; }

    public int? CaseCount { get; init; }

    public decimal? PalletTare { get; init; }

    [StringLength(50)]
    public string? StockBarcode { get; init; }

    public EtiketBasimCalculationRequest ToApplicationRequest() =>
        new(GrossWeight, CaseTare, CaseCount, PalletTare, StockBarcode);
}

public sealed class SaveEtiketBasimAcceptanceRecordHttpRequest
{
    [Required]
    [StringLength(25)]
    public string SupplierCode { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string SupplierName { get; init; } = string.Empty;

    [StringLength(25)]
    public string? DocumentSeries { get; init; } = "MNV";

    [Required]
    [StringLength(25)]
    public string? DocumentNo { get; init; }

    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string StockName { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string StockBarcode { get; init; } = string.Empty;

    public decimal GrossWeight { get; init; }

    public decimal CaseTare { get; init; }

    public int? CaseCount { get; init; }

    public decimal? PalletTare { get; init; }

    [Required]
    [StringLength(100)]
    public string ReceivedBy { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string CaseType { get; init; } = string.Empty;

    public SaveEtiketBasimAcceptanceRecordRequest ToApplicationRequest() =>
        new(
            SupplierCode,
            SupplierName,
            DocumentSeries,
            DocumentNo,
            StockCode,
            StockName,
            StockBarcode,
            GrossWeight,
            CaseTare,
            CaseCount,
            PalletTare,
            ReceivedBy,
            CaseType);
}

public sealed class EtiketBasimMicroTransferHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Required]
    [StringLength(25)]
    public string SupplierCode { get; init; } = string.Empty;
}
