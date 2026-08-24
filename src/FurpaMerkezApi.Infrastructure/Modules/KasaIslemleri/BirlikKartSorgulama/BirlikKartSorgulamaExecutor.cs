using FurpaMerkezApi.Application.Modules.KasaIslemleri.BirlikKartSorgulama;
using FurpaMerkezApi.Infrastructure.Persistence.Puan;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BirlikKartSorgulama;

public sealed class BirlikKartSorgulamaExecutor(PuanDbContext puanDbContext) : IBirlikKartSorgulamaUseCase
{
    public async Task<BirlikKartSorgulamaResponse> SorgulaAsync(
        BirlikKartSorgulamaRequest request,
        CancellationToken cancellationToken)
    {
        var kartNo = request.KartNo?.Trim();
        var kart = await puanDbContext.InterbonusIndirimCeks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Cek_No == kartNo,
                cancellationToken);

        if (kart == null)
        {
            return new BirlikKartSorgulamaResponse
            {
                IsFound = false,
                KartNo = kartNo,
                Message = "Kart veya cek kaydi bulunamadi."
            };
        }

        return new BirlikKartSorgulamaResponse
        {
            IsFound = true,
            KartNo = kartNo,
            CekNo = kart.Cek_No,
            CariKod = kart.Cari_Kod,
            Tutar = kart.Tutar,
            Puan = kart.Puan,
            Baslangic = kart.Baslangic,
            Bitis = kart.Bitis,
            Flag = kart.Flag,
            SubeKodu = kart.Sube_Kodu,
            KasaNo = kart.Kasa_No,
            KartTipi = kart.Kart_Tipi
        };
    }
}

public sealed class PuanConnectionNotConfiguredBirlikKartSorgulamaExecutor : IBirlikKartSorgulamaUseCase
{
    public Task<BirlikKartSorgulamaResponse> SorgulaAsync(
        BirlikKartSorgulamaRequest request,
        CancellationToken cancellationToken)
    {
        var response = new BirlikKartSorgulamaResponse
        {
            IsFound = false,
            KartNo = request.KartNo?.Trim(),
            Message = "PuanConnection tanimli olmadigi icin Birlik Kart sorgulama kullanilamaz."
        };

        return Task.FromResult(response);
    }
}
