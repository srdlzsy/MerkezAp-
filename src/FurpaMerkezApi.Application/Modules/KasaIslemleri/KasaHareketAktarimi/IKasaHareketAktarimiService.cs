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

    Task<KasaHareketTransferStatusDto> GetTransferStatusAsync(
        KasaHareketTransferStatusRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<KasaHareketReportRowDto>> GetReportAsync(
        KasaHareketReportRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketReportSummaryDto> GetReportSummaryAsync(
        KasaHareketReportRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketSalesComparisonDto> GetSalesComparisonAsync(
        KasaHareketSalesComparisonRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketSatisMutabakatReportDto> GetSatisMutabakatReportAsync(
        KasaHareketSatisMutabakatRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketCashSummaryComparisonDto> GetCashSummaryComparisonAsync(
        KasaHareketCashSummaryComparisonRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketDetailDto> GetDetailAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketDosyaBilgiDto> GetHareketDosyaBilgisiAsync(
        KasaHareketDosyaBilgiRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketDosyaKopyalaResultDto> KopyalaHareketDosyasiAsync(
        KasaHareketDosyaKopyalaRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketDosyaIcerikDto> OkuHareketDosyasiIcerigiAsync(
        KasaHareketDosyaIcerikRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketKasaZDetayReportDto> GetKasaZDetayReportAsync(
        KasaHareketKasaZDetayRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketBirlikKartReportDto> GetBirlikKartReportAsync(
        KasaHareketBirlikKartRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketFisIptalReportDto> GetFisIptalReportAsync(
        KasaHareketFisIptalRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketFisIptalDetailDto> GetFisIptalDetailAsync(
        Guid faturaGuid,
        CancellationToken cancellationToken);

    Task<KasaHareketGiderPusulasiReportDto> GetGiderPusulasiReportAsync(
        KasaHareketGiderPusulasiRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketGiderPusulasiDetailDto> GetGiderPusulasiDetailAsync(
        Guid faturaGuid,
        CancellationToken cancellationToken);

    Task<KasaHareketTrendyolReportDto> GetTrendyolReportAsync(
        KasaHareketTrendyolRequest request,
        CancellationToken cancellationToken);

    Task<KasaHareketNakitReportDto> GetNakitReportAsync(
        KasaHareketNakitRequest request,
        CancellationToken cancellationToken);
}
