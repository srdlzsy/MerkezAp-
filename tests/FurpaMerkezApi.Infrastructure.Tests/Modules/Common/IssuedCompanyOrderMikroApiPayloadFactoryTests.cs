using FurpaMerkezApi.Application.Modules.Common.PurchasePricing;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class IssuedCompanyOrderMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_PreservesPurchaseFinancialBreakdown()
    {
        var snapshot = new PurchaseFinancialSnapshot(
            Guid.NewGuid(),
            "purchase-requirement",
            53.25m,
            51.1308614025m,
            [3m, 1m, 0.01m, 0m, 0m, 0m],
            [1.5975m, 0.516525m, 0.0051135975m, 0m, 0m, 0m],
            new decimal[4],
            new decimal[4],
            3,
            1m);
        var line = new CreateIssuedCompanyOrderLineRequest(
            "088449",
            12d,
            UnitPrice: Convert.ToDouble(snapshot.NetUnitPrice),
            PurchaseFinancial: snapshot);
        var request = new CreateIssuedCompanyOrderRequest(
            5,
            "32000391",
            new DateTime(2026, 10, 23),
            new DateTime(2026, 10, 24),
            null,
            null,
            null,
            null,
            [line]);

        var payloadLine = IssuedCompanyOrderMikroApiPayloadFactory.Create(
                request,
                [line],
                0,
                false,
                new DateTime(2026, 10, 23),
                new DateTime(2026, 10, 24),
                "F5",
                1,
                "trace")
            .evraklar.Single().satirlar.Single();

        Assert.Equal(53.25d, payloadLine.sip_b_fiyat);
        Assert.Equal(639d, payloadLine.sip_tutar);
        Assert.Equal(19.17d, payloadLine.sip_iskonto_1);
        Assert.Equal(6.1983d, payloadLine.sip_iskonto_2);
        Assert.Equal(0.06136317d, payloadLine.sip_iskonto_3);
        Assert.Equal(3, payloadLine.sip_vergi_pntr);
        Assert.Equal(6.1357033683d, payloadLine.sip_vergi, 10);
    }
}
