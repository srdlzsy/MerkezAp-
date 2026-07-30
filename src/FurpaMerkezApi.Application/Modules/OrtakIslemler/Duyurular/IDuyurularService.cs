namespace FurpaMerkezApi.Application.Modules.OrtakIslemler.Duyurular;

public interface IDuyurularService
{
    Task<IReadOnlyCollection<AnnouncementDto>> GetInboxAsync(
        AnnouncementInboxRequest request,
        CancellationToken cancellationToken);

    Task<AnnouncementSummaryDto> GetSummaryAsync(
        Guid userId,
        int warehouseNo,
        CancellationToken cancellationToken);

    Task<AnnouncementDto> MarkAsReadAsync(
        Guid announcementId,
        Guid userId,
        int warehouseNo,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AnnouncementDto>> ListForManagementAsync(
        AnnouncementManagementListRequest request,
        CancellationToken cancellationToken);

    Task<AnnouncementDto> GetForManagementAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken);

    Task<AnnouncementReadReceiptListDto> GetReadReceiptsAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AnnouncementTargetUserDto>> SearchTargetUsersAsync(
        AnnouncementTargetUserSearchRequest request,
        CancellationToken cancellationToken);

    Task<AnnouncementDto> CreateAsync(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken);

    Task<AnnouncementDto> UpdateAsync(
        Guid announcementId,
        UpdateAnnouncementRequest request,
        CancellationToken cancellationToken);

    Task<AnnouncementDto> ArchiveAsync(
        Guid announcementId,
        AnnouncementActorContext actor,
        CancellationToken cancellationToken);
}
