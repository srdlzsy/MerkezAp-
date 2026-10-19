using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Operations;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Create;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Offline;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class SafeWriteRecoveryDispatcher(
    IGetCompanyReceivingOfflineSyncStatusUseCase companyReceivingStatus,
    IGetInventoryCountOfflineSyncStatusUseCase inventoryCountStatus,
    ICreateIssuedWarehouseOrderUseCase issuedWarehouseOrder,
    ICreateIssuedCompanyOrderUseCase issuedCompanyOrder,
    ICreateInterWarehouseShipmentUseCase interWarehouseShipment,
    ICreateCompanyShipmentUseCase companyShipment,
    ICreateWarehouseReturnUseCase warehouseReturn,
    ICreateCompanyReturnUseCase companyReturn,
    ICreateOutageReceiptUseCase outageReceipt,
    ICreateExpenseReceiptUseCase expenseReceipt,
    ICreateVirmanUseCase virman,
    IGreenGrocerOperationsUseCase greenGrocerOperations) : ISafeWriteRecoveryDispatcher
{
    public async Task<SafeWriteRecoveryResult> RecoverAsync(
        string operationCode,
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        operationCode switch
        {
            "mal-kabul-islemleri.firma-mal-kabulleri.create" => Map(await companyReceivingStatus.ExecuteAsync(
                warehouseNo, requestedByUserId, clientRequestId, cancellationToken)),
            "stok-islemleri.sayim-sonuclari.create" => Map(await inventoryCountStatus.ExecuteAsync(
                warehouseNo, requestedByUserId, clientRequestId, cancellationToken)),
            "siparis-islemleri.verilen-depo-siparisleri.create" => Map(await issuedWarehouseOrder.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "siparis-islemleri.verilen-firma-siparisleri.create" => Map(await issuedCompanyOrder.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "sevk-islemleri.giden-depolar-arasi-sevkler.create" => Map(await interWarehouseShipment.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "sevk-islemleri.giden-firma-sevkleri.create" => Map(await companyShipment.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "iade-islemleri.giden-depo-iadeleri.create" => Map(await warehouseReturn.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "iade-islemleri.firma-iadeleri.create" => Map(await companyReturn.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "stok-islemleri.zayiat-fisleri.create" => Map(await outageReceipt.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "stok-islemleri.masraf-fisleri.create" => Map(await expenseReceipt.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "stok-islemleri.virmanlar.create" => Map(await virman.GetOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            "green-grocer.operations.adjustment.apply" => Map(await greenGrocerOperations.GetAdjustmentOfflineSyncStatusAsync(
                requestedByUserId, clientRequestId, cancellationToken)),
            _ => throw new InvalidOperationException(
                $"Operation '{operationCode}' does not support managed recovery.")
        };

    private static SafeWriteRecoveryResult Map<T>(OfflineSyncStatusDto<T> status) =>
        new(
            status.Status,
            status.Result,
            status.RecoveryState,
            status.RecommendedAction,
            status.ErrorCode,
            status.Retryable);
}
