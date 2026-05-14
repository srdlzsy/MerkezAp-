namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Tags;

public interface IListLabelTagsUseCase
{
    Task<IReadOnlyCollection<LabelTagDto>> ExecuteAsync(
        LabelTagListRequest request,
        CancellationToken cancellationToken);
}
