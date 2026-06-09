namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;

public interface IKasaHareketAktarimiService
{
    Task<IReadOnlyCollection<KasaHareketBranchDto>> ListBranchesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<KasaHareketCashRegisterDto>> ListCashRegistersAsync(
        int branchNo,
        CancellationToken cancellationToken);

    Task<KasaHareketImportResultDto> ImportMovementsAsync(
        KasaHareketImportRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketImportResultDto> ImportCancelMovementsAsync(
        KasaHareketImportRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketImportResultDto> RunScheduledImportAsync(
        KasaHareketScheduledImportRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketProcedureResultDto> DeleteStagingMovementsAsync(
        KasaHareketDeleteStagingRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketProcedureResultDto> TransferMovementsToMikroAsync(
        KasaHareketMikroTransferRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketProcedureResultDto> DeleteMovementsFromMikroAsync(
        KasaHareketMikroTransferRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketProcedureResultDto> TransferMovementRangeToMikroAsync(
        KasaHareketMikroTransferRangeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<KasaHareketReportRowDto>> GetReportAsync(
        KasaHareketReportRequest request,
        CancellationToken cancellationToken);
}
