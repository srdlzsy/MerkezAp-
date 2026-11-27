using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed class ManavMalKabulVeEtiketAcceptanceRequestValidationTests
{
    [Fact]
    public void AcceptanceRecord_AllowsLabelOnlySaveWithoutDocumentIdentity()
    {
        var request = new SaveManavMalKabulVeEtiketAcceptanceRecordHttpRequest
        {
            SupplierCode = "32000001",
            SupplierName = "TEDARIKCI",
            DocumentSeries = null,
            DocumentNo = null,
            StockCode = "001082",
            StockName = "MNV DOMATES",
            StockBarcode = "1234567",
            GrossWeight = 100m,
            CaseTare = 1m,
            CaseCount = 10,
            PalletTare = 0m,
            ReceivedBy = "DEPO PERSONELI",
            CaseType = "REHINLI"
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.True(isValid, string.Join("; ", validationResults.Select(result => result.ErrorMessage)));
        var applicationRequest = request.ToApplicationRequest();
        Assert.Null(applicationRequest.DocumentSeries);
        Assert.Null(applicationRequest.DocumentNo);
    }
}
