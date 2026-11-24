namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;

public sealed class BarcodeResolutionTimeoutException(Exception innerException)
    : TimeoutException(
        "Barkod sorgusu zaman asimina ugradi. Baglantiyi kontrol edip tekrar deneyin.",
        innerException);
