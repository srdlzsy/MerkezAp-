using System.Text.RegularExpressions;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal static partial class SensitiveDataRedactor
{
    public static string RedactJsonValues(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = SensitiveJsonPropertyRegex().Replace(
            value,
            match => $"\"{match.Groups["name"].Value}\":\"[REDACTED]\"");
        var safeMaxLength = Math.Clamp(maxLength, 256, 32768);

        return redacted.Length <= safeMaxLength
            ? redacted
            : string.Concat(redacted.AsSpan(0, safeMaxLength), "...[truncated]");
    }

    [GeneratedRegex(
        "\"(?<name>Authorization|ApiKey|api_key|AccessToken|access_token|RefreshToken|refresh_token|Password|Parola|Sifre|SifreAnahtari|Secret|SecretKey|Token)\"\\s*:\\s*(\"(?:\\\\.|[^\"])*\"|[^,}\\]\\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveJsonPropertyRegex();
}
