namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.List;

public interface IListVirmansUseCase
{
    Task<IReadOnlyCollection<VirmanListItemDto>> ExecuteAsync(
        VirmanListRequest request,
        CancellationToken cancellationToken);
}
