namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.BakeryReport;

public interface IGetBakeryProductionReportUseCase
{
    Task<BakeryProductionReportDto> ExecuteAsync(
        BakeryProductionReportRequest request,
        CancellationToken cancellationToken);
}

public sealed record BakeryProductionReportRequest(
    DateOnly ReportDate,
    int SourceWarehouseNo);

public sealed record BakeryProductionReportDto(
    DateOnly ReportDate,
    int SourceWarehouseNo,
    string SourceWarehouseName,
    IReadOnlyCollection<BakeryProductionReportItemDto> Items);

public sealed record BakeryProductionReportItemDto(
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    string ProductCode,
    string ProductName,
    string TypeCode,
    string TypeName,
    double Quantity);
