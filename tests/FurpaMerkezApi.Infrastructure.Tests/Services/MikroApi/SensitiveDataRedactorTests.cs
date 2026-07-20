using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services.MikroApi;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactJsonValues_ReplacesSensitiveStringValues()
    {
        const string input = """
            {
              "ApiKey": "api-key-value",
              "Token": "token-value",
              "Password": "password-value",
              "Sifre": "sifre-value",
              "SecretKey": "secret-key-value",
              "Authorization": "Bearer auth-value",
              "Result": "visible-value"
            }
            """;

        var result = SensitiveDataRedactor.RedactJsonValues(input, 4096);

        Assert.DoesNotContain("api-key-value", result);
        Assert.DoesNotContain("token-value", result);
        Assert.DoesNotContain("password-value", result);
        Assert.DoesNotContain("sifre-value", result);
        Assert.DoesNotContain("secret-key-value", result);
        Assert.DoesNotContain("Bearer auth-value", result);
        Assert.Contains("\"Result\": \"visible-value\"", result);
        Assert.Equal(6, CountRedactedValues(result));
    }

    [Fact]
    public void RedactJsonValues_ReplacesSensitiveNonStringValues()
    {
        const string input = """
            {
              "ApiKey": 123456,
              "Token": true,
              "access_token": null,
              "Count": 7
            }
            """;

        var result = SensitiveDataRedactor.RedactJsonValues(input, 4096);

        Assert.DoesNotContain("123456", result);
        Assert.DoesNotContain("\"Token\": true", result);
        Assert.DoesNotContain("\"access_token\": null", result);
        Assert.Contains("\"Count\": 7", result);
        Assert.Equal(3, CountRedactedValues(result));
    }

    [Fact]
    public void RedactJsonValues_RedactsBeforeTruncating()
    {
        var secret = new string('x', 600);
        var input = $$"""
            {
              "Token": "{{secret}}",
              "Message": "{{new string('m', 600)}}"
            }
            """;

        var result = SensitiveDataRedactor.RedactJsonValues(input, 300);

        Assert.DoesNotContain(secret, result);
        Assert.Contains("\"Token\":\"[REDACTED]\"", result);
        Assert.EndsWith("...[truncated]", result);
    }

    private static int CountRedactedValues(string value) =>
        value.Split("\"[REDACTED]\"", StringSplitOptions.None).Length - 1;
}
