using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationFailureBehaviorTests
{
    [Fact]
    public void CancelledC02Document_DoesNotRequireMikroAnalysis()
    {
        var document = new AxataOutboundDeliveryDocument(
            1912,
            "a.1912",
            "a",
            1912,
            "C02",
            "0",
            "3",
            "IPTAL",
            50,
            110,
            DateTime.Today,
            Array.Empty<AxataOutboundDeliveryLine>());

        Assert.False(AxataOutboundDeliveryImportService.RequiresC02MikroAnalysis(document));
        Assert.True(AxataOutboundDeliveryImportService.IsOutboundDeliveryReadyForProcessing(document));
    }

    [Fact]
    public void HeaderOnlyC02Document_IsNotReadyAndDoesNotBlockRealDeliveries()
    {
        var document = new AxataOutboundDeliveryDocument(
            262270,
            "A.1930",
            "A",
            1930,
            "C02",
            "0",
            string.Empty,
            string.Empty,
            12120063,
            12120063,
            DateTime.Today,
            Array.Empty<AxataOutboundDeliveryLine>());

        Assert.False(AxataOutboundDeliveryImportService.IsOutboundDeliveryReadyForProcessing(document));
    }

    [Fact]
    public void C02DocumentWithPositiveLine_IsReadyForProcessing()
    {
        var document = new AxataOutboundDeliveryDocument(
            262280,
            "A.1936",
            "A",
            1936,
            "C02",
            "0",
            string.Empty,
            string.Empty,
            12120063,
            12120063,
            DateTime.Today,
            [new AxataOutboundDeliveryLine(0, "007921", 757d)]);

        Assert.True(AxataOutboundDeliveryImportService.IsOutboundDeliveryReadyForProcessing(document));
    }

    [Fact]
    public void ProductFailureMessage_ContainsProductAndServiceReason()
    {
        var results = new[]
        {
            new AxataProductSynchronizationResultDto(
                "015681",
                false,
                12,
                "Barkod bilgisi gecersiz.",
                1,
                1)
        };

        var message = ProductMasterSyncTaskHandler.BuildProductFailureMessage(1, 10, results);

        Assert.Contains("Urun=015681", message);
        Assert.Contains("State=12", message);
        Assert.Contains("Barkod bilgisi gecersiz.", message);
    }

    [Fact]
    public void FormatExceptionMessage_IncludesInnerDatabaseReason()
    {
        var exception = new InvalidOperationException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("The INSERT statement conflicted with a constraint."));

        var message = AxataImportFailureClassifier.FormatExceptionMessage(exception);

        Assert.Contains("An error occurred while saving the entity changes.", message);
        Assert.Contains("The INSERT statement conflicted with a constraint.", message);
    }
}
