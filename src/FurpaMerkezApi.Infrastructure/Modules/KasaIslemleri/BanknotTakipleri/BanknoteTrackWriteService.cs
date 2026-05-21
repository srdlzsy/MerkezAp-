using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri;

public sealed class BanknoteTrackWriteService(MikroWriteDbContext mikroWriteDbContext)
{
    public async Task<CreateBanknoteTrackResponse> CreateAsync(
        CreateBanknoteTrackRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var trackDate = request.BanknoteTrackDate.Date;
        var existing = await mikroWriteDbContext.BanknoteTracks
            .FirstOrDefaultAsync(item =>
                item.WarehouseNo == request.WarehouseNo &&
                item.BanknoteTrackDate >= trackDate &&
                item.BanknoteTrackDate < trackDate.AddDays(1),
                cancellationToken);

        if (existing is not null)
        {
            return new CreateBanknoteTrackResponse(
                existing.Id,
                existing.BanknoteTrackDate,
                existing.WarehouseNo,
                false);
        }

        var entity = new BanknoteTrackEntity
        {
            Id = Guid.NewGuid(),
            WarehouseNo = request.WarehouseNo,
            BanknoteTrackDate = trackDate,
            TotalAmount = request.TotalAmount,
            DeliveryTotalAmount = request.DeliveryTotalAmount,
            Deliverer = NormalizeText(request.Deliverer),
            Receiver = NormalizeText(request.Receiver),
            CreateDate = DateTime.Now
        };

        await mikroWriteDbContext.BanknoteTracks.AddAsync(entity, cancellationToken);
        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

        return new CreateBanknoteTrackResponse(
            entity.Id,
            entity.BanknoteTrackDate,
            entity.WarehouseNo,
            true);
    }

    private static void Validate(CreateBanknoteTrackRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.BanknoteTrackDate == default)
        {
            throw new ArgumentException("Banknote track date is required.", nameof(request.BanknoteTrackDate));
        }

        if (request.TotalAmount < 0 || request.DeliveryTotalAmount < 0)
        {
            throw new ArgumentException("Amounts can not be negative.");
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
