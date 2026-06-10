namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;

public sealed record DeviceTypeDto(
    int Id,
    string DeviceName);

public sealed record DeviceDto(
    int Id,
    int BranchNo,
    int DeviceTypeId,
    string DeviceTypeName,
    string IpAddress,
    string Description);

public sealed record CreateDeviceRequest(
    int BranchNo,
    int DeviceTypeId,
    string IpAddress,
    string Description);

public sealed record DeviceStatusDto(
    int BranchNo,
    int DeviceTypeId,
    string DeviceTypeName,
    string IpAddress,
    string Description,
    bool Online,
    long? LatencyMs,
    string? Error);

public sealed record BranchDetailDto(
    int BranchNo,
    string BranchIpAddress,
    string BranchScalesFolderPath,
    byte ScalesType,
    string PoskonFolderPath,
    string PosGenelFolderPath);

public sealed record CreateBranchSettingsRequest(
    int BranchNo,
    string BranchIpAddress,
    string BranchScalesFolderPath,
    byte ScalesType,
    string PoskonFolderPath,
    string PosGenelFolderPath,
    IReadOnlyCollection<CreateCashRegistryRequest> CashRegisters);

public sealed record UpdateBranchSettingsRequest(
    string BranchIpAddress,
    string BranchScalesFolderPath,
    byte ScalesType,
    string PoskonFolderPath,
    string PosGenelFolderPath);

public sealed record CreateCashRegistryRequest(
    int CashNo,
    byte CashType);

public sealed record CashRegistryDto(
    int DetailId,
    int BranchNo,
    int CashNo,
    byte CashType);

public sealed record CreateCashRegisterRequest(
    int BranchNo,
    int CashNo,
    byte CashType,
    IReadOnlyCollection<CreateCashRegisterTerminalRequest> Terminals);

public sealed record CreateCashRegisterTerminalRequest(
    string TerminalNo,
    string Bank,
    string TerminalId,
    string MerchantNo);

public sealed record CashRegisterResponse(
    int BranchNo,
    int CashNo,
    byte CashType,
    IReadOnlyCollection<CashRegisterTerminalDto> Terminals);

public sealed record CashRegisterTerminalDto(
    int Id,
    string TerminalNo,
    string Bank,
    string TerminalId,
    string MerchantNo,
    int? CashNo);

public sealed record CashRegisterMessageStatusDto(
    int BranchNo,
    int CashNo,
    byte CashType,
    int? State,
    string FilePath,
    string? Error);

public sealed record CreateCashierRequest(
    string CashierName,
    string CashierAuthorization);

public sealed record UpdateCashierRequest(
    string CashierName,
    string CashierAuthorization,
    bool CashierState);

public sealed record CashierDto(
    int CashierCode,
    string CashierName,
    string CashierAuthorization,
    bool CashierState);

public sealed record CashierPasswordMutationDto(
    int CashierCode,
    string GeneratedPassword,
    CashierDto Cashier);
