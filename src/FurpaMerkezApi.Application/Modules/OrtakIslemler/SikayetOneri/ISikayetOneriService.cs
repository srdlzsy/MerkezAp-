namespace FurpaMerkezApi.Application.Modules.OrtakIslemler.SikayetOneri;

public interface ISikayetOneriService
{
    Task<FeedbackItemDto> CreateAsync(
        CreateFeedbackItemRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<FeedbackItemDto>> GetMyItemsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<FeedbackSummaryDto> GetMySummaryAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<FeedbackItemDto>> ListForManagementAsync(
        FeedbackManagementListRequest request,
        CancellationToken cancellationToken);

    Task<FeedbackItemDto> GetForManagementAsync(
        Guid feedbackId,
        FeedbackManagementScope scope,
        CancellationToken cancellationToken);

    Task<FeedbackItemDto> MarkAsReadAsync(
        Guid feedbackId,
        FeedbackManagementActionContext context,
        CancellationToken cancellationToken);

    Task<FeedbackItemDto> ChangeStatusAsync(
        Guid feedbackId,
        ChangeFeedbackStatusRequest request,
        FeedbackManagementActionContext context,
        CancellationToken cancellationToken);
}
