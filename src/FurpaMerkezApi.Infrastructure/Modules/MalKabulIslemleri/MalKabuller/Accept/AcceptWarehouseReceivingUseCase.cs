using System.Data;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;

public sealed class AcceptWarehouseReceivingUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions)
    : IAcceptWarehouseReceivingUseCase
{
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte DeliveredToTargetWarehouseState = 1;
    private const short FallbackMikroUserNo = 39;
    private const double QuantityTolerance = 0.000001d;

    public async Task<AcceptWarehouseReceivingResponse> ExecuteAsync(
        AcceptWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var options = mikroWriteOptions.Value;
        var documentSerie = request.DocumentSerie.Trim();
        var receivedQuantitiesByMovementGuid = request.Lines.ToDictionary(
            line => line.MovementGuid,
            line => line.ReceivedQuantity);
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var movements = await LoadMovementsAsync(
                    request.WarehouseNo,
                    documentSerie,
                    request.DocumentOrderNo,
                    cancellationToken);
                var lineResults = BuildLineResults(movements, receivedQuantitiesByMovementGuid);
                var hasDiscrepancy = lineResults.Any(line => line.DifferenceType != "none");

                if (hasDiscrepancy && !request.AllowDiscrepancy)
                {
                    throw new InvalidOperationException(
                        "Warehouse receiving contains missing or excess quantities. Send allowDiscrepancy=true to accept with open difference resolution.");
                }

                ApplyAcceptance(request.WarehouseNo, movements, receivedQuantitiesByMovementGuid);

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var firstMovement = movements[0];
                var transitWarehouseNo = firstMovement.sth_nakliyedeposu ?? 0;

                return new AcceptWarehouseReceivingResponse(
                    documentSerie,
                    request.DocumentOrderNo,
                    request.WarehouseNo,
                    firstMovement.sth_cikis_depo_no ?? 0,
                    transitWarehouseNo,
                    DeliveredToTargetWarehouseState,
                    lineResults.Count,
                    lineResults.Sum(line => line.ShippedQuantity),
                    lineResults.Sum(line => line.ReceivedQuantity),
                    lineResults.Where(line => line.DifferenceQuantity < -QuantityTolerance).Sum(line => Math.Abs(line.DifferenceQuantity)),
                    lineResults.Where(line => line.DifferenceQuantity > QuantityTolerance).Sum(line => line.DifferenceQuantity),
                    hasDiscrepancy,
                    hasDiscrepancy ? "requires-manual-resolution" : "not-required",
                    options.ConnectionStringName,
                    lineResults);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<List<STOK_HAREKETLERI>> LoadMovementsAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var movements = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo &&
                (movement.sth_nakliyedeposu == warehouseNo || movement.sth_giris_depo_no == warehouseNo))
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_stok_kod)
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            throw new KeyNotFoundException("Warehouse receiving document was not found.");
        }

        if (movements.All(movement =>
                movement.sth_nakliyedurumu == DeliveredToTargetWarehouseState &&
                movement.sth_giris_depo_no == warehouseNo))
        {
            throw new InvalidOperationException("Warehouse receiving document is already accepted.");
        }

        if (movements.Any(movement =>
                movement.sth_nakliyedeposu != warehouseNo ||
                movement.sth_nakliyedurumu == DeliveredToTargetWarehouseState))
        {
            throw new InvalidOperationException(
                "Warehouse receiving document is not in a pending receiving state for the selected warehouse.");
        }

        if (movements.Any(movement => movement.sth_giris_depo_no is null or <= 0))
        {
            throw new InvalidOperationException("Warehouse receiving document does not contain a valid transit warehouse.");
        }

        return movements;
    }

    private static IReadOnlyCollection<AcceptWarehouseReceivingLineResultDto> BuildLineResults(
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        IReadOnlyDictionary<Guid, double> receivedQuantitiesByMovementGuid)
    {
        var movementGuids = movements
            .Select(movement => movement.sth_Guid)
            .ToHashSet();
        var missingMovementGuid = movementGuids.FirstOrDefault(guid => !receivedQuantitiesByMovementGuid.ContainsKey(guid));

        if (missingMovementGuid != Guid.Empty)
        {
            throw new ArgumentException($"Receiving quantity is missing for movement line: {missingMovementGuid}");
        }

        var extraMovementGuid = receivedQuantitiesByMovementGuid.Keys.FirstOrDefault(guid => !movementGuids.Contains(guid));
        if (extraMovementGuid != Guid.Empty)
        {
            throw new ArgumentException($"Receiving line does not belong to this document: {extraMovementGuid}");
        }

        return movements
            .Select(movement =>
            {
                var shippedQuantity = movement.sth_miktar ?? 0d;
                var receivedQuantity = receivedQuantitiesByMovementGuid[movement.sth_Guid];
                var differenceQuantity = receivedQuantity - shippedQuantity;

                return new AcceptWarehouseReceivingLineResultDto(
                    movement.sth_Guid,
                    movement.sth_satirno ?? 0,
                    movement.sth_stok_kod ?? string.Empty,
                    shippedQuantity,
                    receivedQuantity,
                    differenceQuantity,
                    ResolveDifferenceType(differenceQuantity));
            })
            .ToArray();
    }

    private static void ApplyAcceptance(
        int warehouseNo,
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        IReadOnlyDictionary<Guid, double> receivedQuantitiesByMovementGuid)
    {
        var now = DateTime.Now;
        var updateUser = ResolveMikroUserNo(warehouseNo);

        foreach (var movement in movements)
        {
            var transitWarehouseNo = movement.sth_giris_depo_no!.Value;

            movement.sth_FormulMiktar = receivedQuantitiesByMovementGuid[movement.sth_Guid];
            movement.sth_giris_depo_no = warehouseNo;
            movement.sth_nakliyedeposu = transitWarehouseNo;
            movement.sth_nakliyedurumu = DeliveredToTargetWarehouseState;
            movement.sth_lastup_user = updateUser;
            movement.sth_lastup_date = now;
            movement.sth_degisti = true;
        }
    }

    private static void Validate(AcceptWarehouseReceivingRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one receiving line is required.", nameof(request.Lines));
        }

        var duplicateMovementGuid = request.Lines
            .GroupBy(line => line.MovementGuid)
            .FirstOrDefault(group => group.Key == Guid.Empty || group.Count() > 1)
            ?.Key;

        if (duplicateMovementGuid is not null)
        {
            throw new ArgumentException("Receiving lines must contain unique non-empty movement guid values.", nameof(request.Lines));
        }

        if (request.Lines.Any(line => line.ReceivedQuantity < 0))
        {
            throw new ArgumentException("Received quantity can not be negative.", nameof(request.Lines));
        }
    }

    private static string ResolveDifferenceType(double differenceQuantity)
    {
        if (differenceQuantity < -QuantityTolerance)
        {
            return "missing";
        }

        if (differenceQuantity > QuantityTolerance)
        {
            return "excess";
        }

        return "none";
    }

    private static short ResolveMikroUserNo(int warehouseNo) =>
        warehouseNo is > 0 and <= short.MaxValue
            ? Convert.ToInt16(warehouseNo)
            : FallbackMikroUserNo;
}
