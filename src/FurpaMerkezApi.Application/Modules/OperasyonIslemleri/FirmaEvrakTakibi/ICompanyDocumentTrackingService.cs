namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.FirmaEvrakTakibi;

public interface ICompanyDocumentTrackingService
{
    Task<CompanyDocumentTrackingDto> GetAsync(
        CompanyDocumentTrackingRequest request,
        CancellationToken cancellationToken);
}

public sealed record CompanyDocumentTrackingRequest(
    DateOnly Date,
    int? WarehouseNo);

public sealed record CompanyDocumentTrackingDto(
    DateOnly Date,
    DateTime GeneratedAtUtc,
    int? WarehouseNo,
    int DocumentCount,
    int CompanyReceivingCount,
    int CompanyReturnCount,
    IReadOnlyCollection<CompanyDocumentTrackingItemDto> Items);

public sealed record CompanyDocumentTrackingItemDto(
    string DocumentKind,
    string DocumentKindName,
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    string CustomerCode,
    string CustomerName,
    string CustomerTitle,
    string CustomerDisplayName,
    DateTime? DocumentDate,
    DateTime MovementCreateDate,
    string Deliverer,
    string Receiver,
    int WarehouseNo,
    string WarehouseName,
    int LineCount,
    double TotalQuantity);
