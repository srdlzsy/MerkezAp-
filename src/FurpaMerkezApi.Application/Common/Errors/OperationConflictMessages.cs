namespace FurpaMerkezApi.Application.Common.Errors;

public static class OperationConflictMessages
{
    public const string MikroWriteInProgress =
        "Kayit islemi halen devam ediyor. Formu degistirmeden kisa bir sure sonra tekrar deneyin; yeni bir kayit istegi olusturmayin.";

    public const string MikroWriteQueueBusy =
        "Mikro yazma kuyrugu su anda mesgul. Bu istegin yazimi henuz baslamadi. Ayni kaydi kisa bir sure sonra tekrar deneyin.";

    public const string MikroWriteOutcomeUnconfirmed =
        "Kaydin Mikro'ya yazilip yazilmadigi henuz kesinlestirilemedi. Yeni bir kayit olusturmayin; mevcut islem icin durum kontrolu yapin veya guvenli tekrar deneyin.";

    public const string MikroDocumentContentMismatch =
        "Mikro'daki belge ile gonderilen kaydin satirlari eslesmiyor. Tekrar gondermeyin; yetkili incelemesi gerekiyor.";

    public const string ClientRequestPayloadMismatch =
        "Ayni istek kimligi farkli bir kayit icerigiyle kullanilmis. Bekleyen kaydi degistirmeyin; yalniz gercekten yeni bir islem baslatacaksaniz yeni kayit olusturun.";

    public static string? Resolve(string? errorCode, string? fallbackMessage = null) =>
        errorCode switch
        {
            OperationConflictErrorCodes.MikroWriteInProgress => MikroWriteInProgress,
            OperationConflictErrorCodes.MikroWriteQueueBusy => MikroWriteQueueBusy,
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed => MikroWriteOutcomeUnconfirmed,
            OperationConflictErrorCodes.MikroDocumentContentMismatch => MikroDocumentContentMismatch,
            OperationConflictErrorCodes.ClientRequestPayloadMismatch => ClientRequestPayloadMismatch,
            _ => fallbackMessage
        };
}
