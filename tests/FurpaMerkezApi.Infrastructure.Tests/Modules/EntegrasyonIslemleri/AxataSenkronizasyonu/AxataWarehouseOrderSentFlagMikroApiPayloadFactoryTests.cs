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
            [
                new(firstLineGuid, new DateTime(2026, 9, 7, 10, 11, 12, 345)),
                new(secondLineGuid, new DateTime(2026, 9, 7, 10, 12, 13, 456))
            ],
            "1");

        var lines = payload.evraklar.Single().satirlar.ToArray();

        Assert.Equal(2, lines.Length);
        Assert.Equal("F1D25B23-E4B1-4C8A-B404-933CB2F174C8", lines[0].ssip_Guid);
        Assert.Equal("1", lines[0].ssip_special1);
        Assert.Equal("9A4AD2ED-C4E8-4D4C-A1BD-9B4BB4757D31", lines[1].ssip_Guid);
        Assert.Equal("1", lines[1].ssip_special1);
    }

    [Fact]
    public void CompanyCreate_MapsGuidAndSentFlagToDuzeltPayload()
    {
        var lineGuid = Guid.Parse("82a2356a-9a32-4786-83e3-ab7b901802be");

        var payload = AxataCompanyOrderSentFlagMikroApiPayloadFactory.Create(
            [new(lineGuid, new DateTime(2026, 9, 7, 14, 15, 16, 789))],
            "1");

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal("82A2356A-9A32-4786-83E3-AB7B901802BE", line.sip_Guid);
        Assert.Equal("1", line.sip_special1);
    }
}
