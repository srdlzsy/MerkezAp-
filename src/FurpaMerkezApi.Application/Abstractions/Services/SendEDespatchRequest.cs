namespace FurpaMerkezApi.Application.Abstractions.Services;

public sealed record SendEDespatchRequest(
    EDespatchDocumentType DocumentType,
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    string Plaque,
    string DriverNameSurname,
    string DriverTckn);
