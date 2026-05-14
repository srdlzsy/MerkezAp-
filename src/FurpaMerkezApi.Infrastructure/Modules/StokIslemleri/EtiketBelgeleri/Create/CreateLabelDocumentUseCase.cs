using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.EtiketBelgeleri.Create;

public sealed class CreateLabelDocumentUseCase(LabelDocumentWriteService writeService)
    : ICreateLabelDocumentUseCase
{
    public Task<CreateLabelDocumentResponse> ExecuteAsync(
        CreateLabelDocumentRequest request,
        CancellationToken cancellationToken) =>
        writeService.ExecuteAsync(request, cancellationToken);
}
