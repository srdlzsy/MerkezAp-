using FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Interfaces;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Models;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.YeniKasaYonetimi;

[ApiController]
[Route("api/ayar-islemleri/yeni-kasa-yonetimi")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class YeniKasaYonetimiController(IYeniKasaYonetimiService service)
    : ModuleMenuControllerBase("ayar-islemleri", "AyarIslemleri", "yeni-kasa-yonetimi", "YeniKasaYonetimi")
{
    private const string ListPolicy = "ayar-islemleri.yeni-kasa-yonetimi.list";
    private const string CreatePolicy = "ayar-islemleri.yeni-kasa-yonetimi.create";
    private const string UpdatePolicy = "ayar-islemleri.yeni-kasa-yonetimi.update";
    private const string DeletePolicy = "ayar-islemleri.yeni-kasa-yonetimi.delete";

    #region Genel Ayarlar & FTP Testi & Sync Push

    [HttpGet("genel-ayarlar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(YeniKasaAppSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<YeniKasaAppSettingsDto?>> GetAppSettings(CancellationToken cancellationToken) =>
        Ok(await service.GetAppSettingsAsync(cancellationToken));

    [HttpPut("genel-ayarlar")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateAppSettings(
        [FromBody] UpdateYeniKasaAppSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateAppSettingsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("genel-ayarlar/test-ftp")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(TestFtpConnectionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestFtpConnectionResultDto>> TestFtpConnection(
        [FromBody] TestFtpConnectionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.TestFtpConnectionAsync(request, cancellationToken));

    [HttpPost("sync-push")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(SyncPushResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncPushResultDto>> TriggerSyncPush(CancellationToken cancellationToken) =>
        Ok(await service.TriggerSyncPushAsync(cancellationToken));

    #endregion

    #region Ödeme Yöntemleri

    [HttpGet("odeme-yontemleri")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<YeniKasaPaymentMethodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YeniKasaPaymentMethodDto>>> GetPaymentMethods(CancellationToken cancellationToken) =>
        Ok(await service.GetPaymentMethodsAsync(cancellationToken));

    [HttpGet("odeme-yontemleri/lookups")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(YeniKasaPaymentMethodLookupsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<YeniKasaPaymentMethodLookupsDto>> GetPaymentMethodLookups(CancellationToken cancellationToken) =>
        Ok(await service.GetPaymentMethodLookupsAsync(cancellationToken));

    [HttpPut("odeme-yontemleri")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePaymentMethod(
        [FromBody] UpdateYeniKasaPaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdatePaymentMethodAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPut("odeme-yontemleri/siralama")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePaymentMethodsOrder(
        [FromBody] UpdatePaymentMethodsOrderRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdatePaymentMethodsOrderAsync(request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Kasa Yetki Matrisi & Rol Kopyalama

    [HttpGet("yetki-matrisi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(YeniKasaRolePermissionMatrixDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<YeniKasaRolePermissionMatrixDto>> GetRolePermissionMatrix(CancellationToken cancellationToken) =>
        Ok(await service.GetRolePermissionMatrixAsync(cancellationToken));

    [HttpPut("yetki-matrisi")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateRolePermissions(
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateRolePermissionsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPut("yetki-matrisi/toplu")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkUpdateRolePermissions(
        [FromBody] BulkUpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        await service.BulkUpdateRolePermissionsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("yetki-matrisi/kopyala")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(CopyRolePermissionsResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CopyRolePermissionsResultDto>> CopyRolePermissions(
        [FromBody] CopyRolePermissionsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.CopyRolePermissionsAsync(request, cancellationToken));

    #endregion

    #region Müşteri Ekranı (Display) & Sıralama

    [HttpGet("musteri-ekrani/ayarlar")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<YeniKasaDisplaySettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YeniKasaDisplaySettingsDto>>> GetDisplaySettings(CancellationToken cancellationToken) =>
        Ok(await service.GetDisplaySettingsAsync(cancellationToken));

    [HttpPut("musteri-ekrani/ayarlar")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateDisplaySettings(
        [FromBody] UpdateYeniKasaDisplaySettingsRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateDisplaySettingsAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("musteri-ekrani/icerikler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<YeniKasaDisplayContentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YeniKasaDisplayContentDto>>> GetDisplayContents(
        [FromQuery] string? pageType,
        CancellationToken cancellationToken) =>
        Ok(await service.GetDisplayContentsAsync(pageType, cancellationToken));

    [HttpPost("musteri-ekrani/icerikler")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(YeniKasaDisplayContentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<YeniKasaDisplayContentDto>> CreateDisplayContent(
        [FromBody] CreateYeniKasaDisplayContentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateDisplayContentAsync(request, cancellationToken);
        return Created($"/api/ayar-islemleri/yeni-kasa-yonetimi/musteri-ekrani/icerikler/{result.Id}", result);
    }

    [HttpPut("musteri-ekrani/siralama")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateDisplayContentsOrder(
        [FromBody] UpdateDisplayContentsOrderRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateDisplayContentsOrderAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("musteri-ekrani/icerikler/{id:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDisplayContent(int id, CancellationToken cancellationToken)
    {
        await service.DeleteDisplayContentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("musteri-gorsel/{fileName}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDisplayContentImage(string fileName, CancellationToken cancellationToken)
    {
        var image = await service.GetDisplayContentImageAsync(fileName, cancellationToken);
        if (image == null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(image.Value.Bytes, image.Value.ContentType, enableRangeProcessing: true);
    }

    #endregion

    #region Hızlı Tuş Grupları & Ürünler & Mikro Stok Arama

    [HttpGet("hizli-tus/kategoriler")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<YeniKasaFastAccessCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YeniKasaFastAccessCategoryDto>>> GetFastAccessCategories(CancellationToken cancellationToken) =>
        Ok(await service.GetFastAccessCategoriesAsync(cancellationToken));

    [HttpPost("hizli-tus/kategoriler")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(YeniKasaFastAccessCategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<YeniKasaFastAccessCategoryDto>> CreateFastAccessCategory(
        [FromBody] CreateFastAccessCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateFastAccessCategoryAsync(request, cancellationToken);
        return Created($"/api/ayar-islemleri/yeni-kasa-yonetimi/hizli-tus/kategoriler/{result.Id}", result);
    }

    [HttpPut("hizli-tus/kategoriler")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateFastAccessCategory(
        [FromBody] UpdateFastAccessCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await service.UpdateFastAccessCategoryAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("hizli-tus/kategoriler/{id:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFastAccessCategory(int id, CancellationToken cancellationToken)
    {
        await service.DeleteFastAccessCategoryAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("hizli-tus/urunler/{categoryKey}")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<YeniKasaFastAccessItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YeniKasaFastAccessItemDto>>> GetFastAccessItems(
        string categoryKey,
        CancellationToken cancellationToken) =>
        Ok(await service.GetFastAccessItemsByCategoryKeyAsync(categoryKey, cancellationToken));

    [HttpPost("hizli-tus/urunler")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(YeniKasaFastAccessItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<YeniKasaFastAccessItemDto>> CreateFastAccessItem(
        [FromBody] CreateFastAccessItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateFastAccessItemAsync(request, cancellationToken);
        return Created($"/api/ayar-islemleri/yeni-kasa-yonetimi/hizli-tus/urunler/{result.Id}", result);
    }

    [HttpDelete("hizli-tus/urunler/{id:int}")]
    [Authorize(Policy = DeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFastAccessItem(int id, CancellationToken cancellationToken)
    {
        await service.DeleteFastAccessItemAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("urun-gorsel/{inventoryCode}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFastAccessItemImage(string inventoryCode, CancellationToken cancellationToken)
    {
        var image = await service.GetFastAccessItemImageAsync(inventoryCode, cancellationToken);
        if (image == null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(image.Value.Bytes, image.Value.ContentType);
    }

    [HttpGet("stok-ara")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<YeniKasaStockLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YeniKasaStockLookupDto>>> SearchStocks(
        [FromQuery] string q,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await service.SearchStocksAsync(q, take, cancellationToken));

    #endregion
}
