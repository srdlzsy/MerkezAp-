namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCiroAktarimi;

public interface IKasaCiroAktarimiService
{
    Task<IReadOnlyCollection<KasaCiroBranchDto>> ListBranchesAsync(CancellationToken cancellationToken);

    Task<KasaCiroImportResultDto> ImportTextMovementsAsync(
        KasaCiroImportRequest request,
        CancellationToken cancellationToken);
}
