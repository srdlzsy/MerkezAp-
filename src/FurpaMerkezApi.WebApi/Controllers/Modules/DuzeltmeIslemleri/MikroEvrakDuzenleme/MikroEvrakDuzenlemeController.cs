using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

[ApiController]
[Route("api/duzeltme-islemleri/mikro-evrak-duzenleme")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class MikroEvrakDuzenlemeController(
    IMikroDocumentEditingService service,
    IDocumentFlowService documentFlowService) : ControllerBase
{
    private const byte CompanyDispatchDocumentType = 1;
    private const byte ReceivingReceiptDocumentType = 13;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte InterWarehouseMovementType = 2;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const string ListPolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.list";
    private const string DetailPolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.detail";
    private const string UpdatePolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.update";
    private const string DeletePolicy = "duzeltme-islemleri.mikro-evrak-duzenleme.delete";

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
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.UpdateStockCardAsync(
            new UpdateStockCardRequest(
                stockCode,
                request.ToApplicationRequest(),
                warehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.StockCard,
            warehouseNo,
            "STOKKARTI",
            response.StockCard.StockCode,
            DocumentFlowStep.MasterDataUpdated,
            $"Stok karti duzenlendi. Guncellenen satir: {response.Summary.UpdatedRowCount}.",
            response.StockCard.StockCode,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("stok-kartlari/{stockCode}/depolar")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockCardWarehouseSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<StockCardWarehouseSettingsDto>>> GetStockCardWarehouseSettings(
        string stockCode,
        [FromQuery] int? warehouseNo,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockCardWarehouseSettingsAsync(stockCode, warehouseNo, cancellationToken));

    [HttpPut("stok-kartlari/{stockCode}/depolar/{warehouseNo:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(StockCardWarehouseUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockCardWarehouseUpdateResponse>> UpdateStockCardWarehouseSettings(
        string stockCode,
        [Range(1, int.MaxValue)] int warehouseNo,
        [FromBody] StockCardWarehousePatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserWarehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.UpdateStockCardWarehouseSettingsAsync(
            new UpdateStockCardWarehouseSettingsRequest(
                stockCode,
                warehouseNo,
                request.ToApplicationRequest(),
                currentUserWarehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.StockCard,
            currentUserWarehouseNo,
            "STOKDEPO",
            $"{response.WarehouseSettings.StockCode}:{response.WarehouseSettings.WarehouseNo}",
            DocumentFlowStep.MasterDataUpdated,
            $"Stok depo ayari duzenlendi. Depo: {response.WarehouseSettings.WarehouseNo}, guncellenen satir: {response.Summary.UpdatedRowCount}.",
            response.WarehouseSettings.StockCode,
            cancellationToken,
            targetWarehouseNo: response.WarehouseSettings.WarehouseNo);

        return Ok(response);
    }

    [HttpDelete("stok-kartlari/{stockCode}/depolar/{warehouseNo:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(typeof(MikroDocumentDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MikroDocumentDeleteResponse>> DeleteStockCardWarehouseSettings(
        string stockCode,
        [Range(1, int.MaxValue)] int warehouseNo,
        CancellationToken cancellationToken)
    {
        var currentUserWarehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.DeleteStockCardWarehouseSettingsAsync(
            new DeleteStockCardWarehouseSettingsRequest(
                stockCode,
                warehouseNo,
                currentUserWarehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.StockCard,
            currentUserWarehouseNo,
            "STOKDEPO",
            $"{stockCode}:{warehouseNo}",
            DocumentFlowStep.DocumentDeleted,
            $"Stok depo ayari silindi. Depo: {warehouseNo}, silinen satir: {response.DeletedRowCount}.",
            stockCode,
            cancellationToken,
            targetWarehouseNo: warehouseNo);

        return Ok(response);
    }

    [HttpGet("depolar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseCardListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseCardListItemDto>>> SearchWarehouseCards(
        [FromQuery] WarehouseCardSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchWarehouseCardsAsync(
            new WarehouseCardSearchRequest(
                request.SearchText,
                request.IncludePassive,
                request.Take),
            cancellationToken));

    [HttpGet("depolar/{warehouseNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseCardDetailDto>> GetWarehouseCard(
        [Range(1, int.MaxValue)] int warehouseNo,
        CancellationToken cancellationToken) =>
        Ok(await service.GetWarehouseCardAsync(warehouseNo, cancellationToken));

    [HttpPut("depolar/{warehouseNo:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(WarehouseCardUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseCardUpdateResponse>> UpdateWarehouseCard(
        [Range(1, int.MaxValue)] int warehouseNo,
        [FromBody] WarehouseCardPatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserWarehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.UpdateWarehouseCardAsync(
            new UpdateWarehouseCardRequest(
                warehouseNo,
                request.ToApplicationRequest(),
                currentUserWarehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.WarehouseCard,
            currentUserWarehouseNo,
            "DEPO",
            response.WarehouseCard.WarehouseNo.ToString(),
            DocumentFlowStep.MasterDataUpdated,
            $"Depo karti duzenlendi. Depo: {response.WarehouseCard.WarehouseNo}, guncellenen satir: {response.Summary.UpdatedRowCount}.",
            response.WarehouseCard.WarehouseNo.ToString(),
            cancellationToken,
            targetWarehouseNo: response.WarehouseCard.WarehouseNo);

        return Ok(response);
    }

    [HttpGet("cariler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<CustomerCardListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerCardListItemDto>>> SearchCustomerCards(
        [FromQuery] CustomerCardSearchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.SearchCustomerCardsAsync(
            new CustomerCardSearchRequest(
                request.SearchText,
                request.IncludePassive,
                request.Take),
            cancellationToken));

    [HttpGet("cariler/{customerCode}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(CustomerCardDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerCardDetailDto>> GetCustomerCard(
        string customerCode,
        CancellationToken cancellationToken) =>
        Ok(await service.GetCustomerCardAsync(customerCode, cancellationToken));

    [HttpPut("cariler/{customerCode}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CustomerCardUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerCardUpdateResponse>> UpdateCustomerCard(
        string customerCode,
        [FromBody] CustomerCardPatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.UpdateCustomerCardAsync(
            new UpdateCustomerCardRequest(
                customerCode,
                request.ToApplicationRequest(),
                warehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.CustomerCard,
            warehouseNo,
            "CARI",
            response.CustomerCard.CustomerCode,
            DocumentFlowStep.MasterDataUpdated,
            $"Cari karti duzenlendi. Guncellenen satir: {response.Summary.UpdatedRowCount}.",
            response.CustomerCard.CustomerCode,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("stok-kartlari/{stockCode}/satis-fiyatlari")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<StockSalesPriceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<StockSalesPriceDto>>> GetStockSalesPrices(
        string stockCode,
        [FromQuery] int? warehouseNo,
        CancellationToken cancellationToken) =>
        Ok(await service.GetStockSalesPricesAsync(stockCode, warehouseNo, cancellationToken));

    [HttpPut("stok-kartlari/{stockCode}/satis-fiyatlari/{warehouseNo:int}")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(StockSalesPriceUpsertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockSalesPriceUpsertResponse>> UpsertStockSalesPrice(
        string stockCode,
        [Range(1, int.MaxValue)] int warehouseNo,
        [FromBody] StockSalesPriceUpsertHttpRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserWarehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.UpsertStockSalesPriceAsync(
            new UpsertStockSalesPriceRequest(
                stockCode,
                warehouseNo,
                request.PriceListNo,
                request.PaymentPlanNo,
                request.UnitPointer,
                request.Price,
                request.CurrencyType,
                request.ChangeReason,
                currentUserWarehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.StockSalesPrice,
            currentUserWarehouseNo,
            "SATISFIYATI",
            $"{response.SalesPrice.StockCode}:{response.SalesPrice.WarehouseNo}:{response.SalesPrice.PriceListNo}:{response.SalesPrice.PaymentPlanNo}:{response.SalesPrice.UnitPointer}",
            DocumentFlowStep.PriceUpdated,
            response.Created
                ? $"Satis fiyati olusturuldu. Depo: {response.SalesPrice.WarehouseNo}, fiyat: {response.SalesPrice.Price}."
                : $"Satis fiyati guncellendi. Depo: {response.SalesPrice.WarehouseNo}, eski fiyat: {response.PreviousPrice}, yeni fiyat: {response.SalesPrice.Price}.",
            response.SalesPrice.StockCode,
            cancellationToken,
            targetWarehouseNo: response.SalesPrice.WarehouseNo);

        return Ok(response);
    }

    [HttpDelete("stok-kartlari/{stockCode}/satis-fiyatlari/{warehouseNo:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(typeof(MikroDocumentDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MikroDocumentDeleteResponse>> DeleteStockSalesPrice(
        string stockCode,
        [Range(1, int.MaxValue)] int warehouseNo,
        [FromQuery] StockSalesPriceDeleteHttpRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserWarehouseNo = User.GetRequiredWarehouseNo();
        var response = await service.DeleteStockSalesPriceAsync(
            new DeleteStockSalesPriceRequest(
                stockCode,
                warehouseNo,
                request.PriceListNo,
                request.PaymentPlanNo,
                request.UnitPointer,
                currentUserWarehouseNo),
            cancellationToken);

        await RecordReferenceFlowAsync(
            DocumentFlowType.StockSalesPrice,
            currentUserWarehouseNo,
            "SATISFIYATI",
            $"{stockCode}:{warehouseNo}:{request.PriceListNo}:{request.PaymentPlanNo}:{request.UnitPointer}",
            DocumentFlowStep.PriceDeleted,
            $"Satis fiyati silindi. Depo: {warehouseNo}, silinen satir: {response.DeletedRowCount}.",
            stockCode,
            cancellationToken,
            targetWarehouseNo: warehouseNo);

        return Ok(response);
    }

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
        CancellationToken cancellationToken)
    {
        var response = await service.UpdateStockMovementDocumentAsync(
            request.ToApplicationRequest(User.GetRequiredWarehouseNo()),
            cancellationToken);

        await RecordStockMovementFlowAsync(
            response.Document,
            DocumentFlowStep.DocumentUpdated,
            $"Stok hareket evraki duzenlendi. Guncellenen satir: {response.Summary.UpdatedRowCount}.",
            cancellationToken);

        return Ok(response);
    }

    [HttpDelete("stok-hareketleri")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(typeof(MikroDocumentDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MikroDocumentDeleteResponse>> DeleteStockMovementDocument(
        [FromQuery] StockMovementDocumentLookupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = request.ToApplicationRequest();
        var beforeDelete = await service.GetStockMovementDocumentAsync(lookup, cancellationToken);
        var response = await service.DeleteStockMovementDocumentAsync(
            new DeleteStockMovementDocumentRequest(
                lookup,
                User.GetRequiredWarehouseNo()),
            cancellationToken);

        await RecordStockMovementFlowAsync(
            beforeDelete,
            DocumentFlowStep.DocumentDeleted,
            $"Stok hareket evraki silindi. Silinen satir: {response.DeletedRowCount}.",
            cancellationToken);

        return Ok(response);
    }

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
        CancellationToken cancellationToken)
    {
        var response = await service.UpdateCustomerMovementDocumentAsync(
            request.ToApplicationRequest(User.GetRequiredWarehouseNo()),
            cancellationToken);

        await RecordCustomerMovementFlowAsync(
            response.Document,
            DocumentFlowStep.DocumentUpdated,
            $"Cari hareket evraki duzenlendi. Guncellenen satir: {response.Summary.UpdatedRowCount}.",
            cancellationToken);

        return Ok(response);
    }

    [HttpDelete("cari-hareketleri")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(typeof(MikroDocumentDeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MikroDocumentDeleteResponse>> DeleteCustomerMovementDocument(
        [FromQuery] CustomerMovementDocumentLookupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = request.ToApplicationRequest();
        var beforeDelete = await service.GetCustomerMovementDocumentAsync(lookup, cancellationToken);
        var response = await service.DeleteCustomerMovementDocumentAsync(
            new DeleteCustomerMovementDocumentRequest(
                lookup,
                User.GetRequiredWarehouseNo()),
            cancellationToken);

        await RecordCustomerMovementFlowAsync(
            beforeDelete,
            DocumentFlowStep.DocumentDeleted,
            $"Cari hareket evraki silindi. Silinen satir: {response.DeletedRowCount}.",
            cancellationToken);

        return Ok(response);
    }

    private Task RecordReferenceFlowAsync(
        DocumentFlowType documentType,
        int sourceWarehouseNo,
        string entityKind,
        string entityKey,
        DocumentFlowStep step,
        string message,
        string documentNo,
        CancellationToken cancellationToken,
        int? targetWarehouseNo = null) =>
        documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.CreateEntity(documentType, sourceWarehouseNo, entityKind, entityKey),
                documentType,
                sourceWarehouseNo,
                targetWarehouseNo,
                entityKind,
                0,
                step,
                DocumentFlowStatus.Succeeded,
                message,
                ChangedByUserId: User.GetRequiredUserId(),
                DocumentNo: documentNo,
                ExternalDocumentNo: entityKey),
            cancellationToken);

    private Task RecordStockMovementFlowAsync(
        StockMovementDocumentDto document,
        DocumentFlowStep step,
        string message,
        CancellationToken cancellationToken)
    {
        var identity = ResolveStockMovementFlowIdentity(document.Header);

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    identity.DocumentType,
                    identity.SourceWarehouseNo,
                    document.Header.DocumentSerie,
                    document.Header.DocumentOrderNo),
                identity.DocumentType,
                identity.SourceWarehouseNo,
                identity.TargetWarehouseNo,
                document.Header.DocumentSerie,
                document.Header.DocumentOrderNo,
                step,
                DocumentFlowStatus.Succeeded,
                message,
                ChangedByUserId: User.GetRequiredUserId(),
                DocumentNo: document.Header.DocumentNo,
                ExternalDocumentNo: document.Header.CustomerCode),
            cancellationToken);
    }

    private Task RecordCustomerMovementFlowAsync(
        CustomerMovementDocumentDto document,
        DocumentFlowStep step,
        string message,
        CancellationToken cancellationToken)
    {
        var sourceWarehouseNo = User.GetRequiredWarehouseNo();

        return documentFlowService.RecordAsync(
            new RecordDocumentFlowRequest(
                DocumentFlowKeys.Create(
                    DocumentFlowType.CustomerMovementDocument,
                    sourceWarehouseNo,
                    document.Header.DocumentSerie,
                    document.Header.DocumentOrderNo),
                DocumentFlowType.CustomerMovementDocument,
                sourceWarehouseNo,
                null,
                document.Header.DocumentSerie,
                document.Header.DocumentOrderNo,
                step,
                DocumentFlowStatus.Succeeded,
                message,
                ChangedByUserId: User.GetRequiredUserId(),
                DocumentNo: document.Header.DocumentNo,
                ExternalDocumentNo: document.Header.CustomerCode),
            cancellationToken);
    }

    private DocumentFlowIdentity ResolveStockMovementFlowIdentity(StockMovementDocumentHeaderDto header)
    {
        var movementTypes = header.MovementTypes;

        if (header.DocumentType == CompanyDispatchDocumentType &&
            header.NormalReturn == NormalMovement &&
            movementTypes.Contains(OutgoingMovementType))
        {
            return new DocumentFlowIdentity(
                DocumentFlowType.CompanyShipment,
                ResolveWarehouseNo(header.OutputWarehouseNo),
                null);
        }

        if (header.DocumentType == CompanyDispatchDocumentType &&
            header.NormalReturn == ReturnMovement &&
            movementTypes.Contains(OutgoingMovementType))
        {
            return new DocumentFlowIdentity(
                DocumentFlowType.CompanyReturn,
                ResolveWarehouseNo(header.OutputWarehouseNo),
                null);
        }

        if (header.DocumentType == ReceivingReceiptDocumentType &&
            movementTypes.Contains(IncomingMovementType))
        {
            return new DocumentFlowIdentity(
                DocumentFlowType.CompanyReceiving,
                ResolveWarehouseNo(header.InputWarehouseNo),
                null);
        }

        if (header.DocumentType == InterWarehouseShipmentDocumentType &&
            movementTypes.Contains(InterWarehouseMovementType))
        {
            return new DocumentFlowIdentity(
                header.NormalReturn == ReturnMovement
                    ? DocumentFlowType.WarehouseReturn
                    : DocumentFlowType.InterWarehouseShipment,
                ResolveWarehouseNo(header.OutputWarehouseNo),
                ResolveNullableWarehouseNo(header.InputWarehouseNo));
        }

        return new DocumentFlowIdentity(
            DocumentFlowType.StockMovementDocument,
            ResolveWarehouseNo(header.OutputWarehouseNo, header.InputWarehouseNo),
            ResolveNullableWarehouseNo(header.InputWarehouseNo));
    }

    private int ResolveWarehouseNo(params int[] candidates) =>
        candidates.FirstOrDefault(candidate => candidate > 0) is var warehouseNo && warehouseNo > 0
            ? warehouseNo
            : User.GetRequiredWarehouseNo();

    private static int? ResolveNullableWarehouseNo(int warehouseNo) =>
        warehouseNo > 0 ? warehouseNo : null;

    private sealed record DocumentFlowIdentity(
        DocumentFlowType DocumentType,
        int SourceWarehouseNo,
        int? TargetWarehouseNo);
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

public sealed class StockCardWarehousePatchHttpRequest
{
    public bool? SalesStopped { get; init; }

    public bool? OrderStopped { get; init; }

    public bool? ReceivingStopped { get; init; }

    public bool? IsPassive { get; init; }

    public bool? DiscountDisabled { get; init; }

    public bool ResetToGlobal { get; init; }

    public StockCardWarehousePatchDto ToApplicationRequest() =>
        new(
            SalesStopped,
            OrderStopped,
            ReceivingStopped,
            IsPassive,
            DiscountDisabled,
            ResetToGlobal);
}

public sealed class WarehouseCardSearchHttpRequest
{
    [StringLength(100)]
    public string? SearchText { get; init; }

    public bool IncludePassive { get; init; }

    [Range(1, 200)]
    public int Take { get; init; } = 50;
}

public sealed class WarehouseCardPatchHttpRequest
{
    [StringLength(50)]
    public string? Name { get; init; }

    [StringLength(25)]
    public string? GroupCode { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? WarehouseType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? ShipmentAutoPriceType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? MovementType { get; init; }

    [StringLength(40)]
    public string? AccountingCode { get; init; }

    [StringLength(25)]
    public string? ResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? ShipmentAppliedPriceNo { get; init; }

    public DateTime? LockDate { get; init; }

    [StringLength(50)]
    public string? Street { get; init; }

    [StringLength(50)]
    public string? Neighborhood { get; init; }

    [StringLength(50)]
    public string? Avenue { get; init; }

    [StringLength(25)]
    public string? Quarter { get; init; }

    [StringLength(10)]
    public string? ApartmentNo { get; init; }

    [StringLength(10)]
    public string? ApartmentUnitNo { get; init; }

    [StringLength(8)]
    public string? PostalCode { get; init; }

    [StringLength(50)]
    public string? District { get; init; }

    [StringLength(50)]
    public string? City { get; init; }

    [StringLength(50)]
    public string? Country { get; init; }

    [StringLength(10)]
    public string? AddressCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    [StringLength(50)]
    [EmailAddress]
    public string? AuthorizedEmail { get; init; }

    [StringLength(5)]
    public string? PhoneCountryCode { get; init; }

    [StringLength(5)]
    public string? PhoneAreaCode { get; init; }

    [StringLength(10)]
    public string? PhoneNo1 { get; init; }

    [StringLength(10)]
    public string? PhoneNo2 { get; init; }

    [StringLength(10)]
    public string? FaxNo { get; init; }

    public bool? ExcludedFromInventory { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? DetailTrackingType { get; init; }

    [StringLength(25)]
    public string? RegionCode { get; init; }

    public bool? OutgoingEDespatchEnabled { get; init; }

    public bool? IncomingEDespatchEnabled { get; init; }

    public bool? IsPassive { get; init; }

    public bool? IsHidden { get; init; }

    public bool? IsLocked { get; init; }

    public WarehouseCardPatchDto ToApplicationRequest() =>
        new(
            Name,
            GroupCode,
            WarehouseType,
            ShipmentAutoPriceType,
            MovementType,
            AccountingCode,
            ResponsibilityCenter,
            ProjectCode,
            ShipmentAppliedPriceNo,
            LockDate,
            Street,
            Neighborhood,
            Avenue,
            Quarter,
            ApartmentNo,
            ApartmentUnitNo,
            PostalCode,
            District,
            City,
            Country,
            AddressCode,
            Latitude,
            Longitude,
            AuthorizedEmail,
            PhoneCountryCode,
            PhoneAreaCode,
            PhoneNo1,
            PhoneNo2,
            FaxNo,
            ExcludedFromInventory,
            DetailTrackingType,
            RegionCode,
            OutgoingEDespatchEnabled,
            IncomingEDespatchEnabled,
            IsPassive,
            IsHidden,
            IsLocked);
}

public sealed class CustomerCardSearchHttpRequest
{
    [StringLength(100)]
    public string? SearchText { get; init; }

    public bool IncludePassive { get; init; }

    [Range(1, 200)]
    public int Take { get; init; } = 50;
}

public sealed class CustomerCardPatchHttpRequest
{
    [StringLength(127)]
    public string? Title1 { get; init; }

    [StringLength(127)]
    public string? Title2 { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? MovementType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? ConnectionType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? PurchaseStockType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? SalesStockType { get; init; }

    [StringLength(40)]
    public string? AccountingCode { get; init; }

    [StringLength(40)]
    public string? AccountingCode1 { get; init; }

    [StringLength(40)]
    public string? AccountingCode2 { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? CurrencyType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? CurrencyType1 { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? CurrencyType2 { get; init; }

    [StringLength(50)]
    public string? TaxOffice { get; init; }

    [StringLength(15)]
    public string? TaxOfficeNo { get; init; }

    [StringLength(15)]
    public string? RegistryNo { get; init; }

    [StringLength(10)]
    public string? TaxNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? SalesPriceListNo { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? PaymentType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? PaymentDay { get; init; }

    [Range(0, int.MaxValue)]
    public int? PaymentPlanNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? OptionDay { get; init; }

    [Range(0, int.MaxValue)]
    public int? InvoiceAddressNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? ShippingAddressNo { get; init; }

    [StringLength(25)]
    public string? ParentCustomerCode { get; init; }

    [StringLength(25)]
    public string? SectorCode { get; init; }

    [StringLength(25)]
    public string? RegionCode { get; init; }

    [StringLength(25)]
    public string? GroupCode { get; init; }

    [StringLength(25)]
    public string? RepresentativeCode { get; init; }

    public bool? IsClosed { get; init; }

    public bool? IsLocked { get; init; }

    public bool? EInvoiceEnabled { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? DefaultEInvoiceType { get; init; }

    public bool? EDespatchEnabled { get; init; }

    [Range(0, byte.MaxValue)]
    public byte? DefaultEDespatchType { get; init; }

    [StringLength(30)]
    public string? Website { get; init; }

    [StringLength(127)]
    [EmailAddress]
    public string? Email { get; init; }

    [StringLength(20)]
    public string? MobilePhone { get; init; }

    [Range(0, int.MaxValue)]
    public int? DefaultInputWarehouseNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? DefaultOutputWarehouseNo { get; init; }

    [StringLength(80)]
    public string? KepAddress { get; init; }

    [StringLength(80)]
    [EmailAddress]
    public string? ReconciliationEmail { get; init; }

    [StringLength(25)]
    public string? MersisNo { get; init; }

    [StringLength(10)]
    public string? TaxOfficeCode { get; init; }

    public bool? RetailCustomer { get; init; }

    public CustomerCardPatchDto ToApplicationRequest() =>
        new(
            Title1,
            Title2,
            MovementType,
            ConnectionType,
            PurchaseStockType,
            SalesStockType,
            AccountingCode,
            AccountingCode1,
            AccountingCode2,
            CurrencyType,
            CurrencyType1,
            CurrencyType2,
            TaxOffice,
            TaxOfficeNo,
            RegistryNo,
            TaxNo,
            SalesPriceListNo,
            PaymentType,
            PaymentDay,
            PaymentPlanNo,
            OptionDay,
            InvoiceAddressNo,
            ShippingAddressNo,
            ParentCustomerCode,
            SectorCode,
            RegionCode,
            GroupCode,
            RepresentativeCode,
            IsClosed,
            IsLocked,
            EInvoiceEnabled,
            DefaultEInvoiceType,
            EDespatchEnabled,
            DefaultEDespatchType,
            Website,
            Email,
            MobilePhone,
            DefaultInputWarehouseNo,
            DefaultOutputWarehouseNo,
            KepAddress,
            ReconciliationEmail,
            MersisNo,
            TaxOfficeCode,
            RetailCustomer);
}

public sealed class StockSalesPriceUpsertHttpRequest
{
    [Range(1, int.MaxValue)]
    public int PriceListNo { get; init; } = 1;

    [Range(0, int.MaxValue)]
    public int PaymentPlanNo { get; init; }

    [Range(1, 4)]
    public byte UnitPointer { get; init; } = 1;

    [Range(0.000001, double.MaxValue)]
    public double Price { get; init; }

    [Range(0, byte.MaxValue)]
    public byte CurrencyType { get; init; }

    [Range(0, byte.MaxValue)]
    public byte ChangeReason { get; init; } = 4;
}

public sealed class StockSalesPriceDeleteHttpRequest
{
    [Range(1, int.MaxValue)]
    public int PriceListNo { get; init; } = 1;

    [Range(0, int.MaxValue)]
    public int PaymentPlanNo { get; init; }

    [Range(1, 4)]
    public byte UnitPointer { get; init; } = 1;
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

    public DateTime? GoodsAcceptanceDate { get; init; }

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
            GoodsAcceptanceDate,
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

    public DateTime? GoodsAcceptanceDate { get; init; }

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
            GoodsAcceptanceDate,
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
