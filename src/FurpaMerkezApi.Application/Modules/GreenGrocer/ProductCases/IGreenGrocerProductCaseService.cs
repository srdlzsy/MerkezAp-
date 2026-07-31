namespace FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;

public interface IGreenGrocerProductCaseService
{
    Task<IReadOnlyCollection<GreenGrocerProductCaseProfileDto>> ListProfilesAsync(
        GreenGrocerProductCaseProfileListRequest request,
        CancellationToken cancellationToken);

    Task<GreenGrocerProductCaseProfileDto> GetProfileAsync(
        string stockCode,
        CancellationToken cancellationToken);

    Task<GreenGrocerProductCaseProfileDto> SaveProfileAsync(
        string stockCode,
        SaveGreenGrocerProductCaseProfileRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken);

    Task DeleteProfileAsync(
        string stockCode,
        Guid changedByUserId,
        CancellationToken cancellationToken);

    Task<GreenGrocerProductCaseResolutionDto> PreviewResolutionAsync(
        GreenGrocerProductCaseResolutionRequest request,
        CancellationToken cancellationToken);
}
