using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.Virmanlar;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanConversionSuggestionRequestValidationTests
{
    [Fact]
    public void SourceQuantity_ValidatesWithTurkishCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            var request = new VirmanConversionSuggestionHttpRequest
            {
                SourceStockCode = "034484",
                SourceQuantity = 1d
            };
            var validationResults = new List<ValidationResult>();

            var isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true);

            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
