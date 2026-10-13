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

    Task<global::FurpaMerkezApi.Application.Modules.Common.OfflineSync.OfflineSyncStatusDto<GreenGrocerOperationsAdjustmentApplyResponse>> GetAdjustmentOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
