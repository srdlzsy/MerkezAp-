namespace FurpaMerkezApi.Application.Abstractions.Services;

public sealed record GetEDespatchStatusResponse(
    EDespatchDocumentType DocumentType,
    string DocumentSerie,
    int DocumentOrderNo,
    bool IsSentToUyumsoft,
    string Status,
    string? EDespatchDocumentNo,
    string? EDespatchUuid,
    DateTime? SentAtUtc,
    bool LocalMikroMetadataUpdated,
    bool LocalMikroMetadataUpdateQueued,
    string? Warning);
