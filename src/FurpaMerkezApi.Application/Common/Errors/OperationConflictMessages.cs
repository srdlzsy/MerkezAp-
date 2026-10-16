namespace FurpaMerkezApi.Application.Common.Errors;

public static class OperationConflictMessages
{
    public const string MikroWriteInProgress =
        "Kayit islemi halen devam ediyor. Yalniz bu taslagin Kaydet butonunu bekletin; ekrani veya Yeni Ekle islemini genel olarak kilitlemeyin. Formu degistirmeden ayni istek kimligiyle durum kontrolu yapin.";

    public const string MikroWriteQueueBusy =
        "Mikro yazma kuyrugu su anda mesgul. Bu istegin yazimi henuz baslamadi. Yalniz bu taslagi bekletin ve ayni istek kimligiyle kisa bir sure sonra tekrar deneyin.";

    public const string MikroWriteOutcomeUnconfirmed =
        "Kaydin Mikro'ya yazilip yazilmadigi henuz kesinlestirilemedi. Bu taslak icin yeni kayit olusturmayin; ayni istek kimligiyle durum kontrolu yapin. Diger ekranlar ve yeni islemler kullanilmaya devam edebilir.";

    public const string MikroDocumentContentMismatch =
        "Mikro'daki belge ile gonderilen kaydin satirlari eslesmiyor. Yalniz bu kaydi tekrar gondermeyin ve yetkili incelemesine alin; modulun diger islemlerini kilitlemeyin.";

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
