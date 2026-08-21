using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Common;

public sealed class StockMovementMikroApiPayloadFactoryTests
{
    [Fact]
    public void CreateStockReceipt_MapsCalculatedLineAmountToTutar()
    {
        var request = new CreateStockReceiptRequest(
            110,
            "Teslim Eden",
            "Teslim Alan",
            new DateTime(2026, 8, 21),
            new DateTime(2026, 8, 21),
            "",
            "Zayiat",
            [
                new CreateStockReceiptLineRequest(
                    "008373",
                    0.48d)
            ]);
        var line = new StockReceiptLineWithAmount(request.Lines.Single(), 70.296d);

        var payload = StockMovementMikroApiPayloadFactory.CreateStockReceipt(
            request,
            [line],
            4,
            "",
            new DateTime(2026, 8, 21),
            new DateTime(2026, 8, 21),
            "",
            "F110",
            2449,
            "Teslim Eden",
            "Teslim Alan",
            "Zayiat",
            "");

        var payloadLine = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(70.296d, payloadLine.sth_tutar);
    }
}
