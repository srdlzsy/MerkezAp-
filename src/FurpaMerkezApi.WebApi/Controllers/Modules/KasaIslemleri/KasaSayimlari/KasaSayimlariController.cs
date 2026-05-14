using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Queries;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaSayimlari;

[ApiController]
[Route("api/kasa-islemleri/kasa-sayimlari")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class KasaSayimlariController(
    ICashSummaryQueriesUseCase cashSummaryQueriesUseCase,
    ICashSummaryLookupsUseCase cashSummaryLookupsUseCase,
    ICashSummaryCommandsUseCase cashSummaryCommandsUseCase,
    IGetCashSummaryZReportTotalUseCase getCashSummaryZReportTotalUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "kasa-sayimlari";
    private const string MenuName = "KasaSayimlari";
    private const string ListPolicy = "kasa-islemleri.kasa-sayimlari.list";
    private const string DetailPolicy = "kasa-islemleri.kasa-sayimlari.detail";
    private const string CreatePolicy = "kasa-islemleri.kasa-sayimlari.create";
    private const string UpdatePolicy = "kasa-islemleri.kasa-sayimlari.update";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashSummaryListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashSummaryListItemDto>>> List(
        [FromQuery] CashSummaryDateHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.ListAsync(
            new CashSummaryDateRequest(
                request.DateToGet!.Value,
                warehouseNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("rapor")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashSummaryReportItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashSummaryReportItemDto>>> Report(
        [FromQuery] CashSummaryDateHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.GetReportAsync(
            new CashSummaryDateRequest(
                request.DateToGet!.Value,
                warehouseNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("banknot-takipleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BanknoteTrackItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BanknoteTrackItemDto>>> ListBanknoteTracks(
        [FromQuery] CashSummaryDateHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.ListBanknoteTracksAsync(
            new CashSummaryDateRequest(
                request.DateToGet!.Value,
                warehouseNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("banknot-takipleri/toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<double>> GetBanknoteTrackTotalAmount(
        [FromQuery] CashSummaryDateHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.GetBanknoteTrackTotalAmountAsync(
            new CashSummaryDateRequest(
                request.DateToGet!.Value,
                warehouseNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashSummaryDetailItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<CashSummaryDetailItemDto>>> Detail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailInternal(documentSerie, documentOrderNo, warehouseNo, cancellationToken);

    [HttpGet("{documentSerie}/{documentOrderNo:int}/detaylar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashSummaryDetailItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<CashSummaryDetailItemDto>>> DetailLines(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken) =>
        await DetailInternal(documentSerie, documentOrderNo, warehouseNo, cancellationToken);

    [HttpGet("{documentSerie}/{documentOrderNo:int}/banknot-hareketleri")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BanknoteMovementItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BanknoteMovementItemDto>>> BanknoteMovements(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.GetBanknoteMovementsAsync(
            new CashSummaryDocumentRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{documentSerie}/{documentOrderNo:int}/hediye-ceki-hareketleri")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GiftCheckMovementItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<GiftCheckMovementItemDto>>> GiftCheckMovements(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.GetGiftCheckMovementsAsync(
            new CashSummaryDocumentRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("kasiyerler/ikili")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashierItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashierItemDto>>> GetCashierAndManager(
        [FromQuery] CashierPairHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await cashSummaryLookupsUseCase.GetCashierAndManagerAsync(
            new CashierPairRequest(
                request.CashierCode!.Value,
                request.ManagerCode!.Value),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("kasalar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegistryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegistryItemDto>>> GetCashRegistries(
        [FromQuery] CashRegistryHttpRequest request,
        CancellationToken cancellationToken)
    {
        var branchNo = request.BranchNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryLookupsUseCase.GetCashRegistriesAsync(
            new CashRegistryRequest(branchNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("kasa-detayi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(CashRegisterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashRegisterDetailDto>> GetCashRegisterDetail(
        [FromQuery] CashRegisterLookupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await cashSummaryLookupsUseCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(
                request.CashNo,
                request.CashRegisterNo),
            cancellationToken);

        if (response is null)
        {
            throw new KeyNotFoundException("Cash register detail was not found.");
        }

        return Ok(response);
    }

    [HttpGet("kasiyerler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashierSearchItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CashierSearchItemDto>>> SearchCashiers(
        [FromQuery] CashierSearchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await cashSummaryLookupsUseCase.SearchCashiersAsync(
            new CashierSearchRequest(request.FilterString!),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("banknot-tipleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BanknoteTypeItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<BanknoteTypeItemDto>>> BanknoteTypes(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListBanknoteTypesAsync(cancellationToken));

    [HttpGet("hediye-ceki-tipleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<GiftCheckTypeItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<GiftCheckTypeItemDto>>> GiftCheckTypes(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListGiftCheckTypesAsync(cancellationToken));

    [HttpGet("odeme-tipleri/banka")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaymentTypeItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentTypeItemDto>>> BankPaymentTypes(
        [FromQuery] BankPaymentTypeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await cashSummaryLookupsUseCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest(request.CashRegisterNo!),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("odeme-tipleri/yemek-ceki")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaymentTypeItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentTypeItemDto>>> FoodCheckPaymentTypes(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListFoodCheckPaymentTypesAsync(cancellationToken));

    [HttpGet("odeme-tipleri/online")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaymentTypeItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentTypeItemDto>>> OnlinePaymentTypes(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListOnlineSalesPaymentTypesAsync(cancellationToken));

    [HttpGet("odeme-tipleri/masraf-pusulasi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaymentTypeItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentTypeItemDto>>> ExpenseCompassPaymentTypes(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListExpenseCompassPaymentTypesAsync(cancellationToken));

    [HttpGet("odeme-tipleri/magaza-masrafi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PaymentTypeItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentTypeItemDto>>> StoreExpensePaymentTypes(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListStoreExpensePaymentTypesAsync(cancellationToken));

    [HttpGet("online-kasa-detaylari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CashRegisterDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterDetailDto>>> OnlineCashRegisters(
        CancellationToken cancellationToken) =>
        Ok(await cashSummaryLookupsUseCase.ListOnlineCashRegistersAsync(cancellationToken));

    [HttpGet("z-rapor-toplam")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<double>> GetZReportTotal(
        [FromQuery] ZReportValueHttpRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await getCashSummaryZReportTotalUseCase.ExecuteAsync(
            new ZReportValueRequest(
                resolvedWarehouseNo,
                request.DocumentSerie!,
                request.ZReportNo!.Value,
                request.CashNo!.Value),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateCashSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateCashSummaryResponse>> Create(
        [FromBody] CreateCashSummaryHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = ResolveWriteWarehouseNo(request.WarehouseNo);
        var response = await cashSummaryCommandsUseCase.CreateAsync(
            new CreateCashSummaryRequest(
                warehouseNo,
                request.CashNo!.Value,
                request.ZReportNo!.Value,
                request.CashierNo!.Value,
                request.ManagerNo!.Value,
                request.ZTotalValue,
                request.Total,
                request.SummaryDate!.Value,
                request.GiftCheckMovements
                    .Select(line => new CreateCashSummaryGiftCheckLineRequest(
                        line.GiftCheckType!.Value,
                        line.Quantity!.Value,
                        line.Total,
                        line.Value))
                    .ToArray(),
                request.BanknoteMovements
                    .Select(line => new CreateCashSummaryBanknoteLineRequest(
                        line.BanknoteType!.Value,
                        line.Quantity!.Value,
                        line.Total,
                        line.Value))
                    .ToArray(),
                request.PaymentTypes
                    .Select(line => new CreateCashSummaryPaymentLineRequest(
                        line.PaymentName!,
                        line.PaymentTypeNo!.Value,
                        line.AccountCode ?? string.Empty,
                        line.TerminalId ?? string.Empty,
                        line.SlipNumber ?? 0,
                        line.AmountValue))
                    .ToArray(),
                request.StoreExpenses
                    .Select(line => new CreateCashSummaryStoreExpenseLineRequest(
                        line.StoreExpensesType!.Value,
                        line.Description ?? string.Empty,
                        line.AmountValue))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("banknot-takipleri")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateBanknoteTrackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CreateBanknoteTrackResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateBanknoteTrackResponse>> CreateBanknoteTrack(
        [FromBody] CreateBanknoteTrackHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = ResolveWriteWarehouseNo(request.WarehouseNo);
        var response = await cashSummaryCommandsUseCase.CreateBanknoteTrackAsync(
            new CreateBanknoteTrackRequest(
                warehouseNo,
                request.BanknoteTrackDate!.Value,
                request.TotalAmount,
                request.DeliveryTotalAmount,
                request.Deliverer ?? string.Empty,
                request.Receiver ?? string.Empty),
            cancellationToken);

        return response.Created
            ? StatusCode(StatusCodes.Status201Created, response)
            : Ok(response);
    }

    [HttpPut("{documentSerie}/{documentOrderNo:int}/detaylar")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(UpdateCashSummaryDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateCashSummaryDetailsResponse>> UpdateDetails(
        string documentSerie,
        int documentOrderNo,
        [FromBody] UpdateCashSummaryDetailsHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = ResolveWriteWarehouseNo(request.WarehouseNo);
        var response = await cashSummaryCommandsUseCase.UpdateDetailsAsync(
            new UpdateCashSummaryDetailsRequest(
                warehouseNo,
                documentSerie,
                documentOrderNo,
                request.Details
                    .Select(line => new UpdateCashSummaryDetailLineRequest(
                        line.TypeName ?? string.Empty,
                        line.PaymentTypeId!.Value,
                        line.AccountCode ?? string.Empty,
                        line.SlipNumber ?? 0,
                        line.Amount,
                        line.TerminalId ?? string.Empty,
                        line.Description ?? string.Empty))
                    .ToArray()),
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{documentSerie}/{documentOrderNo:int}/banknot-hareketleri")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(UpdateCashSummaryBanknotesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateCashSummaryBanknotesResponse>> UpdateBanknotes(
        string documentSerie,
        int documentOrderNo,
        [FromBody] UpdateCashSummaryBanknotesHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = ResolveWriteWarehouseNo(request.WarehouseNo);
        var response = await cashSummaryCommandsUseCase.UpdateBanknotesAsync(
            new UpdateCashSummaryBanknotesRequest(
                warehouseNo,
                documentSerie,
                documentOrderNo,
                request.BanknoteMovements
                    .Select(line => new UpdateCashSummaryBanknoteLineRequest(
                        line.Value,
                        line.BanknoteType!.Value,
                        line.Quantity!.Value,
                        line.Total))
                    .ToArray()),
            cancellationToken);

        return Ok(response);
    }

    [HttpDelete("{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(DeleteCashSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeleteCashSummaryResponse>> Delete(
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await cashSummaryCommandsUseCase.DeleteAsync(
            new DeleteCashSummaryRequest(
                warehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        return Ok(response);
    }

    private async Task<ActionResult<IReadOnlyCollection<CashSummaryDetailItemDto>>> DetailInternal(
        string documentSerie,
        int documentOrderNo,
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await cashSummaryQueriesUseCase.GetDetailsAsync(
            new CashSummaryDocumentRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken);

        if (response.Count == 0)
        {
            throw new KeyNotFoundException("Cash summary detail was not found.");
        }

        return Ok(response);
    }

    private int ResolveWriteWarehouseNo(int? warehouseNo)
    {
        var currentWarehouseNo = User.GetRequiredWarehouseNo();

        if (warehouseNo.HasValue && warehouseNo.Value != currentWarehouseNo)
        {
            throw new ArgumentException("Warehouse no must match the current user warehouse.", nameof(warehouseNo));
        }

        return currentWarehouseNo;
    }
}

public sealed class CashSummaryDateHttpRequest
{
    [Required]
    public DateTime? DateToGet { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class CashierPairHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? CashierCode { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? ManagerCode { get; init; }
}

public sealed class CashRegistryHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? BranchNo { get; init; }
}

public sealed class CashRegisterLookupHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? CashNo { get; init; }

    [StringLength(40)]
    public string? CashRegisterNo { get; init; }
}

public sealed class CashierSearchHttpRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? FilterString { get; init; }
}

public sealed class BankPaymentTypeHttpRequest
{
    [Required]
    [StringLength(40)]
    public string? CashRegisterNo { get; init; }
}

public sealed class ZReportValueHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [StringLength(20)]
    public string? DocumentSerie { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? ZReportNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CashNo { get; init; }
}

public sealed class CreateBanknoteTrackHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? BanknoteTrackDate { get; init; }

    public double TotalAmount { get; init; }

    public double DeliveryTotalAmount { get; init; }

    [StringLength(100)]
    public string? Deliverer { get; init; }

    [StringLength(100)]
    public string? Receiver { get; init; }
}

public sealed class CreateCashSummaryHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CashNo { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? ZReportNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CashierNo { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? ManagerNo { get; init; }

    public double ZTotalValue { get; init; }

    public double Total { get; init; }

    [Required]
    public DateTime? SummaryDate { get; init; }

    public IReadOnlyCollection<CreateGiftCheckMovementHttpRequest> GiftCheckMovements { get; init; } =
        Array.Empty<CreateGiftCheckMovementHttpRequest>();

    public IReadOnlyCollection<CreateBanknoteMovementHttpRequest> BanknoteMovements { get; init; } =
        Array.Empty<CreateBanknoteMovementHttpRequest>();

    public IReadOnlyCollection<CreatePaymentTypeHttpRequest> PaymentTypes { get; init; } =
        Array.Empty<CreatePaymentTypeHttpRequest>();

    public IReadOnlyCollection<CreateStoreExpenseHttpRequest> StoreExpenses { get; init; } =
        Array.Empty<CreateStoreExpenseHttpRequest>();
}

public sealed class CreateGiftCheckMovementHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? GiftCheckType { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? Quantity { get; init; }

    public double Total { get; init; }

    public double Value { get; init; }
}

public sealed class CreateBanknoteMovementHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? BanknoteType { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? Quantity { get; init; }

    public double Total { get; init; }

    public double Value { get; init; }
}

public sealed class CreatePaymentTypeHttpRequest
{
    [Required]
    [StringLength(100)]
    public string? PaymentName { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? PaymentTypeNo { get; init; }

    [StringLength(40)]
    public string? AccountCode { get; init; }

    [StringLength(40)]
    public string? TerminalId { get; init; }

    [Range(0, int.MaxValue)]
    public int? SlipNumber { get; init; }

    public double AmountValue { get; init; }
}

public sealed class CreateStoreExpenseHttpRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? StoreExpensesType { get; init; }

    [StringLength(250)]
    public string? Description { get; init; }

    public double AmountValue { get; init; }
}

public sealed class UpdateCashSummaryDetailsHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<UpdateCashSummaryDetailLineHttpRequest> Details { get; init; } =
        Array.Empty<UpdateCashSummaryDetailLineHttpRequest>();
}

public sealed class UpdateCashSummaryDetailLineHttpRequest
{
    [StringLength(50)]
    public string? TypeName { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? PaymentTypeId { get; init; }

    [StringLength(40)]
    public string? AccountCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? SlipNumber { get; init; }

    public double Amount { get; init; }

    [StringLength(40)]
    public string? TerminalId { get; init; }

    [StringLength(250)]
    public string? Description { get; init; }
}

public sealed class UpdateCashSummaryBanknotesHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public IReadOnlyCollection<UpdateCashSummaryBanknoteLineHttpRequest> BanknoteMovements { get; init; } =
        Array.Empty<UpdateCashSummaryBanknoteLineHttpRequest>();
}

public sealed class UpdateCashSummaryBanknoteLineHttpRequest
{
    public double Value { get; init; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? BanknoteType { get; init; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? Quantity { get; init; }

    public double Total { get; init; }
}
