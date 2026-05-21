namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Create;

public interface ICreateLabelDocumentUseCase
{
    Task<CreateLabelDocumentResponse> ExecuteAsync(
        CreateLabelDocumentRequest request,
        CancellationToken cancellationToken);
}
