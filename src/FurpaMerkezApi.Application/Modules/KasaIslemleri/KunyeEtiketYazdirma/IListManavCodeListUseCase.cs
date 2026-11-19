namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public interface IListManavCodeListUseCase
{
    Task<IReadOnlyCollection<ManavCodeListItemDto>> ExecuteAsync(
        CancellationToken cancellationToken);
}
