namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;

public interface IAyarlarService
{
    Task<IReadOnlyCollection<DeviceTypeDto>> ListDeviceTypesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DeviceDto>> ListDevicesAsync(int? branchNo, CancellationToken cancellationToken);

    Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken);

    Task DeleteDeviceAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DeviceStatusDto>> CheckDeviceStatusAsync(
        int branchNo,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BranchDetailDto>> ListBranchesAsync(CancellationToken cancellationToken);

    Task<BranchDetailDto> GetBranchAsync(int branchNo, CancellationToken cancellationToken);

    Task<BranchDetailDto> CreateBranchAsync(
        CreateBranchSettingsRequest request,
        CancellationToken cancellationToken);

    Task<BranchDetailDto> UpdateBranchAsync(
        int branchNo,
        UpdateBranchSettingsRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashRegistryDto>> ListBranchCashRegistersAsync(
        int branchNo,
        CancellationToken cancellationToken);

    Task<CashRegisterResponse> CreateCashRegisterAsync(
        CreateCashRegisterRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashRegisterTerminalDto>> ListCashRegisterTerminalsAsync(
        int cashNo,
        CancellationToken cancellationToken);

    Task DeleteCashRegisterAsync(int branchNo, int cashNo, CancellationToken cancellationToken);

    Task DeleteCashRegisterTerminalAsync(int branchNo, string terminalNo, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashRegisterMessageStatusDto>> ReadCashRegisterMessageStatusAsync(
        int branchNo,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashierDto>> ListCashiersAsync(CancellationToken cancellationToken);

    Task<CashierPasswordMutationDto> CreateCashierAsync(
        CreateCashierRequest request,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken);

    Task<CashierDto> UpdateCashierAsync(
        int cashierCode,
        UpdateCashierRequest request,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken);

    Task<CashierPasswordMutationDto> ResetCashierPasswordAsync(
        int cashierCode,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken);
}
