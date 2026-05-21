namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Tags;

public interface IListLabelTagsUseCase
{
    Task<IReadOnlyCollection<LabelTagDto>> ExecuteAsync(
        LabelTagListRequest request,
        CancellationToken cancellationToken);
}
