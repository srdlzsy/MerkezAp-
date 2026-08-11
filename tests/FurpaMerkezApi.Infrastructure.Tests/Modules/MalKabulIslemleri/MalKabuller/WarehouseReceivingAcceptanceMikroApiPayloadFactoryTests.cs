using System.Text.Json;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.MalKabulIslemleri.MalKabuller;

public sealed class WarehouseReceivingAcceptanceMikroApiPayloadFactoryTests
{
    [Fact]
    public void Create_MapsAcceptanceFieldsWithoutAuditFields()
    {
        var movementGuid = Guid.Parse("a33833b0-f540-4c5b-af7e-9978060a8014");
        var movement = new STOK_HAREKETLERI
        {
            sth_Guid = movementGuid,
            sth_satirno = 0,
            sth_stok_kod = "016955",
            sth_giris_depo_no = 60
        };

        var payload = WarehouseReceivingAcceptanceMikroApiPayloadFactory.Create(
            101,
            [movement],
            new Dictionary<Guid, double>
            {
                [movementGuid] = 3d
            });

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal("A33833B0-F540-4C5B-AF7E-9978060A8014", line.sth_Guid);
        Assert.Equal(3d, line.sth_FormulMiktar);
        Assert.Equal(101, line.sth_giris_depo_no);
        Assert.Equal(60, line.sth_nakliyedeposu);
        Assert.Equal(1, line.sth_nakliyedurumu);

        var json = JsonSerializer.Serialize(payload);

        Assert.DoesNotContain("sth_lastup_user", json);
        Assert.DoesNotContain("sth_lastup_date", json);
        Assert.DoesNotContain("sth_degisti", json);
    }
}
