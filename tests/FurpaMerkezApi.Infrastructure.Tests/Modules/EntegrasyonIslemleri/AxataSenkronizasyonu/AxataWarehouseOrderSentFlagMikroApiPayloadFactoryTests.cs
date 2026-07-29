using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataWarehouseOrderSentFlagMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_MapsLineGuidsAndSentFlagToDuzeltPayload()
    {
        var firstLineGuid = Guid.Parse("f1d25b23-e4b1-4c8a-b404-933cb2f174c8");
        var secondLineGuid = Guid.Parse("9a4ad2ed-c4e8-4d4c-a1bd-9b4bb4757d31");

        var payload = AxataWarehouseOrderSentFlagMikroApiPayloadFactory.Create(
            [firstLineGuid, secondLineGuid],
            "1");

        var lines = payload.evraklar.Single().satirlar.ToArray();

        Assert.Equal(2, lines.Length);
        Assert.Equal("F1D25B23-E4B1-4C8A-B404-933CB2F174C8", lines[0].ssip_Guid);
        Assert.Equal("1", lines[0].ssip_special1);
        Assert.Equal("9A4AD2ED-C4E8-4D4C-A1BD-9B4BB4757D31", lines[1].ssip_Guid);
        Assert.Equal("1", lines[1].ssip_special1);
    }
}
