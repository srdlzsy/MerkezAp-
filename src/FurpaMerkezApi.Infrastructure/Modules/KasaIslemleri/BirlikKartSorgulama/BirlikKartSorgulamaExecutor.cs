using FurpaMerkezApi.Application.Modules.KasaIslemleri.BirlikKartSorgulama;
using FurpaMerkezApi.Infrastructure.Persistence.Puan;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BirlikKartSorgulama;

public sealed class BirlikKartSorgulamaExecutor(PuanDbContext puanDbContext) : IBirlikKartSorgulamaUseCase
{
    public async Task<BirlikKartDetayResponse> DetayAsync(
        BirlikKartDetayRequest request,
        CancellationToken cancellationToken)
    {
        var cekNo = request.CekNo?.Trim();
        var kart = await puanDbContext.InterbonusIndirimCeks
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Cek_No == cekNo, cancellationToken);

        if (kart == null)
        {
            return new BirlikKartDetayResponse
            {
                IsFound = false,
                KartNo = cekNo,
                CekNo = cekNo,
                Message = "Kart veya cek kaydi bulunamadi."
            };
        }

        return new BirlikKartDetayResponse
        {
            IsFound = true,
            KartNo = cekNo,
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

    public async Task<BirlikKartGuncelleResponse> GuncelleAsync(
        BirlikKartSorgulamaGuncelleRequest request,
        CancellationToken cancellationToken)
    {
        var cekNo = request.CekNo?.Trim();
        var cariKod = request.CariKod?.Trim();
        var kart = await puanDbContext.InterbonusIndirimCeks
            .FirstOrDefaultAsync(item => item.Cek_No == cekNo, cancellationToken);

        if (kart == null)
        {
            return new BirlikKartGuncelleResponse
            {
                IsUpdated = false,
                Message = "Kart veya cek kaydi bulunamadi."
            };
        }

        if (string.IsNullOrWhiteSpace(cariKod))
        {
            return new BirlikKartGuncelleResponse
            {
                IsUpdated = false,
                Message = "CariKod zorunludur."
            };
        }

        if (!string.Equals(kart.Cari_Kod?.Trim(), cariKod, StringComparison.OrdinalIgnoreCase))
        {
            return new BirlikKartGuncelleResponse
            {
                IsUpdated = false,
                Message = "Cari kodu farkli oldugu icin kart veya cek kaydi guncellenemedi."
            };
        }

        kart.Cari_Kod = cariKod;
        kart.Tutar = request.Tutar;
        kart.Puan = request.Puan;
        kart.Baslangic = request.Baslangic;
        kart.Bitis = request.Bitis;
        kart.Flag = request.Flag;
        kart.Sube_Kodu = request.SubeKodu;
        kart.Kasa_No = request.KasaNo;
        kart.Kart_Tipi = request.KartTipi;

        await puanDbContext.SaveChangesAsync(cancellationToken);

        return new BirlikKartGuncelleResponse
        {
            IsUpdated = true,
            Message = "Kart veya cek kaydi guncellendi."
        };
    }

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
    public Task<BirlikKartDetayResponse> DetayAsync(BirlikKartDetayRequest request, CancellationToken cancellationToken)
    {
        var response = new BirlikKartDetayResponse
        {
            IsFound = false,
            KartNo = request.CekNo?.Trim(),
            CekNo = request.CekNo?.Trim(),
            Message = "PuanConnection tanimli olmadigi icin Birlik Kart detay kullanilamaz."
        };

        return Task.FromResult(response);
    }

    public Task<BirlikKartGuncelleResponse> GuncelleAsync(BirlikKartSorgulamaGuncelleRequest request, CancellationToken cancellationToken)
    {
        var response = new BirlikKartGuncelleResponse
        {
            IsUpdated = false,
            Message = "PuanConnection tanimli olmadigi icin Birlik Kart guncelleme kullanilamaz."
        };

        return Task.FromResult(response);
    }

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
