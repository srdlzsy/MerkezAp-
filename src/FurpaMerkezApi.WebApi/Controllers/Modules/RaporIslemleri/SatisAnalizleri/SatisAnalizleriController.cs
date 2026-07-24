using FurpaMerkezApi.Application.Modules.RaporIslemleri.SatisAnalizleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.SatisAnalizleri;

[ApiController]
[Route("api/rapor-islemleri/satis-analizleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class SatisAnalizleriController(
    ISalesAnalysisReportsUseCase salesAnalysisReportsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "rapor-islemleri";
    private const string ModuleName = "RaporIslemleri";
    private const string MenuCode = "satis-analizleri";
    private const string MenuName = "SatisAnalizleri";
    private const string ListPolicy = "rapor-islemleri.satis-analizleri.list";

    [HttpGet("banka-hareketleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BankMovementAnalysisItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BankMovementAnalysisItemDto>>> BankMovements(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetBankMovementsAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("banka-hareketleri/sube")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BranchBankMovementSummaryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BranchBankMovementSummaryItemDto>>> BankMovementsByBranch(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetBankMovementsByBranchAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("banka-odeme-ozetleri/banka")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(BankPaymentSummaryReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BankPaymentSummaryReportDto>> BankPaymentSummary(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetBankPaymentSummaryAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("banka-odeme-ozetleri/merchant")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(MerchantPaymentSummaryReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MerchantPaymentSummaryReportDto>> MerchantPaymentSummary(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetMerchantPaymentSummaryAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("banka-odeme-ozetleri/valor")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(ValorPaymentSummaryReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ValorPaymentSummaryReportDto>> ValorPaymentSummary(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetValorPaymentSummaryAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("yemek-cekleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(FoodCheckReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FoodCheckReportDto>> FoodChecks(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetFoodCheckReportAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("yemek-cekleri/toplamlar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(FoodCheckTotalsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FoodCheckTotalsDto>> FoodCheckTotals(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var report = await salesAnalysisReportsUseCase.GetFoodCheckReportAsync(ToDateRangeRequest(request), cancellationToken);
        return Ok(report.Totals);
    }

    [HttpGet("yemek-cekleri/metropol-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> MetropolTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.Metropol, cancellationToken);

    [HttpGet("yemek-cekleri/multinet-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> MultinetTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.Multinet, cancellationToken);

    [HttpGet("yemek-cekleri/setcard-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> SetcardTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.Setcard, cancellationToken);

    [HttpGet("yemek-cekleri/sodexo-kupon-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> SodexoKuponTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.SodexoKupon, cancellationToken);

    [HttpGet("yemek-cekleri/sodexo-pos-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> SodexoPosTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.SodexoPos, cancellationToken);

    [HttpGet("yemek-cekleri/ticket-kupon-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> TicketKuponTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.TicketKupon, cancellationToken);

    [HttpGet("yemek-cekleri/ticket-pos-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> TicketPosTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.TicketPos, cancellationToken);

    [HttpGet("yemek-cekleri/genel-toplam")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(SalesAnalysisAmountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<SalesAnalysisAmountDto>> FoodCheckGeneralTotal(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        FoodCheckTotal(request, FoodCheckTotalKind.Total, cancellationToken);

    [HttpGet("marketyo-satislari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(MyoSalesReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MyoSalesReportDto>> MyoSales(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetMyoSalesReportAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("marketyo-satislari/sube")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<MyoSalesByBranchItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<MyoSalesByBranchItemDto>>> MyoSalesByBranch(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetMyoSalesByBranchAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("z-rapor-banka-analizi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ZReportBankAnalysisItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<ZReportBankAnalysisItemDto>>> ZReportBankAnalysis(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetZReportBankAnalysisAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("indirim-kartlari")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<DiscountCardDetailItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<DiscountCardDetailItemDto>>> DiscountCards(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetDiscountCardDetailsAsync(ToDateRangeRequest(request), cancellationToken));

    [HttpGet("eksik-cirolar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<MissingTurnoverBranchItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<MissingTurnoverBranchItemDto>>> MissingTurnovers(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetMissingTurnoverBranchesAsync(ToDateRangeRequest(request), cancellationToken));

    private async Task<ActionResult<SalesAnalysisAmountDto>> FoodCheckTotal(
        WarehouseOrderDateRangeHttpRequest request,
        FoodCheckTotalKind totalKind,
        CancellationToken cancellationToken) =>
        Ok(await salesAnalysisReportsUseCase.GetFoodCheckTotalAsync(
            ToDateRangeRequest(request),
            totalKind,
            cancellationToken));

    private SalesAnalysisDateRangeRequest ToDateRangeRequest(WarehouseOrderDateRangeHttpRequest request) =>
        new(
            User.ResolveWarehouseScope(request.WarehouseNo),
            request.StartDate!.Value,
            request.EndDate!.Value);
}
