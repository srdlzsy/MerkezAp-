using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.YeniKasaAnalizleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.YeniKasaAnalizleri;

[ApiController]
[Route("api/kasa-islemleri/yeni-kasa-analizleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class YeniKasaAnalizleriController(
    IYeniKasaAnalizleriService yeniKasaAnalizleriService)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "yeni-kasa-analizleri";
    private const string MenuName = "YeniKasaAnalizleri";
    private const string ListPolicy = "kasa-islemleri.yeni-kasa-analizleri.list";

    [HttpGet("ciro-ozeti")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YeniKasaCiroOzetItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YeniKasaCiroOzetItemDto>>> CiroOzeti(
        [FromQuery] YeniKasaAnalizHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await yeniKasaAnalizleriService.GetCiroOzetiAsync(ToRequest(request), cancellationToken));

    [HttpGet("kasa-ozeti")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YeniKasaKasaOzetItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YeniKasaKasaOzetItemDto>>> KasaOzeti(
        [FromQuery] YeniKasaAnalizHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await yeniKasaAnalizleriService.GetKasaOzetiAsync(ToRequest(request), cancellationToken));

    [HttpGet("fis-mutabakat")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YeniKasaFisMutabakatItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YeniKasaFisMutabakatItemDto>>> FisMutabakat(
        [FromQuery] YeniKasaAnalizHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await yeniKasaAnalizleriService.GetFisMutabakatiAsync(ToRequest(request), cancellationToken));

    [HttpGet("anomaliler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YeniKasaAnomalyItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YeniKasaAnomalyItemDto>>> Anomaliler(
        [FromQuery] YeniKasaAnalizHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await yeniKasaAnalizleriService.GetAnomalilerAsync(ToRequest(request), cancellationToken));

    [HttpGet("odeme-tipleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YeniKasaPaymentMethodItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YeniKasaPaymentMethodItemDto>>> OdemeTipleri(
        [FromQuery] YeniKasaAnalizHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await yeniKasaAnalizleriService.GetOdemeTipleriAsync(ToRequest(request), cancellationToken));

    [HttpGet("saglik-ozeti")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<YeniKasaSaglikOzetItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<YeniKasaSaglikOzetItemDto>>> SaglikOzeti(
        [FromQuery] YeniKasaAnalizHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await yeniKasaAnalizleriService.GetSaglikOzetiAsync(ToRequest(request), cancellationToken));

    [HttpGet("fis-detay")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(YeniKasaFisDetayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<YeniKasaFisDetayDto>> FisDetay(
        [FromQuery] YeniKasaFisDetayHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasValidFisDetayLookup(request))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Gecersiz fis detay istegi.",
                Detail = "Uuid verilmeli veya businessDate, warehouseNo, cashRegisterNo ve receiptNumber birlikte gonderilmelidir."
            });
        }

        var result = await yeniKasaAnalizleriService.GetFisDetayiAsync(ToRequest(request), cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    private static YeniKasaAnalizRequest ToRequest(YeniKasaAnalizHttpRequest request) =>
        new(
            request.WarehouseNo,
            request.StartDate!.Value,
            request.EndDate!.Value,
            request.CashRegisterNo,
            request.CashierCode,
            request.Take ?? 500,
            request.OnlyProblematic ?? false);

    private static YeniKasaFisDetayRequest ToRequest(YeniKasaFisDetayHttpRequest request) =>
        new(
            request.Uuid,
            request.BusinessDate,
            request.WarehouseNo,
            request.CashRegisterNo,
            request.ReceiptNumber);

    private static bool HasValidFisDetayLookup(YeniKasaFisDetayHttpRequest request) =>
        !string.IsNullOrWhiteSpace(request.Uuid) ||
        (request.BusinessDate.HasValue &&
            request.WarehouseNo is > 0 &&
            !string.IsNullOrWhiteSpace(request.CashRegisterNo) &&
            !string.IsNullOrWhiteSpace(request.ReceiptNumber));
}

public sealed class YeniKasaAnalizHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? StartDate { get; init; }

    [Required]
    public DateTime? EndDate { get; init; }

    [StringLength(40)]
    public string? CashRegisterNo { get; init; }

    [StringLength(25)]
    public string? CashierCode { get; init; }

    [Range(1, 2000)]
    public int? Take { get; init; }

    public bool? OnlyProblematic { get; init; }
}

public sealed class YeniKasaFisDetayHttpRequest
{
    [StringLength(80)]
    public string? Uuid { get; init; }

    public DateTime? BusinessDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [StringLength(40)]
    public string? CashRegisterNo { get; init; }

    [StringLength(80)]
    public string? ReceiptNumber { get; init; }
}
