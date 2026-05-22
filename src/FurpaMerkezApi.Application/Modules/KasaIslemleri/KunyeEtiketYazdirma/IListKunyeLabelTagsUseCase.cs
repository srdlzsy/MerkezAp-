namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public interface IListKunyeLabelTagsUseCase
{
    Task<IReadOnlyCollection<KunyeLabelTagDto>> ExecuteAsync(
        KunyeLabelTagListRequest request,
        CancellationToken cancellationToken);
}
