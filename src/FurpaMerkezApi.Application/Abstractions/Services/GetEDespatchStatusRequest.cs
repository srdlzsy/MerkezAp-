namespace FurpaMerkezApi.Application.Abstractions.Services;

public sealed record GetEDespatchStatusRequest(
    EDespatchDocumentType DocumentType,
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);
