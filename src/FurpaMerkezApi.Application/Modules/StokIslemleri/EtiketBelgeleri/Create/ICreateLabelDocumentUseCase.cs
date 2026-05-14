namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Create;

public interface ICreateLabelDocumentUseCase
{
    Task<CreateLabelDocumentResponse> ExecuteAsync(
        CreateLabelDocumentRequest request,
        CancellationToken cancellationToken);
}
