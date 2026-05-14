namespace FurpaMerkezApi.Application.Abstractions.Services;

public sealed record GetEDespatchPdfRequest(
    EDespatchDocumentType DocumentType,
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);
