using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.Manav;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.Manav;

public sealed class ListGreenGrocerSuggestedProductsUseCase(MikroDbContext mikroDbContext)
    : IListGreenGrocerSuggestedProductsUseCase
{
    private static readonly string[] GreenGrocerModelCodes = ["10", "11", "12", "23"];

    public async Task<IReadOnlyCollection<GreenGrocerSuggestedProductDto>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        return await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock =>
                 stock.sto_isim != null &&
                stock.sto_isim.StartsWith("MNV") &&
                stock.sto_kod != null &&
                stock.sto_iptal == false &&
                stock.sto_model_kodu != null &&
                GreenGrocerModelCodes.Contains(stock.sto_model_kodu.Trim()))
            .OrderBy(stock => stock.sto_isim)
            .Select(stock => new GreenGrocerSuggestedProductDto(
                stock.sto_kod ?? string.Empty,
                stock.sto_isim ?? string.Empty,
                stock.sto_model_kodu ?? string.Empty,
                GetModelName(stock.sto_model_kodu),
                stock.sto_birim1_ad ?? string.Empty,
                0,
                0,
                0,
                1))
            .ToListAsync(cancellationToken);
    }

    private static string GetModelName(string? modelCode) =>
        modelCode?.Trim() switch
        {
            "10" => "Meyve",
            "11" => "Sebze",
            "12" => "Yesillik",
            "23" => "Manav Sarf",
            _ => string.Empty
        };
}
