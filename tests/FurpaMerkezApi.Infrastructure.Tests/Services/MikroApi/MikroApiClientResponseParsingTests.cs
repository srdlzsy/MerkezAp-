using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services.MikroApi;

public sealed class MikroApiClientResponseParsingTests
{
    [Fact]
    public void ParseResponseInfo_ReadsNestedResultError()
    {
        const string response = """
            {
              "result": [
                {
                  "StatusCode": 400,
                  "Data": null,
                  "ErrorMessage": "Degisiklik yaptiginiz surede kayit degistiginden kayit kabul edilmedi.",
                  "IsError": true
                }
              ]
            }
            """;

        var result = MikroApiClient.ParseResponseInfo(response);

        Assert.True(result.IsError);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(
            "Degisiklik yaptiginiz surede kayit degistiginden kayit kabul edilmedi.",
            result.ErrorMessage);
    }

    [Fact]
    public void ParseResponseInfo_ReadsNestedResultSuccess()
    {
        const string response = """
            {
              "result": [
                {
                  "StatusCode": 200,
                  "Data": {
                    "list": [
                      {
                        "cariHarGuid": "9A3733B0-F540-4C5B-AF7E-9978060A8014",
                        "evrakno_seri": "F101",
                        "evrakno_sira": "5796"
                      }
                    ]
                  },
                  "ErrorMessage": "",
                  "IsError": false
                }
              ]
            }
            """;

        var result = MikroApiClient.ParseResponseInfo(response);

        Assert.False(result.IsError);
        Assert.Equal(200, result.StatusCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ParseResponseInfo_TreatsNestedStatusCodeAsErrorEvenWithoutIsError()
    {
        const string response = """
            {
              "result": [
                {
                  "StatusCode": "400",
                  "ErrorMessage": "Stok Hareketi Kaydedilemedi."
                }
              ]
            }
            """;

        var result = MikroApiClient.ParseResponseInfo(response);

        Assert.True(result.IsError);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Stok Hareketi Kaydedilemedi.", result.ErrorMessage);
    }

    [Fact]
    public void ParseResponseInfo_TreatsLowercaseSuccessFalseAsError()
    {
        const string response = """
            {
              "result": [
                {
                  "data": null,
                  "success": false,
                  "errorText": "MikroAPI - TimeOut"
                }
              ]
            }
            """;

        var result = MikroApiClient.ParseResponseInfo(response);

        Assert.True(result.IsError);
        Assert.Null(result.StatusCode);
        Assert.Equal("MikroAPI - TimeOut", result.ErrorMessage);
    }

    [Fact]
    public void ParseResponseInfo_TreatsAnyFailedBulkItemAsError()
    {
        const string response = """
            {
              "result": [
                { "data": { "guid": "first" }, "success": true, "errorText": "" },
                { "data": null, "success": false, "errorText": "Ikinci satir kaydedilemedi." }
              ]
            }
            """;

        var result = MikroApiClient.ParseResponseInfo(response);

        Assert.True(result.IsError);
        Assert.Equal("Ikinci satir kaydedilemedi.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("MikroAPI - TimeOut")]
    [InlineData("The operation timed out.")]
    public void IsUnknownWriteOutcome_TreatsTimeoutErrorsAsAmbiguous(string errorMessage)
    {
        var result = new MikroApiResult<string>(
            true,
            200,
            System.Net.HttpStatusCode.OK,
            errorMessage,
            $"{{\"errorText\":\"{errorMessage}\"}}",
            null,
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            1,
            TimeSpan.FromSeconds(130));

        Assert.True(MikroApiWriteAuditService.IsUnknownWriteOutcome(result));
    }

    [Fact]
    public void IsUnknownWriteOutcome_LeavesBusinessErrorAsFailed()
    {
        var result = new MikroApiResult<string>(
            true,
            400,
            System.Net.HttpStatusCode.OK,
            "Gider muhasebe kodu bos.",
            "{}",
            null,
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            1,
            TimeSpan.FromSeconds(1));

        Assert.False(MikroApiWriteAuditService.IsUnknownWriteOutcome(result));
    }

    [Theory]
    [InlineData("F56-88038 numarali evrak zaten mevcut.")]
    [InlineData("Duplicate document number.")]
    public void IsRecoverableCreateOutcome_AllowsVerifiedReadbackForDuplicateDocumentErrors(
        string errorMessage)
    {
        var result = new MikroApiResult<string>(
            true,
            400,
            System.Net.HttpStatusCode.OK,
            errorMessage,
            $"{{\"errorText\":\"{errorMessage}\"}}",
            null,
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            1,
            TimeSpan.FromSeconds(1));

        Assert.True(MikroApiWriteAuditService.IsRecoverableCreateOutcome(result));
        Assert.False(MikroApiWriteAuditService.IsUnknownWriteOutcome(result));
    }

    [Fact]
    public void IsRecoverableCreateOutcome_DoesNotRetryBusinessRuleFailure()
    {
        var result = new MikroApiResult<string>(
            true,
            400,
            System.Net.HttpStatusCode.OK,
            "Gider muhasebe kodu bos. U17",
            "{}",
            null,
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            1,
            TimeSpan.FromSeconds(1));

        Assert.False(MikroApiWriteAuditService.IsRecoverableCreateOutcome(result));
    }

    [Fact]
    public void CreateConflict_ClassifiesDuplicateContentMismatchAsManualReview()
    {
        var result = new MikroApiResult<string>(
            true,
            400,
            System.Net.HttpStatusCode.OK,
            "Evrak zaten mevcut.",
            "{}",
            null,
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            1,
            TimeSpan.FromSeconds(1));

        var exception = MikroApiCreateConflictFactory.Create(
            result,
            new InvalidOperationException("Existing rows did not match."));

        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, exception.ErrorCode);
        Assert.False(exception.Retryable);
    }

    [Fact]
    public void CreateConflict_ClassifiesTimeoutAsRetryableUnconfirmedOutcome()
    {
        var result = new MikroApiResult<string>(
            true,
            200,
            System.Net.HttpStatusCode.OK,
            "MikroAPI - TimeOut",
            "{}",
            null,
            "/Api/apiMethods/DahiliStokHareketKaydetV2",
            1,
            TimeSpan.FromSeconds(130));

        var exception = MikroApiCreateConflictFactory.Create(
            result,
            new InvalidOperationException("Rows are not visible yet."));

        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, exception.ErrorCode);
        Assert.True(exception.Retryable);
    }

    [Fact]
    public void CreatedDocumentResultReader_ReadsNestedDataListRows()
    {
        const string response = """
            {
              "result": [
                {
                  "StatusCode": 200,
                  "Data": {
                    "list": [
                      {
                        "cariHarGuid": "9A3733B0-F540-4C5B-AF7E-9978060A8014",
                        "evrakno_seri": "F101",
                        "evrakno_sira": "5796"
                      },
                      {
                        "cariHarGuid": "9F3733B0-F540-4C5B-AF7E-9978060A8014",
                        "evrakno_seri": "F101",
                        "evrakno_sira": 5796
                      }
                    ]
                  },
                  "ErrorMessage": "",
                  "IsError": false
                }
              ]
            }
            """;

        var rows = MikroApiCreatedDocumentResultReader.ReadRows(response);

        Assert.Equal(2, rows.Count);
        Assert.Equal(Guid.Parse("9A3733B0-F540-4C5B-AF7E-9978060A8014"), rows[0].Guid);
        Assert.Equal("F101", rows[0].DocumentSerie);
        Assert.Equal(5796, rows[0].DocumentOrderNo);
        Assert.Equal(Guid.Parse("9F3733B0-F540-4C5B-AF7E-9978060A8014"), rows[1].Guid);
        Assert.Equal("F101", rows[1].DocumentSerie);
        Assert.Equal(5796, rows[1].DocumentOrderNo);
    }
}
