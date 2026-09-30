namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record DeleteWarehouseShippingDocumentResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int DeletedLineCount,
    DateTime DeletedAt,
    short UpdateUser,
    string WriteConnectionName);
