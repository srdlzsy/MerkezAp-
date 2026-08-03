using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Soforler;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Soforler;

public sealed class DespatchDriverService(
    AuthDbContext dbContext,
    IClock clock)
    : IDespatchDriverService
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;

    public async Task<IReadOnlyCollection<DespatchDriverDto>> ListAsync(
        DespatchDriverListRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var search = NormalizeSearch(request.Search);
        var query = dbContext.DespatchDrivers.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(driver => driver.IsActive);
        }

        if (search is not null)
        {
            query = query.Where(driver =>
                driver.FirstName.Contains(search) ||
                driver.LastName.Contains(search) ||
                driver.PlateNumber.Contains(search) ||
                driver.Tckn.Contains(search));
        }

        var drivers = await query
            .OrderByDescending(driver => driver.IsActive)
            .ThenBy(driver => driver.LastName)
            .ThenBy(driver => driver.FirstName)
            .ThenBy(driver => driver.PlateNumber)
            .Take(take)
            .ToListAsync(cancellationToken);

        return drivers.Select(Map).ToArray();
    }

    public async Task<DespatchDriverDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.DespatchDrivers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despatch driver was not found.");

        return Map(driver);
    }

    public async Task<DespatchDriverDto> CreateAsync(
        SaveDespatchDriverRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await EnsureNoActiveDuplicateAsync(request, null, cancellationToken);

        var driver = new DespatchDriver(
            Guid.NewGuid(),
            request.FirstName,
            request.LastName,
            request.PlateNumber,
            request.Tckn,
            changedByUserId,
            clock.UtcNow,
            request.IsActive,
            request.Notes);

        await dbContext.DespatchDrivers.AddAsync(driver, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(driver);
    }

    public async Task<DespatchDriverDto> UpdateAsync(
        Guid id,
        SaveDespatchDriverRequest request,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        await EnsureNoActiveDuplicateAsync(request, id, cancellationToken);

        var driver = await dbContext.DespatchDrivers
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despatch driver was not found.");

        driver.Update(
            request.FirstName,
            request.LastName,
            request.PlateNumber,
            request.Tckn,
            request.IsActive,
            request.Notes,
            changedByUserId,
            clock.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(driver);
    }

    public async Task DeleteAsync(
        Guid id,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var driver = await dbContext.DespatchDrivers
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Despatch driver was not found.");

        driver.Deactivate(changedByUserId, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNoActiveDuplicateAsync(
        SaveDespatchDriverRequest request,
        Guid? currentDriverId,
        CancellationToken cancellationToken)
    {
        if (!request.IsActive)
        {
            return;
        }

        var normalizedPlate = NormalizePlate(request.PlateNumber);
        var normalizedTckn = NormalizeTckn(request.Tckn);

        var duplicateExists = await dbContext.DespatchDrivers
            .AsNoTracking()
            .AnyAsync(
                driver =>
                    driver.IsActive &&
                    driver.Id != currentDriverId &&
                    driver.PlateNumber == normalizedPlate &&
                    driver.Tckn == normalizedTckn,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("An active despatch driver with the same plate and TCKN already exists.");
        }
    }

    private static DespatchDriverDto Map(DespatchDriver driver)
    {
        var fullName = $"{driver.FirstName} {driver.LastName}".Trim();

        return new DespatchDriverDto(
            driver.Id,
            driver.FirstName,
            driver.LastName,
            fullName,
            driver.PlateNumber,
            driver.Tckn,
            MaskTckn(driver.Tckn),
            driver.IsActive,
            driver.Notes,
            driver.CreatedAtUtc,
            driver.UpdatedAtUtc);
    }

    private static int NormalizeTake(int take)
    {
        if (take <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(take, MaxTake);
    }

    private static string? NormalizeSearch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePlate(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string NormalizeTckn(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string MaskTckn(string value) =>
        value.Length == 11
            ? string.Concat(value.AsSpan(0, 3), "*****", value.AsSpan(8, 3))
            : value;
}
