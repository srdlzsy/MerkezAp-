using FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Models;

namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Interfaces;

public interface IYeniKasaYonetimiService
{
    // Genel Ayarlar & FTP Testi
    Task<YeniKasaAppSettingsDto?> GetAppSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateAppSettingsAsync(UpdateYeniKasaAppSettingsRequest request, CancellationToken cancellationToken = default);
    Task<TestFtpConnectionResultDto> TestFtpConnectionAsync(TestFtpConnectionRequest request, CancellationToken cancellationToken = default);
    Task<SyncPushResultDto> TriggerSyncPushAsync(CancellationToken cancellationToken = default);

    // Ödeme Yöntemleri
    Task<IReadOnlyList<YeniKasaPaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);
    Task<YeniKasaPaymentMethodLookupsDto> GetPaymentMethodLookupsAsync(CancellationToken cancellationToken = default);
    Task UpdatePaymentMethodAsync(UpdateYeniKasaPaymentMethodRequest request, CancellationToken cancellationToken = default);
    Task UpdatePaymentMethodsOrderAsync(UpdatePaymentMethodsOrderRequest request, CancellationToken cancellationToken = default);

    // Kasa Yetki Matrisi & Rol Kopyalama
    Task<YeniKasaRolePermissionMatrixDto> GetRolePermissionMatrixAsync(CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    Task BulkUpdateRolePermissionsAsync(BulkUpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    Task<CopyRolePermissionsResultDto> CopyRolePermissionsAsync(CopyRolePermissionsRequest request, CancellationToken cancellationToken = default);

    // Müşteri Ekranı (Display) & Sıralama
    Task<IReadOnlyList<YeniKasaDisplaySettingsDto>> GetDisplaySettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateDisplaySettingsAsync(UpdateYeniKasaDisplaySettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YeniKasaDisplayContentDto>> GetDisplayContentsAsync(string? pageType = null, CancellationToken cancellationToken = default);
    Task<YeniKasaDisplayContentDto> CreateDisplayContentAsync(CreateYeniKasaDisplayContentRequest request, CancellationToken cancellationToken = default);
    Task UpdateDisplayContentsOrderAsync(UpdateDisplayContentsOrderRequest request, CancellationToken cancellationToken = default);
    Task DeleteDisplayContentAsync(int id, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string ContentType)?> GetDisplayContentImageAsync(string fileName, CancellationToken cancellationToken = default);

    // Hızlı Tuş Grupları & Ürünler & Mikro Stok Arama
    Task<IReadOnlyList<YeniKasaFastAccessCategoryDto>> GetFastAccessCategoriesAsync(CancellationToken cancellationToken = default);
    Task<YeniKasaFastAccessCategoryDto> CreateFastAccessCategoryAsync(CreateFastAccessCategoryRequest request, CancellationToken cancellationToken = default);
    Task UpdateFastAccessCategoryAsync(UpdateFastAccessCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteFastAccessCategoryAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YeniKasaFastAccessItemDto>> GetFastAccessItemsByCategoryKeyAsync(string categoryKey, CancellationToken cancellationToken = default);
    Task<YeniKasaFastAccessItemDto> CreateFastAccessItemAsync(CreateFastAccessItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteFastAccessItemAsync(int id, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string ContentType)?> GetFastAccessItemImageAsync(string inventoryCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YeniKasaStockLookupDto>> SearchStocksAsync(string query, int take = 20, CancellationToken cancellationToken = default);
}
