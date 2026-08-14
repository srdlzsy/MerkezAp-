using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

[ApiController]
[Authorize]
[Route("api/kasa-islemleri/manav-mal-kabul-etiket")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class ManavMalKabulVeEtiketController(IManavMalKabulVeEtiketService service)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "manav-mal-kabul-etiket";
    private const string MenuName = "ManavMalKabulVeEtiket";
    private const string ListPolicy = ModuleCode + "." + MenuCode + ".list";
    private const string DetailPolicy = ModuleCode + "." + MenuCode + ".detail";
    private const string CreatePolicy = ModuleCode + "." + MenuCode + ".create";
    private const string UpdatePolicy = ModuleCode + "." + MenuCode + ".update";
    private const string DeletePolicy = ModuleCode + "." + MenuCode + ".delete";
    private const string TransferPolicy = ModuleCode + "." + MenuCode + ".transfer";

    [HttpGet("suppliers")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketSupplierSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketSupplierSuggestionDto>>> SearchSuppliers(
        [FromQuery] ManavMalKabulVeEtiketReferenceSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchSuppliersAsync(
            new ManavMalKabulVeEtiketReferenceSearchRequest(request.Query, request.Take),
            cancellationToken));

    [HttpGet("suppliers/by-name")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketSupplierSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManavMalKabulVeEtiketSupplierSuggestionDto>> GetSupplierByName(
        [FromQuery, Required] string name,
        CancellationToken cancellationToken) =>
        Ok(await service.GetSupplierByNameAsync(name, cancellationToken));

    [HttpGet("stocks")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketStockSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketStockSuggestionDto>>> SearchStocks(
        [FromQuery] ManavMalKabulVeEtiketStockSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchStocksAsync(
            new ManavMalKabulVeEtiketStockSearchRequest(request.Query, request.Prefix, request.Take),
            cancellationToken));

    [HttpGet("stocks/by-name")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketStockSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManavMalKabulVeEtiketStockSuggestionDto>> GetStockByName(
        [FromQuery, Required] string name,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockByNameAsync(name, cancellationToken));

    [HttpGet("stocks/{stockCode}")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketStockSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManavMalKabulVeEtiketStockSuggestionDto>> GetStock(
        string stockCode,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockByCodeAsync(stockCode, cancellationToken));

    [HttpGet("acceptance-records")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketAcceptanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketAcceptanceRecordDto>>> ListAcceptanceRecords(
        [FromQuery] ManavMalKabulVeEtiketDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAcceptanceRecordsAsync(request.GetRequiredDate(), cancellationToken));

    [HttpGet("acceptance-records/{id:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketAcceptanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManavMalKabulVeEtiketAcceptanceRecordDto>> GetAcceptanceRecord(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetAcceptanceRecordAsync(id, cancellationToken));

    [HttpPost("acceptance-records/calculate")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketCalculationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<ManavMalKabulVeEtiketCalculationDto> Calculate(
        [FromBody] ManavMalKabulVeEtiketCalculationHttpRequest request) =>
        Ok(service.Calculate(request.ToApplicationRequest()));

    [HttpPost("acceptance-records")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketAcceptanceRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ManavMalKabulVeEtiketAcceptanceRecordDto>> CreateAcceptanceRecord(
        [FromBody] SaveManavMalKabulVeEtiketAcceptanceRecordHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateAcceptanceRecordAsync(request.ToApplicationRequest(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("acceptance-records/{id:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketAcceptanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManavMalKabulVeEtiketAcceptanceRecordDto>> UpdateAcceptanceRecord(
        int id,
        [FromBody] SaveManavMalKabulVeEtiketAcceptanceRecordHttpRequest request,
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
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketLabelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManavMalKabulVeEtiketLabelDto>> GetLabel(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetLabelAsync(id, cancellationToken));

    [HttpPost("labels/preview")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketLabelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<ManavMalKabulVeEtiketLabelDto> PreviewLabel(
        [FromBody] SaveManavMalKabulVeEtiketAcceptanceRecordHttpRequest request) =>
        Ok(service.PreviewLabel(request.ToApplicationRequest()));

    [HttpPost("micro/goods-receipts")]
    [Authorize(Policy = TransferPolicy)]
    [ProducesResponseType(typeof(ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManavMalKabulVeEtiketCreateMicroGoodsReceiptResultDto>> TransferToMicro(
        [FromBody] ManavMalKabulVeEtiketCreateMicroGoodsReceiptHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateMicroGoodsReceiptAsync(request.ToApplicationRequest(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("micro/goods-receipts")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketMicroGoodsReceiptDocumentDto>>> GetMicroGoodsReceipts(
        [FromQuery] ManavMalKabulVeEtiketMicroGoodsReceiptQueryHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetMicroGoodsReceiptsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("micro/goods-receipts/comparison")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketGoodsReceiptComparisonItemDto>>> CompareMicroGoodsReceipts(
        [FromQuery] ManavMalKabulVeEtiketMicroGoodsReceiptQueryHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.CompareGoodsReceiptsAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpGet("reports/received-products")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketReceivedProductReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketReceivedProductReportItemDto>>> GetReceivedProductsReport(
        [FromQuery] ManavMalKabulVeEtiketDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetReceivedProductsReportAsync(request.GetRequiredDate(), cancellationToken));

    [HttpGet("reports/depot-stock")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManavMalKabulVeEtiketDepotStockReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ManavMalKabulVeEtiketDepotStockReportItemDto>>> GetDepotStockReport(
        [FromQuery] ManavMalKabulVeEtiketDepotStockReportHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo ?? 56, ListPolicy);
        return Ok(await service.GetDepotStockReportAsync(
            warehouseNo,
            request.Date?.Date ?? DateTime.Today,
            cancellationToken));
    }
}

public sealed class ManavMalKabulVeEtiketReferenceSearchHttpRequest
{
    [Required]
    [MinLength(2)]
    public string? Query { get; init; }

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class ManavMalKabulVeEtiketStockSearchHttpRequest
{
    public string? Query { get; init; }

    [StringLength(10)]
    public string? Prefix { get; init; } = "MNV";

    [Range(1, 100)]
    public int Take { get; init; } = 20;
}

public sealed class ManavMalKabulVeEtiketDateHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    public DateTime GetRequiredDate() =>
        Date?.Date ?? throw new ArgumentException("Date is required.", nameof(Date));
}

public sealed class ManavMalKabulVeEtiketDepotStockReportHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? Date { get; init; }
}

public sealed class ManavMalKabulVeEtiketCalculationHttpRequest
{
    public decimal GrossWeight { get; init; }

    public decimal CaseTare { get; init; }

    public int? CaseCount { get; init; }

    public decimal? PalletTare { get; init; }

    [StringLength(50)]
    public string? StockBarcode { get; init; }

    public ManavMalKabulVeEtiketCalculationRequest ToApplicationRequest() =>
        new(GrossWeight, CaseTare, CaseCount, PalletTare, StockBarcode);
}

public sealed class SaveManavMalKabulVeEtiketAcceptanceRecordHttpRequest
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

    public SaveManavMalKabulVeEtiketAcceptanceRecordRequest ToApplicationRequest() =>
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

public sealed class ManavMalKabulVeEtiketMicroTransferHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Required]
    [StringLength(25)]
    public string SupplierCode { get; init; } = string.Empty;
}

public sealed class ManavMalKabulVeEtiketCreateMicroGoodsReceiptHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [Required]
    [StringLength(25)]
    public string SupplierCode { get; init; } = string.Empty;

    [StringLength(25)]
    public string? DocumentSeries { get; init; }

    [Range(0, int.MaxValue)]
    public int? DocumentOrderNo { get; init; }

    [StringLength(25)]
    public string? DocumentNo { get; init; }

    [Range(0, short.MaxValue)]
    public int? MikroUserNo { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    public bool MarkAcceptanceRecordsTransferred { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineHttpRequest> Lines { get; init; } =
        Array.Empty<ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineHttpRequest>();

    public ManavMalKabulVeEtiketCreateMicroGoodsReceiptRequest ToApplicationRequest() =>
        new(
            Date?.Date ?? throw new ArgumentException("Date is required.", nameof(Date)),
            SupplierCode,
            DocumentSeries,
            DocumentOrderNo,
            DocumentNo,
            MikroUserNo,
            Description,
            MarkAcceptanceRecordsTransferred,
            Lines.Select(line => line.ToApplicationRequest()).ToArray());
}

public sealed class ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? AcceptanceRecordId { get; init; }

    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    [Range(0, byte.MaxValue)]
    public int? TaxPointer { get; init; }

    [Range(0, 100)]
    public decimal? TaxRatePercent { get; init; }

    public decimal? TaxAmount { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    public ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineRequest ToApplicationRequest() =>
        new(
            AcceptanceRecordId,
            StockCode,
            Quantity,
            UnitPrice,
            UnitPointer,
            TaxPointer,
            TaxRatePercent,
            TaxAmount,
            Description);
}

public sealed class ManavMalKabulVeEtiketMicroGoodsReceiptQueryHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }

    [StringLength(25)]
    public string? SupplierCode { get; init; }

    public ManavMalKabulVeEtiketMicroGoodsReceiptQuery ToApplicationRequest() =>
        new(
            Date?.Date ?? throw new ArgumentException("Date is required.", nameof(Date)),
            SupplierCode);
}
