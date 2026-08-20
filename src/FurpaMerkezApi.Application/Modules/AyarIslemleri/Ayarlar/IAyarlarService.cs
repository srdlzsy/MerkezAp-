namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;

public interface IAyarlarService
{
    Task<BranchSettingsLookupsDto> GetBranchSettingsLookupsAsync(CancellationToken cancellationToken);

    Task<CashRegisterSettingsLookupsDto> GetCashRegisterSettingsLookupsAsync(CancellationToken cancellationToken);

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

    Task<IReadOnlyCollection<B2BBulletinDto>> ListB2BBulletinsAsync(
        string? search,
        int take,
        CancellationToken cancellationToken);

    Task<B2BBulletinDto> CreateB2BBulletinAsync(
        SaveB2BBulletinRequest request,
        CancellationToken cancellationToken);

    Task<B2BBulletinDto> UpdateB2BBulletinAsync(
        int id,
        SaveB2BBulletinRequest request,
        CancellationToken cancellationToken);

    Task DeleteB2BBulletinAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<B2BUserDto>> ListB2BUsersAsync(
        string? search,
        bool includeInactive,
        int take,
        CancellationToken cancellationToken);

    Task<B2BUserDetailDto> GetB2BUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<B2BUserDetailDto> UpdateB2BUserAsync(
        Guid userId,
        UpdateB2BUserRequest request,
        CancellationToken cancellationToken);
}
