namespace FurpaMerkezApi.Application.Abstractions.Services;

public sealed record GetEDespatchPdfResponse(
    string FileName,
    byte[] Content);
