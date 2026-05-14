using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.List;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Products;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Tags;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.EtiketBelgeleri;

[ApiController]
[Route("api/stok-islemleri/etiket-belgeleri")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class EtiketBelgeleriController(
    IListLabelDocumentsUseCase listLabelDocumentsUseCase,
    IGetLabelDocumentProductsUseCase getLabelDocumentProductsUseCase,
    ICreateLabelDocumentUseCase createLabelDocumentUseCase,
    IListLabelPriceChangedProductsUseCase listLabelPriceChangedProductsUseCase,
    IListLabelTagsUseCase listLabelTagsUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "stok-islemleri";
    private const string ModuleName = "StokIslemleri";
    private const string MenuCode = "etiket-belgeleri";
    private const string MenuName = "EtiketBelgeleri";
    private const string ListPolicy = "stok-islemleri.etiket-belgeleri.list";
    private const string DetailPolicy = "stok-islemleri.etiket-belgeleri.detail";
    private const string CreatePolicy = "stok-islemleri.etiket-belgeleri.create";
    private const string UpdatePolicy = "stok-islemleri.etiket-belgeleri.update";

    private const string KunyeListPolicy = "stok-islemleri.kunye-etiket-yazdirma.list";




    [HttpGet]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LabelDocumentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LabelDocumentListItemDto>>> ListRecent(
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        [FromQuery, Range(1, 100)] int? take,
        CancellationToken cancellationToken) =>
        await ListRecentInternal(warehouseNo, take, cancellationToken);

    [HttpGet("son")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LabelDocumentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LabelDocumentListItemDto>>> ListLastDocuments(
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        [FromQuery, Range(1, 100)] int? take,
        CancellationToken cancellationToken) =>
        await ListRecentInternal(warehouseNo, take, cancellationToken);

    [HttpGet("tumu")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LabelDocumentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LabelDocumentListItemDto>>> ListAll(
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var response = await listLabelDocumentsUseCase.ExecuteAsync(
            new LabelDocumentListRequest(warehouseNo),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{documentId:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LabelDocumentProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<LabelDocumentProductDto>>> Detail(
        int documentId,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await getLabelDocumentProductsUseCase.ExecuteAsync(
            new LabelDocumentDetailRequest(
                resolvedWarehouseNo,
                documentId),
            cancellationToken);

        return Ok(response);
    }


[HttpGet("kunye-etiket-yazdirma")]
[Authorize(Policy = KunyeListPolicy)]
public Task<ActionResult<IReadOnlyCollection<LabelTagDto>>> KunyeTags(
    [FromQuery] LabelTagListHttpRequest request,
    CancellationToken cancellationToken) =>
    TagsCore(request, cancellationToken);

private async Task<ActionResult<IReadOnlyCollection<LabelTagDto>>> TagsCore(
    LabelTagListHttpRequest request,
    CancellationToken cancellationToken)
{
    var warehouseNo = User.GetRequiredWarehouseNo();
    var response = await listLabelTagsUseCase.ExecuteAsync(
        new LabelTagListRequest(warehouseNo, request.DateToGet!.Value),
        cancellationToken);

    return Ok(response);
}
    [HttpGet("fiyati-degisen-urunler")]
    [HttpGet("get-by-date-for-label")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<LabelPriceChangedProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LabelPriceChangedProductDto>>> GetByDateForLabel(
        [FromQuery] LabelPriceChangedProductListHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await listLabelPriceChangedProductsUseCase.ExecuteAsync(
            new LabelPriceChangedProductRequest(
                warehouseNo,
                request.ParseDateTimeFilter()),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateLabelDocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateLabelDocumentResponse>> Create(
        [FromBody] CreateLabelDocumentHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await createLabelDocumentUseCase.ExecuteAsync(
            new CreateLabelDocumentRequest(
                warehouseNo,
                request.Lines
                    .Select(line => new CreateLabelDocumentLineRequest(line.ProductCode))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(ModuleActionScaffoldResponse), StatusCodes.Status501NotImplemented)]
    public ActionResult<ModuleActionScaffoldResponse> Update(string id, [FromBody] ModuleActionRequest request) =>
        UpdateNotImplemented(UpdatePolicy, id);

    private async Task<ActionResult<IReadOnlyCollection<LabelDocumentListItemDto>>> ListRecentInternal(
        int? warehouseNo,
        int? take,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();
        var response = await listLabelDocumentsUseCase.ExecuteAsync(
            new LabelDocumentListRequest(
                resolvedWarehouseNo,
                take ?? 10),
            cancellationToken);

        return Ok(response);
    }
}

public sealed class LabelTagListHttpRequest
{
    [Required]
    public DateTime? DateToGet { get; init; }
}

public sealed class LabelPriceChangedProductListHttpRequest
{
    [Required]
    public string? DateTimeFilter { get; init; }

    public DateTime ParseDateTimeFilter()
    {
        if (string.IsNullOrWhiteSpace(DateTimeFilter))
        {
            throw new ArgumentException("Date time filter is required.", nameof(DateTimeFilter));
        }

        if (DateTime.TryParseExact(
                DateTimeFilter,
                "dd.MM.yyyy HH:mm:ss",
                System.Globalization.CultureInfo.GetCultureInfo("tr-TR"),
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            "Date time filter must match format 'dd.MM.yyyy HH:mm:ss'.",
            nameof(DateTimeFilter));
    }
}

public sealed class CreateLabelDocumentHttpRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateLabelDocumentLineHttpRequest> Lines { get; init; } =
        Array.Empty<CreateLabelDocumentLineHttpRequest>();
}

public sealed class CreateLabelDocumentLineHttpRequest
{
    [Required]
    [StringLength(25)]
    public string ProductCode { get; init; } = string.Empty;
}
