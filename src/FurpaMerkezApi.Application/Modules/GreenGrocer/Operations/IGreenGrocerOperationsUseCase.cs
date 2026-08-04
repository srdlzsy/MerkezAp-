namespace FurpaMerkezApi.Application.Modules.GreenGrocer.Operations;

public interface IGreenGrocerOperationsUseCase
{
    Task<GreenGrocerOperationsOverviewDto> GetOverviewAsync(
        GreenGrocerOperationsOverviewRequest request,
        CancellationToken cancellationToken);

    GreenGrocerOperationsAdjustmentPreviewDto PreviewAdjustment(
        GreenGrocerOperationsAdjustmentPreviewRequest request);

    Task<GreenGrocerOperationsAdjustmentApplyResponse> ApplyAdjustmentAsync(
        GreenGrocerOperationsAdjustmentApplyRequest request,
        CancellationToken cancellationToken);
}
