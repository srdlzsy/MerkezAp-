namespace FurpaMerkezApi.Application.Authentication.Contracts;

public static class AuthenticationClientTypes
{
    public const string Web = "web";
    public const string Terminal = "terminal";

    public static bool IsSupported(string? value) =>
        string.Equals(value, Web, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, Terminal, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? value) =>
        string.Equals(value, Terminal, StringComparison.OrdinalIgnoreCase)
            ? Terminal
            : Web;
}
