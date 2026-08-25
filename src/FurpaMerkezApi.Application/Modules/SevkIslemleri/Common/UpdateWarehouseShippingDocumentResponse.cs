namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

public sealed record UpdateWarehouseShippingDocumentResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    int TransitWarehouseNo,
    bool IsReturn,
    int UpdatedLineCount,
    int AddedLineCount,
    int DeletedLineCount,
    int LineCount,
    double TotalQuantity,
    double TotalAmount,
    DateTime UpdatedAt,
    short UpdateUser,
    string WriteConnectionName);
