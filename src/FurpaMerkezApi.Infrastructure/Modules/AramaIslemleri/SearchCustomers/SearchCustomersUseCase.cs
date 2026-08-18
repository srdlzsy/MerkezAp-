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
                   (customer.cari_vdaire_no != null && EF.Functions.Like(customer.cari_vdaire_no, like)) ||
                   (customer.cari_vdaire_adi != null && EF.Functions.Like(customer.cari_vdaire_adi, like)) ||
                   (customer.cari_Ana_cari_kodu != null && EF.Functions.Like(customer.cari_Ana_cari_kodu, like)) ||
                   (customer.cari_bolge_kodu != null && EF.Functions.Like(customer.cari_bolge_kodu, like)) ||
                   (customer.cari_grup_kodu != null && EF.Functions.Like(customer.cari_grup_kodu, like)) ||
                   (customer.cari_sektor_kodu != null && EF.Functions.Like(customer.cari_sektor_kodu, like)) ||
                   (customer.cari_temsilci_kodu != null && EF.Functions.Like(customer.cari_temsilci_kodu, like)) ||
                   (customer.cari_EMail != null && EF.Functions.Like(customer.cari_EMail, like)) ||
                   (customer.cari_CepTel != null && EF.Functions.Like(customer.cari_CepTel, like)))
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
                customer.cari_vdaire_adi,
                customer.cari_Ana_cari_kodu,
                customer.cari_bolge_kodu,
                customer.cari_grup_kodu,
                customer.cari_sektor_kodu,
                customer.cari_temsilci_kodu,
                customer.cari_EMail,
                customer.cari_CepTel,
                customer.cari_fatura_adres_no,
                customer.cari_sevk_adres_no,
                customer.cari_cari_kilitli_flg,
                customer.cari_firma_acik_kapal,
                customer.cari_efatura_fl,
                customer.cari_eirsaliye_fl,
                RepresentativeName = representative.cari_per_adi,
                RepresentativeSurname = representative.cari_per_soyadi
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        var taxNumbers = customers
            .Select(customer => FirstNonEmpty(customer.cari_VergiKimlikNo, customer.cari_vdaire_no))
            .Where(taxNumber => taxNumber.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sameTaxCustomerCounts = await GetSameTaxCustomerCountsAsync(taxNumbers, cancellationToken);

        return customers
            .Select(customer =>
            {
                var customerCode = customer.cari_kod ?? string.Empty;
                var customerName = customer.cari_unvan1 ?? string.Empty;
                var customerTitle = customer.cari_unvan2 ?? string.Empty;
                var displayName = JoinNonEmpty(customer.cari_unvan1, customer.cari_unvan2);
                var taxNumber = FirstNonEmpty(customer.cari_VergiKimlikNo, customer.cari_vdaire_no);
                var representativeName = JoinNonEmpty(customer.RepresentativeName, customer.RepresentativeSurname);
                var sameTaxCustomerCount = taxNumber.Length > 0 &&
                                           sameTaxCustomerCounts.TryGetValue(taxNumber, out var count)
                    ? count
                    : 0;

                return new CustomerLookupItemDto(
                    customerCode,
                    customerName,
                    customerTitle,
                    displayName,
                    taxNumber,
                    customer.cari_VergiKimlikNo ?? string.Empty,
                    customer.cari_vdaire_no ?? string.Empty,
                    customer.cari_vdaire_adi ?? string.Empty,
                    customer.cari_Ana_cari_kodu ?? string.Empty,
                    customer.cari_bolge_kodu ?? string.Empty,
                    customer.cari_grup_kodu ?? string.Empty,
                    customer.cari_sektor_kodu ?? string.Empty,
                    customer.cari_temsilci_kodu ?? string.Empty,
                    representativeName,
                    customer.cari_CepTel ?? string.Empty,
                    customer.cari_EMail ?? string.Empty,
                    customer.cari_fatura_adres_no,
                    customer.cari_sevk_adres_no,
                    customer.cari_cari_kilitli_flg ?? false,
                    customer.cari_firma_acik_kapal ?? false,
                    customer.cari_efatura_fl ?? false,
                    customer.cari_eirsaliye_fl ?? false,
                    sameTaxCustomerCount,
                    BuildSelectionLabel(
                        customerCode,
                        displayName,
                        taxNumber,
                        customer.cari_grup_kodu,
                        representativeName,
                        sameTaxCustomerCount));
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<string, int>> GetSameTaxCustomerCountsAsync(
        IReadOnlyCollection<string> taxNumbers,
        CancellationToken cancellationToken)
    {
        if (taxNumbers.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer =>
                customer.cari_iptal != true &&
                ((customer.cari_VergiKimlikNo != null && taxNumbers.Contains(customer.cari_VergiKimlikNo)) ||
                 (customer.cari_vdaire_no != null && taxNumbers.Contains(customer.cari_vdaire_no))))
            .Select(customer => new
            {
                customer.cari_VergiKimlikNo,
                customer.cari_vdaire_no
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => FirstNonEmpty(row.cari_VergiKimlikNo, row.cari_vdaire_no))
            .Where(taxNumber => taxNumber.Length > 0)
            .GroupBy(taxNumber => taxNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
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

    private static string BuildSelectionLabel(
        string customerCode,
        string displayName,
        string taxNumber,
        string? groupCode,
        string representativeName,
        int sameTaxCustomerCount)
    {
        var labelParts = new List<string>();
        var title = JoinNonEmpty(customerCode, displayName);

        if (title.Length > 0)
        {
            labelParts.Add(title);
        }

        if (!string.IsNullOrWhiteSpace(taxNumber))
        {
            labelParts.Add($"VKN/TCKN: {taxNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(groupCode))
        {
            labelParts.Add($"Grup: {groupCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(representativeName))
        {
            labelParts.Add($"Temsilci: {representativeName.Trim()}");
        }

        if (sameTaxCustomerCount > 1)
        {
            labelParts.Add($"Ayni vergi no: {sameTaxCustomerCount}");
        }

        return string.Join(" | ", labelParts);
    }
}
