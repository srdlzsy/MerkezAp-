namespace FurpaMerkezApi.Application.Abstractions.Services;

public sealed record SendEDespatchResponse(
    EDespatchDocumentType DocumentType,
    string DocumentSerie,
    int DocumentOrderNo,
    string EDespatchDocumentNo,
    string EDespatchUuid,
    string ServiceDocumentId,
    string ServiceDocumentNumber,
    DateTime SentAt,
    string EndpointUrl);
