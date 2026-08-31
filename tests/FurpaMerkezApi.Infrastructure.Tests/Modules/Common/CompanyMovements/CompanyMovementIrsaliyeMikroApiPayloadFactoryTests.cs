using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common.CompanyMovements;

public sealed class CompanyMovementIrsaliyeMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_MapsOrderLineGuidToSipUid()
    {
        var orderLineGuid = Guid.Parse("0f4db720-3374-4f80-ae21-6f7d2edec8b1");
        var payload = CreatePayload(orderLineGuid);

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(orderLineGuid.ToString(), line.sth_sip_uid);
    }

    [Fact]
    public void Create_LeavesSipUidEmptyWhenLineIsNotLinked()
    {
        var payload = CreatePayload(null);

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(string.Empty, line.sth_sip_uid);
    }

    [Fact]
    public void Create_MapsDelivererAndReceiverToMovementGroupCodes()
    {
        var payload = CreatePayload(null, "Teslim Eden", "Teslim Alan");

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal("Teslim Eden", line.sth_HareketGrupKodu2);
        Assert.Equal("Teslim Alan", line.sth_HareketGrupKodu3);
    }

    [Fact]
    public void Create_MapsMovementGenre()
    {
        var payload = CreatePayload(null, movementGenre: 1);

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(1, line.sth_cins);
    }

    private static CompanyMovementIrsaliyeMikroApiPayload CreatePayload(
        Guid? orderLineGuid,
        string? deliverer = null,
        string? receiver = null,
        byte movementGenre = 0)
    {
        var request = new CreateCompanyMovementRequest(
            50,
            "32000001",
            new DateTime(2026, 8, 5),
            new DateTime(2026, 8, 5),
            "C02.1",
            "test",
            [
                new CreateCompanyMovementLineRequest(
                    "001",
                    10d,
                    2d,
                    1,
                    OrderLineGuid: orderLineGuid)
            ],
            deliverer,
            receiver);

        return CompanyMovementIrsaliyeMikroApiPayloadFactory.Create(
            request,
            request.Lines,
            "32000001",
            1,
            movementGenre,
            0,
            new DateTime(2026, 8, 5),
            new DateTime(2026, 8, 5),
            "C02.1",
            "F50",
            1,
            "test");
    }
}
