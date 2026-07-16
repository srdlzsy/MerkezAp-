using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

public sealed class CompanyOrderListQueryExecutor(MikroDbContext mikroDbContext)
{
    private const double QuantityTolerance = 0.000001d;

    internal async Task<IReadOnlyCollection<CompanyOrderListItemDto>> ExecuteAsync(
        CompanyOrderListRequest request,
        CompanyOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.");
        }

        var endDateExclusive = endDate.AddDays(1);
        var orderType = direction == CompanyOrderListDirection.Issued ? (byte)1 : (byte)0;
        var customerCode = NormalizeOrNull(request.CustomerCode);
        var onlyOpen = request.OnlyOpen;
        var orders = mikroDbContext.SIPARISLERs.AsNoTracking()
            .Where(order =>
                order.sip_tarih.HasValue &&
                order.sip_tarih.Value >= startDate &&
                order.sip_tarih.Value < endDateExclusive &&
                (!request.WarehouseNo.HasValue || order.sip_depono == request.WarehouseNo.Value) &&
                order.sip_cins == 0 &&
                order.sip_tip == orderType);

        if (customerCode is not null)
        {
            orders = orders.Where(order => order.sip_musteri_kod == customerCode);
        }

        if (onlyOpen)
        {
            orders = orders.Where(order => order.sip_iptal != true && order.sip_kapat_fl != true);
        }

        var groupedQuery =
            from order in orders
            join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking() on order.sip_musteri_kod equals customer.cari_kod into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join address in mikroDbContext.CARI_HESAP_ADRESLERIs.AsNoTracking()
                on new
                {
                    CustomerCode = order.sip_musteri_kod,
                    AddressNo = order.sip_adresno ?? 1
                }
                equals new
                {
                    CustomerCode = address.adr_cari_kod,
                    AddressNo = address.adr_adres_no ?? 0
                }
                into addressGroup
            from address in addressGroup.DefaultIfEmpty()
            group new { order, customer, address }
            by new
            {
                order.sip_tarih,
                order.sip_teslim_tarih,
                order.sip_evrakno_seri,
                order.sip_evrakno_sira,
                order.sip_belgeno,
                order.sip_depono,
                order.sip_aciklama,
                order.sip_aciklama2,
                order.sip_HareketGrupKodu2,
                order.sip_HareketGrupKodu3,
                order.sip_cagrilabilir_fl,
                order.sip_musteri_kod,
                CustomerName = customer.cari_unvan1,
                CustomerTitle = customer.cari_unvan2,
                CustomerRepresentativeCode = address.adr_temsilci_kodu ?? customer.cari_temsilci_kodu,
                AddressLine = address.adr_cadde,
                Neighborhood = address.adr_mahalle,
                Street = address.adr_sokak,
                District = address.adr_ilce,
                Province = address.adr_il
            }
            into grouped
            select new
            {
                grouped.Key.sip_tarih,
                grouped.Key.sip_teslim_tarih,
                grouped.Key.sip_evrakno_seri,
                grouped.Key.sip_evrakno_sira,
                grouped.Key.sip_belgeno,
                grouped.Key.sip_depono,
                grouped.Key.sip_musteri_kod,
                grouped.Key.CustomerName,
                grouped.Key.CustomerTitle,
                grouped.Key.AddressLine,
                grouped.Key.Neighborhood,
                grouped.Key.Street,
                grouped.Key.District,
                grouped.Key.Province,
                grouped.Key.sip_aciklama,
                grouped.Key.sip_aciklama2,
                grouped.Key.sip_HareketGrupKodu2,
                grouped.Key.sip_HareketGrupKodu3,
                grouped.Key.sip_cagrilabilir_fl,
                grouped.Key.CustomerRepresentativeCode,
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(item => item.order.sip_miktar ?? 0d),
                TotalDeliveredQuantity = grouped.Sum(item => item.order.sip_teslim_miktar ?? 0d),
                TotalAmount = grouped.Sum(item => item.order.sip_tutar ?? 0d)
            };

        if (onlyOpen)
        {
            groupedQuery = groupedQuery.Where(document => document.TotalQuantity > document.TotalDeliveredQuantity);
        }

        var query = groupedQuery
            .OrderBy(document => document.sip_tarih)
            .ThenBy(document => document.sip_evrakno_seri)
            .ThenBy(document => document.sip_evrakno_sira);

        var documents = await query.ToListAsync(cancellationToken);

        return documents
            .Select(document => new CompanyOrderListItemDto(
                CompanyOrderDocumentKey.CreateOrNull(
                    document.sip_depono ?? request.WarehouseNo ?? 0,
                    document.sip_evrakno_seri,
                    document.sip_evrakno_sira ?? 0),
                document.sip_tarih ?? DateTime.MinValue,
                document.sip_teslim_tarih,
                document.sip_evrakno_seri ?? string.Empty,
                document.sip_evrakno_sira ?? 0,
                document.sip_belgeno ?? string.Empty,
                document.sip_depono ?? request.WarehouseNo ?? 0,
                document.sip_musteri_kod ?? string.Empty,
                document.CustomerName ?? string.Empty,
                document.CustomerTitle ?? string.Empty,
                JoinNonEmpty(document.CustomerName, document.CustomerTitle),
                JoinNonEmpty(document.AddressLine, document.Neighborhood, document.Street, document.District, document.Province),
                document.sip_aciklama ?? string.Empty,
                document.sip_aciklama2 ?? string.Empty,
                document.sip_HareketGrupKodu2 ?? string.Empty,
                document.sip_HareketGrupKodu3 ?? string.Empty,
                document.sip_cagrilabilir_fl ?? false,
                document.CustomerRepresentativeCode ?? string.Empty,
                document.LineCount,
                document.TotalQuantity,
                document.TotalDeliveredQuantity,
                document.TotalQuantity - document.TotalDeliveredQuantity,
                document.TotalQuantity - document.TotalDeliveredQuantity <= QuantityTolerance,
                document.TotalAmount))
            .ToArray();
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
}
