using PdfSharp.Fonts;

namespace FurpaMerkezApi.WebApi.Services;

public static class PdfSharpConfiguration
{
    static PdfSharpConfiguration()
    {
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    public static void EnsureInitialized()
    {
    }
}
