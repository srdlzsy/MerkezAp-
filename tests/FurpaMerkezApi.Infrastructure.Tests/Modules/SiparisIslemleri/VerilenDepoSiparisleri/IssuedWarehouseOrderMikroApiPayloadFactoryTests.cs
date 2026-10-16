using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.VerilenDepoSiparisleri;

public sealed class IssuedWarehouseOrderMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_WritesOfflineTracePartsToEveryLine()
    {
        var request = new CreateIssuedWarehouseOrderRequest(
            119,
            53,
            new DateTime(2026, 10, 15),
            new DateTime(2026, 10, 15),
            null,
            [new CreateIssuedWarehouseOrderLineRequest("000001", 2)],
            Guid.NewGuid(),
            Guid.NewGuid());

        var payload = IssuedWarehouseOrderMikroApiPayloadFactory.Create(
            request,
            request.Lines,
            request.OrderDate!.Value,
            request.DeliveryDate!.Value,
            "F119",
            3971,
            "FR12",
            "3456",
            "7890");

        var line = Assert.Single(Assert.Single(payload.evraklar).satirlar);
        Assert.Equal("FR12", line.ssip_special1);
        Assert.Equal("3456", line.ssip_special2);
        Assert.Equal("7890", line.ssip_special3);
    }
}
