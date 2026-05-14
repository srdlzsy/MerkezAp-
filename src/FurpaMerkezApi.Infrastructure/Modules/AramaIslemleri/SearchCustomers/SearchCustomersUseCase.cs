using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchCustomers;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchCustomers;

public sealed class SearchCustomersUseCase(MikroDbContext mikroDbContext) : ISearchCustomersUseCase
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public async Task<IReadOnlyCollection<CustomerLookupItemDto>> ExecuteAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken)
    {
        var searchText = Normalize(request.SearchText);

        if (searchText.Length < 2)
        {
            throw new ArgumentException("Customer search text must be at least 2 characters.", nameof(request.SearchText));
        }

        var take = NormalizeTake(request.Take);
        var like = $"%{searchText}%";

        var customers = await (
            from customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
            where customer.cari_iptal != true &&
                  ((customer.cari_kod != null && EF.Functions.Like(customer.cari_kod, like)) ||
                   (customer.cari_unvan1 != null && EF.Functions.Like(customer.cari_unvan1, like)) ||
                   (customer.cari_unvan2 != null && EF.Functions.Like(customer.cari_unvan2, like)) ||
                   (customer.cari_VergiKimlikNo != null && EF.Functions.Like(customer.cari_VergiKimlikNo, like)) ||
                   (customer.cari_vdaire_no != null && EF.Functions.Like(customer.cari_vdaire_no, like)))
            join representative in mikroDbContext.CARI_PERSONEL_TANIMLARIs.AsNoTracking()
                on customer.cari_temsilci_kodu equals representative.cari_per_kod into representativeGroup
            from representative in representativeGroup.DefaultIfEmpty()
            orderby customer.cari_kod
            select new
            {
                customer.cari_kod,
                customer.cari_unvan1,
                customer.cari_unvan2,
                customer.cari_VergiKimlikNo,
                customer.cari_vdaire_no,
                customer.cari_temsilci_kodu,
                customer.cari_fatura_adres_no,
                customer.cari_sevk_adres_no,
                customer.cari_cari_kilitli_flg,
                customer.cari_firma_acik_kapal,
                RepresentativeName = representative.cari_per_adi,
                RepresentativeSurname = representative.cari_per_soyadi
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        return customers
            .Select(customer => new CustomerLookupItemDto(
                customer.cari_kod ?? string.Empty,
                customer.cari_unvan1 ?? string.Empty,
                customer.cari_unvan2 ?? string.Empty,
                JoinNonEmpty(customer.cari_unvan1, customer.cari_unvan2),
                FirstNonEmpty(customer.cari_VergiKimlikNo, customer.cari_vdaire_no),
                customer.cari_temsilci_kodu ?? string.Empty,
                JoinNonEmpty(customer.RepresentativeName, customer.RepresentativeSurname),
                customer.cari_fatura_adres_no,
                customer.cari_sevk_adres_no,
                customer.cari_cari_kilitli_flg ?? false,
                customer.cari_firma_acik_kapal ?? false))
            .ToArray();
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string Normalize(string? value) =>
        value?.Trim() ?? string.Empty;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
}
