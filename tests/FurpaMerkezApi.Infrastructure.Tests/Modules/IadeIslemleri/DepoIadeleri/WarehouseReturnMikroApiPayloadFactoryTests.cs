using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.IadeIslemleri.DepoIadeleri;

public sealed class WarehouseReturnMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_MapsWarehouseOrderLineGuidToSubeSipUid()
    {
        var warehouseOrderLineGuid = Guid.Parse("f1d25b23-e4b1-4c8a-b404-933cb2f174c8");
        var request = CreateRequest();

        var payload = WarehouseReturnMikroApiPayloadFactory.Create(
            request,
            request.Lines,
            new DateTime(2026, 7, 29),
            new DateTime(2026, 7, 29),
            "",
            "F1",
            8,
            "test",
            new Dictionary<int, Guid> { [0] = warehouseOrderLineGuid });

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(warehouseOrderLineGuid, line.sth_subesip_uid);
    }

    [Fact]
    public void Create_LeavesSubeSipUidEmptyWhenLineIsNotLinked()
    {
        var request = CreateRequest();

        var payload = WarehouseReturnMikroApiPayloadFactory.Create(
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

    private static CreateWarehouseReturnRequest CreateRequest() =>
        new(
            1,
            4,
            60,
            new DateTime(2026, 7, 29),
            new DateTime(2026, 7, 29),
            "",
            "test",
            [
                new CreateWarehouseReturnLineRequest(
                    "01",
                    10d,
                    500d,
                    4)
            ]);
}
