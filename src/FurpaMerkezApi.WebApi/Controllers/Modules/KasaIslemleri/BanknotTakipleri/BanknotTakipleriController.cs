using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Create;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Detail;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.List;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.BanknotTakipleri;

[ApiController]
[Route("api/kasa-islemleri/banknot-takipleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class BanknotTakipleriController(
    IListBanknoteTracksUseCase listBanknoteTracksUseCase,
    IGetBanknoteTrackDetailUseCase getBanknoteTrackDetailUseCase,
    ICreateBanknoteTrackUseCase createBanknoteTrackUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "kasa-islemleri";
    private const string ModuleName = "KasaIslemleri";
    private const string MenuCode = "banknot-takipleri";
    private const string MenuName = "BanknotTakipleri";
    private const string ListPolicy = "kasa-islemleri.banknot-takipleri.list";
    private const string DetailPolicy = "kasa-islemleri.banknot-takipleri.detail";
    private const string CreatePolicy = "kasa-islemleri.banknot-takipleri.create";

    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<BanknoteTrackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<BanknoteTrackDto>>> List(
        [FromQuery] BanknoteTrackDateHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScope(request.WarehouseNo);
        var response = await listBanknoteTracksUseCase.ExecuteAsync(
            new BanknoteTrackListRequest(
                request.DateToGet!.Value,
                warehouseNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{banknoteTrackId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(BanknoteTrackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BanknoteTrackDto>> Detail(
        Guid banknoteTrackId,
        CancellationToken cancellationToken)
    {
        var response = await getBanknoteTrackDetailUseCase.ExecuteAsync(
            new BanknoteTrackDetailRequest(
                banknoteTrackId,
                User.ResolveWarehouseNo()),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateBanknoteTrackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CreateBanknoteTrackResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateBanknoteTrackResponse>> Create(
        [FromBody] CreateBanknoteTrackHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = ResolveWriteWarehouseNo(request.WarehouseNo);
        var response = await createBanknoteTrackUseCase.ExecuteAsync(
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

    private int ResolveWriteWarehouseNo(int? warehouseNo)
        => User.ResolveWarehouseNo(warehouseNo);
}

public sealed class BanknoteTrackDateHttpRequest
{
    [Required]
    public DateTime? DateToGet { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class CreateBanknoteTrackHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Required]
    public DateTime? BanknoteTrackDate { get; init; }

    [Range(0d, double.MaxValue)]
    public double TotalAmount { get; init; }

    [Range(0d, double.MaxValue)]
    public double DeliveryTotalAmount { get; init; }

    [StringLength(100)]
    public string? Deliverer { get; init; }

    [StringLength(100)]
    public string? Receiver { get; init; }
}
