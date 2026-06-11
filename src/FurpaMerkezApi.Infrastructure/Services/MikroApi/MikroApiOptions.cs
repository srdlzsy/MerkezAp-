namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiOptions
{
    public const string SectionName = "MikroApi";

    public string BaseUrl { get; init; } = string.Empty;

    public string FirmaKodu { get; init; } = string.Empty;

    public int CalismaYili { get; init; }

    public string KullaniciKodu { get; init; } = string.Empty;

    public string SifreAnahtari { get; init; } = string.Empty;

    public int FirmaNo { get; init; }

    public int SubeNo { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public int RetryCount { get; init; } = 2;

    public int RetryDelayMilliseconds { get; init; } = 250;

    public bool RetryUnsafeHttpMethods { get; init; }

    public int MaxLoggedBodyLength { get; init; } = 4096;

    public int HashDateUtcOffsetHours { get; init; } = 3;
}
