namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.Soforler;

public sealed record DespatchDriverListRequest(
    string? Search = null,
    bool IncludeInactive = false,
    int Take = 100);

public sealed record SaveDespatchDriverRequest(
    string FirstName,
    string LastName,
    string PlateNumber,
    string Tckn,
    bool IsActive,
    string? Notes);

public sealed record DespatchDriverDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string PlateNumber,
    string Tckn,
    string MaskedTckn,
    bool IsActive,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public interface IDespatchDriverService
{
    Task<IReadOnlyCollection<DespatchDriverDto>> ListAsync(
        DespatchDriverListRequest request,
        CancellationToken cancellationToken);

    Task<DespatchDriverDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<DespatchDriverDto> CreateAsync(
        SaveDespatchDriverRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken);

    Task<DespatchDriverDto> UpdateAsync(
        Guid id,
        SaveDespatchDriverRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid id,
        Guid changedByUserId,
        CancellationToken cancellationToken);
}
