using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.MalKabulIslemleri.MalKabuller;

public sealed class AcceptWarehouseReceivingUseCaseEDespatchTests
{
    [Fact]
    public void HasSentEDespatch_ReturnsTrue_WhenMovementHasSentEDespatchMetadata()
    {
        var movement = new STOK_HAREKETLERI
        {
            sth_kilitli = true,
            sth_belge_no = "FRM2026000000001",
            sth_aciklama = Guid.NewGuid().ToString()
        };

        Assert.True(AcceptWarehouseReceivingUseCase.HasSentEDespatch(movement));
    }

    [Theory]
    [InlineData(false, "FRM2026000000001", "3e626c74-8c74-4335-a9fc-c91dd315ecdd")]
    [InlineData(true, "", "3e626c74-8c74-4335-a9fc-c91dd315ecdd")]
    [InlineData(true, "AXATA-123", "3e626c74-8c74-4335-a9fc-c91dd315ecdd")]
    [InlineData(true, "FRM2026000000001", "")]
    [InlineData(true, "FRM2026000000001", "not-a-guid")]
    public void HasSentEDespatch_ReturnsFalse_WhenMovementDoesNotHaveSentEDespatchMetadata(
        bool isLocked,
        string documentNo,
        string uuid)
    {
        var movement = new STOK_HAREKETLERI
        {
            sth_kilitli = isLocked,
            sth_belge_no = documentNo,
            sth_aciklama = uuid
        };

        Assert.False(AcceptWarehouseReceivingUseCase.HasSentEDespatch(movement));
    }
}
