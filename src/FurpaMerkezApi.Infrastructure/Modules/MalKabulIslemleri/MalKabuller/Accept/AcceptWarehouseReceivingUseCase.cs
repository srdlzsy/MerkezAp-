using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;

public sealed class AcceptWarehouseReceivingUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<AcceptWarehouseReceivingUseCase> logger)
    : IAcceptWarehouseReceivingUseCase
{
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte ReturnMovement = 1;
    private const byte DeliveredToTargetWarehouseState = 1;
    private const short FallbackMikroUserNo = 39;
    private const double QuantityTolerance = 0.000001d;
    private const string DahiliStokHareketDuzeltPath = "/Api/apiMethods/DahiliStokHareketDuzeltV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;

    public async Task<AcceptWarehouseReceivingResponse> ExecuteAsync(
        AcceptWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.WarehouseReceivingAcceptance switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => await ExecuteDualShadowAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:WarehouseReceivingAcceptance mode '{mode}'.")
        };
    }

    private async Task<AcceptWarehouseReceivingResponse> ExecuteDatabaseAsync(
        AcceptWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
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
                    firstMovement.sth_normal_iade == ReturnMovement,
                    lineResults.Count,
                    lineResults.Sum(line => line.ShippedQuantity),
                    lineResults.Sum(line => line.ReceivedQuantity),
                    lineResults.Where(line => line.DifferenceQuantity < -QuantityTolerance).Sum(line => Math.Abs(line.DifferenceQuantity)),
                    lineResults.Where(line => line.DifferenceQuantity > QuantityTolerance).Sum(line => line.DifferenceQuantity),
                    hasDiscrepancy,
                    hasDiscrepancy ? "recorded-on-formula-quantity" : "not-required",
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

    private async Task<AcceptWarehouseReceivingResponse> ExecuteMikroApiAsync(
        AcceptWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var documentSerie = request.DocumentSerie.Trim();
        var receivedQuantitiesByMovementGuid = request.Lines.ToDictionary(
            line => line.MovementGuid,
            line => line.ReceivedQuantity);

        mikroWriteDbContext.ChangeTracker.Clear();

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

        var firstMovement = movements[0];
        var sourceWarehouseNo = firstMovement.sth_cikis_depo_no ?? 0;
        var transitWarehouseNo = firstMovement.sth_giris_depo_no ?? 0;
        var isReturn = firstMovement.sth_normal_iade == ReturnMovement;
        var payload = WarehouseReceivingAcceptanceMikroApiPayloadFactory.Create(
            request.WarehouseNo,
            movements,
            receivedQuantitiesByMovementGuid);

        logger.LogInformation(
            "Warehouse receiving acceptance is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, LineCount={LineCount}",
            DahiliStokHareketDuzeltPath,
            documentSerie,
            request.DocumentOrderNo,
            request.WarehouseNo,
            movements.Count);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            DahiliStokHareketDuzeltPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API warehouse receiving acceptance failed.");
        }

        mikroWriteDbContext.ChangeTracker.Clear();

        await RecoverMikroApiAcceptanceAsync(
            request,
            receivedQuantitiesByMovementGuid,
            transitWarehouseNo,
            cancellationToken);

        return new AcceptWarehouseReceivingResponse(
            documentSerie,
            request.DocumentOrderNo,
            request.WarehouseNo,
            sourceWarehouseNo,
            transitWarehouseNo,
            DeliveredToTargetWarehouseState,
            isReturn,
            lineResults.Count,
            lineResults.Sum(line => line.ShippedQuantity),
            lineResults.Sum(line => line.ReceivedQuantity),
            lineResults.Where(line => line.DifferenceQuantity < -QuantityTolerance).Sum(line => Math.Abs(line.DifferenceQuantity)),
            lineResults.Where(line => line.DifferenceQuantity > QuantityTolerance).Sum(line => line.DifferenceQuantity),
            hasDiscrepancy,
            hasDiscrepancy ? "recorded-on-formula-quantity" : "not-required",
            options.ConnectionStringName,
            lineResults);
    }

    private async Task<AcceptWarehouseReceivingResponse> ExecuteDualShadowAsync(
        AcceptWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:WarehouseReceivingAcceptance is DualShadow. DahiliStokHareketDuzeltV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    private async Task RecoverMikroApiAcceptanceAsync(
        AcceptWarehouseReceivingRequest request,
        IReadOnlyDictionary<Guid, double> receivedQuantitiesByMovementGuid,
        int transitWarehouseNo,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            if (await TryRecoverMikroApiAcceptanceAsync(
                    request,
                    receivedQuantitiesByMovementGuid,
                    transitWarehouseNo,
                    cancellationToken))
            {
                return;
            }

            if (attempt < MikroApiRecoveryAttemptCount)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(MikroApiRecoveryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Mikro API warehouse receiving acceptance succeeded, but accepted STOK_HAREKETLERI rows could not be verified.");
    }

    private async Task<bool> TryRecoverMikroApiAcceptanceAsync(
        AcceptWarehouseReceivingRequest request,
        IReadOnlyDictionary<Guid, double> receivedQuantitiesByMovementGuid,
        int transitWarehouseNo,
        CancellationToken cancellationToken)
    {
        var movementGuids = receivedQuantitiesByMovementGuid.Keys.ToArray();
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movementGuids.Contains(movement.sth_Guid) &&
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_evrakno_seri == request.DocumentSerie.Trim() &&
                movement.sth_evrakno_sira == request.DocumentOrderNo)
            .Select(movement => new
            {
                movement.sth_Guid,
                movement.sth_FormulMiktar,
                movement.sth_giris_depo_no,
                movement.sth_nakliyedeposu,
                movement.sth_nakliyedurumu
            })
            .ToListAsync(cancellationToken);

        if (rows.Count != receivedQuantitiesByMovementGuid.Count)
        {
            return false;
        }

        foreach (var row in rows)
        {
            var expectedReceivedQuantity = receivedQuantitiesByMovementGuid[row.sth_Guid];

            if (Math.Abs((row.sth_FormulMiktar ?? 0d) - expectedReceivedQuantity) > QuantityTolerance ||
                row.sth_giris_depo_no != request.WarehouseNo ||
                row.sth_nakliyedeposu != transitWarehouseNo ||
                row.sth_nakliyedurumu != DeliveredToTargetWarehouseState)
            {
                return false;
            }
        }

        return true;
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
