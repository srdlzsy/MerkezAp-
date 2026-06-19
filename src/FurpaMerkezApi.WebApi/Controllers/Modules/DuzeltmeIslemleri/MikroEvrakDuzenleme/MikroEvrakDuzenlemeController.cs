using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

[ApiController]
[Route("api/duzeltme-islemleri/mikro-evrak-duzenleme")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class MikroEvrakDuzenlemeController(IMikroDocumentEditingService service) : ControllerBase
{
    private const string ListPolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.list";
    private const string DetailPolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.detail";
    private const string UpdatePolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.update";

    [HttpGet("stok-kartlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockCardListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<StockCardListItemDto>>> SearchStockCards(
        [FromQuery] StockCardSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchStockCardsAsync(
            new StockCardSearchRequest(
                request.SearchText,
                request.IncludePassive,
                request.Take),
            cancellationToken));

    [HttpGet("stok-kartlari/{stockCode}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(StockCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockCardDetailDto>> GetStockCard(
        string stockCode,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockCardAsync(stockCode, cancellationToken));

    [HttpPut("stok-kartlari/{stockCode}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(StockCardUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockCardUpdateResponse>> UpdateStockCard(
        string stockCode,
        [FromBody] StockCardPatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateStockCardAsync(
            new UpdateStockCardRequest(
                stockCode,
                request.ToApplicationRequest(),
                User.GetRequiredWarehouseNo()),
            cancellationToken));

    [HttpGet("stok-hareketleri")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(StockMovementDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDocumentDto>> GetStockMovementDocument(
        [FromQuery] StockMovementDocumentLookupHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockMovementDocumentAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPut("stok-hareketleri")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(StockMovementDocumentUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDocumentUpdateResponse>> UpdateStockMovementDocument(
        [FromBody] UpdateStockMovementDocumentHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateStockMovementDocumentAsync(
            request.ToApplicationRequest(User.GetRequiredWarehouseNo()),
            cancellationToken));

    [HttpGet("cari-hareketleri")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CustomerMovementDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerMovementDocumentDto>> GetCustomerMovementDocument(
        [FromQuery] CustomerMovementDocumentLookupHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.GetCustomerMovementDocumentAsync(request.ToApplicationRequest(), cancellationToken));

    [HttpPut("cari-hareketleri")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CustomerMovementDocumentUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerMovementDocumentUpdateResponse>> UpdateCustomerMovementDocument(
        [FromBody] UpdateCustomerMovementDocumentHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateCustomerMovementDocumentAsync(
            request.ToApplicationRequest(User.GetRequiredWarehouseNo()),
            cancellationToken));
}

public sealed class StockCardSearchHttpRequest
{
    [StringLength(100)]
    public string? SearchText { get; init; }

    public bool IncludePassive { get; init; }

    [Range(1, 200)]
    public int Take { get; init; } = 50;
}

public sealed class StockCardPatchHttpRequest
{
    [StringLength(127)]
    public string? Name { get; init; }

    [StringLength(50)]
    public string? ShortName { get; init; }

    [StringLength(127)]
    public string? ForeignName { get; init; }

    [StringLength(25)]
    public string? SupplierCode { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? StockType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? CurrencyType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? TrackingType { get; init; }

    [StringLength(10)]
    public string? Unit1Name { get; init; }

    [StringLength(10)]
    public string? Unit2Name { get; init; }

    [StringLength(10)]
    public string? Unit3Name { get; init; }

    [StringLength(10)]
    public string? Unit4Name { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? RetailTaxPointer { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? WholesaleTaxPointer { get; init; }

    [StringLength(25)]
    public string? CategoryCode { get; init; }

    [StringLength(25)]
    public string? MainGroupCode { get; init; }

    [StringLength(25)]
    public string? SubGroupCode { get; init; }

    [StringLength(25)]
    public string? BrandCode { get; init; }

    [StringLength(25)]
    public string? SectorCode { get; init; }

    [StringLength(25)]
    public string? RayonCode { get; init; }

    [StringLength(25)]
    public string? ManufacturerCode { get; init; }

    [StringLength(25)]
    public string? ResponsibilityCode { get; init; }

    [StringLength(25)]
    public string? ShelfCode { get; init; }

    public bool? SalesStopped { get; init; }

    public bool? OrderStopped { get; init; }

    public bool? ReceivingStopped { get; init; }

    public bool? IsPassive { get; init; }

    public bool? DiscountDisabled { get; init; }

    public StockCardPatchDto ToApplicationRequest() =>
        new(
            Name,
            ShortName,
            ForeignName,
            SupplierCode,
            StockType,
            CurrencyType,
            TrackingType,
            Unit1Name,
            Unit2Name,
            Unit3Name,
            Unit4Name,
            RetailTaxPointer,
            WholesaleTaxPointer,
            CategoryCode,
            MainGroupCode,
            SubGroupCode,
            BrandCode,
            SectorCode,
            RayonCode,
            ManufacturerCode,
            ResponsibilityCode,
            ShelfCode,
            SalesStopped,
            OrderStopped,
            ReceivingStopped,
            IsPassive,
            DiscountDisabled);
}

public sealed class StockMovementDocumentLookupHttpRequest
{
    [Required]
    [StringLength(20)]
    public string DocumentSerie { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DocumentOrderNo { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? DocumentType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? MovementType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? MovementKind { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? NormalReturn { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public StockMovementDocumentLookupRequest ToApplicationRequest() =>
        new(
            DocumentSerie,
            DocumentOrderNo,
            DocumentType,
            MovementType,
            MovementKind,
            NormalReturn,
            WarehouseNo);
}

public sealed class UpdateStockMovementDocumentHttpRequest
{
    [Required]
    public StockMovementDocumentLookupHttpRequest Lookup { get; init; } = new();

    public StockMovementHeaderPatchHttpRequest? Header { get; init; }

    public IReadOnlyCollection<StockMovementLinePatchHttpRequest> Lines { get; init; } =
        Array.Empty<StockMovementLinePatchHttpRequest>();

    public UpdateStockMovementDocumentRequest ToApplicationRequest(int currentUserWarehouseNo) =>
        new(
            Lookup.ToApplicationRequest(),
            Header?.ToApplicationRequest(),
            Lines.Select(line => line.ToApplicationRequest()).ToArray(),
            currentUserWarehouseNo);
}

public sealed class StockMovementHeaderPatchHttpRequest
{
    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(25)]
    public string? CustomerCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? InputWarehouseNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? OutputWarehouseNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? ShippingWarehouseNo { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? MovementGroupCode1 { get; init; }

    [StringLength(25)]
    public string? MovementGroupCode2 { get; init; }

    [StringLength(25)]
    public string? MovementGroupCode3 { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? StockResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    public StockMovementHeaderPatchDto ToApplicationRequest() =>
        new(
            MovementDate,
            DocumentDate,
            DocumentNo,
            CustomerCode,
            InputWarehouseNo,
            OutputWarehouseNo,
            ShippingWarehouseNo,
            Description,
            MovementGroupCode1,
            MovementGroupCode2,
            MovementGroupCode3,
            CustomerResponsibilityCenter,
            StockResponsibilityCenter,
            ProjectCode);
}

public sealed class StockMovementLinePatchHttpRequest
{
    public Guid MovementGuid { get; init; }

    [Range(0, int.MaxValue)]
    public int? RowNo { get; init; }

    [StringLength(25)]
    public string? StockCode { get; init; }

    [Range(1, 4)]
    public byte? UnitPointer { get; init; }

    [Range(0, double.MaxValue)]
    public double? Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? SecondaryQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? Amount { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount1 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount2 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount3 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount4 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount5 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount6 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense1 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense2 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense3 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense4 { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? TaxPointer { get; init; }

    [Range(0, double.MaxValue)]
    public double? TaxAmount { get; init; }

    [Range(0, double.MaxValue)]
    public double? NetWeight { get; init; }

    [Range(0, double.MaxValue)]
    public double? GrossWeight { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PartyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? LotNo { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? StockResponsibilityCenter { get; init; }

    [Range(0, int.MaxValue)]
    public int? InputWarehouseNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? OutputWarehouseNo { get; init; }

    public StockMovementLinePatchDto ToApplicationRequest() =>
        new(
            MovementGuid,
            RowNo,
            StockCode,
            UnitPointer,
            Quantity,
            SecondaryQuantity,
            Amount,
            Discount1,
            Discount2,
            Discount3,
            Discount4,
            Discount5,
            Discount6,
            Expense1,
            Expense2,
            Expense3,
            Expense4,
            TaxPointer,
            TaxAmount,
            NetWeight,
            GrossWeight,
            Description,
            PartyCode,
            LotNo,
            ProjectCode,
            CustomerResponsibilityCenter,
            StockResponsibilityCenter,
            InputWarehouseNo,
            OutputWarehouseNo);
}

public sealed class CustomerMovementDocumentLookupHttpRequest
{
    [Required]
    [StringLength(20)]
    public string DocumentSerie { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DocumentOrderNo { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? DocumentType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? MovementType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? MovementKind { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? NormalReturn { get; init; }

    [StringLength(25)]
    public string? CustomerCode { get; init; }

    public CustomerMovementDocumentLookupRequest ToApplicationRequest() =>
        new(
            DocumentSerie,
            DocumentOrderNo,
            DocumentType,
            MovementType,
            MovementKind,
            NormalReturn,
            CustomerCode);
}

public sealed class UpdateCustomerMovementDocumentHttpRequest
{
    [Required]
    public CustomerMovementDocumentLookupHttpRequest Lookup { get; init; } = new();

    public CustomerMovementHeaderPatchHttpRequest? Header { get; init; }

    public IReadOnlyCollection<CustomerMovementLinePatchHttpRequest> Lines { get; init; } =
        Array.Empty<CustomerMovementLinePatchHttpRequest>();

    public UpdateCustomerMovementDocumentRequest ToApplicationRequest(int currentUserWarehouseNo) =>
        new(
            Lookup.ToApplicationRequest(),
            Header?.ToApplicationRequest(),
            Lines.Select(line => line.ToApplicationRequest()).ToArray(),
            currentUserWarehouseNo);
}

public sealed class CustomerMovementHeaderPatchHttpRequest
{
    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(25)]
    public string? CustomerCode { get; init; }

    [StringLength(25)]
    public string? TurnoverCustomerCode { get; init; }

    [StringLength(40)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? SellerCode { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? ResponsibilityCenter { get; init; }

    public CustomerMovementHeaderPatchDto ToApplicationRequest() =>
        new(
            MovementDate,
            DocumentDate,
            DocumentNo,
            CustomerCode,
            TurnoverCustomerCode,
            Description,
            SellerCode,
            ProjectCode,
            ResponsibilityCenter);
}

public sealed class CustomerMovementLinePatchHttpRequest
{
    public Guid MovementGuid { get; init; }

    [Range(0, int.MaxValue)]
    public int? RowNo { get; init; }

    [StringLength(25)]
    public string? CustomerCode { get; init; }

    [StringLength(25)]
    public string? TurnoverCustomerCode { get; init; }

    [Range(0, double.MaxValue)]
    public double? Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public double? Amount { get; init; }

    [Range(0, double.MaxValue)]
    public double? SubAmount { get; init; }

    [Range(0, int.MaxValue)]
    public int? DueDay { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount1 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount2 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount3 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount4 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount5 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Discount6 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense1 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense2 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense3 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Expense4 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Tax1 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Tax2 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Tax3 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Tax4 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Tax5 { get; init; }

    [StringLength(40)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? SellerCode { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? ResponsibilityCenter { get; init; }

    public CustomerMovementLinePatchDto ToApplicationRequest() =>
        new(
            MovementGuid,
            RowNo,
            CustomerCode,
            TurnoverCustomerCode,
            Quantity,
            Amount,
            SubAmount,
            DueDay,
            Discount1,
            Discount2,
            Discount3,
            Discount4,
            Discount5,
            Discount6,
            Expense1,
            Expense2,
            Expense3,
            Expense4,
            Tax1,
            Tax2,
            Tax3,
            Tax4,
            Tax5,
            Description,
            SellerCode,
            ProjectCode,
            ResponsibilityCenter);
}
