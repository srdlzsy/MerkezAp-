using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class InterWarehouseShipmentMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_MapsWarehouseOrderLineGuidToSubeSipUid()
    {
        var warehouseOrderLineGuid = Guid.Parse("f1d25b23-e4b1-4c8a-b404-933cb2f174c8");
        var request = CreateRequest(warehouseOrderLineGuid);

        var payload = InterWarehouseShipmentMikroApiPayloadFactory.Create(
            request,
            request.Lines,
            new DateTime(2026, 7, 29),
            new DateTime(2026, 7, 29),
            "",
            "F1",
            8,
            "test");

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(warehouseOrderLineGuid, line.sth_subesip_uid);
    }

    [Fact]
    public void Create_LeavesSubeSipUidEmptyWhenLineIsNotLinked()
    {
        var request = CreateRequest(null);

        var payload = InterWarehouseShipmentMikroApiPayloadFactory.Create(
            request,
            request.Lines,
            new DateTime(2026, 7, 29),
            new DateTime(2026, 7, 29),
            "",
            "F1",
            8,
            "test");

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Null(line.sth_subesip_uid);
    }

    private static CreateInterWarehouseShipmentRequest CreateRequest(Guid? warehouseOrderLineGuid) =>
        new(
            1,
            4,
            60,
            new DateTime(2026, 7, 29),
            new DateTime(2026, 7, 29),
            "",
            "test",
            [
                new CreateInterWarehouseShipmentLineRequest(
                    "01",
                    10d,
                    warehouseOrderLineGuid,
                    500d,
                    4)
            ]);
}
