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
