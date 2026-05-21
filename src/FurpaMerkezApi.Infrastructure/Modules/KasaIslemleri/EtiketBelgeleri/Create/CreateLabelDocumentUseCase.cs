using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Create;

public sealed class CreateLabelDocumentUseCase(LabelDocumentWriteService writeService)
    : ICreateLabelDocumentUseCase
{
    public Task<CreateLabelDocumentResponse> ExecuteAsync(
        CreateLabelDocumentRequest request,
        CancellationToken cancellationToken) =>
        writeService.ExecuteAsync(request, cancellationToken);
}
