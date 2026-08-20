namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;

public sealed record SettingsTypeOptionDto(
    byte Value,
    string Code,
    string Name,
    string Description,
    bool IsKnown);

public sealed record BranchSettingsLookupsDto(
    IReadOnlyCollection<SettingsTypeOptionDto> ScalesTypes,
    IReadOnlyCollection<SettingsTypeOptionDto> CashTypes);

public sealed record CashRegisterSettingsLookupsDto(
    IReadOnlyCollection<SettingsTypeOptionDto> CashTypes,
    IReadOnlyCollection<TerminalBankOptionDto> TerminalBanks);

public sealed record TerminalBankOptionDto(
    string PaymentName,
    int PaymentTypeNo,
    string AccountCode,
    string DisplayName);

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
    string ScalesTypeName,
    string ScalesTypeDescription,
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
    byte CashType,
    string CashTypeName,
    string CashTypeDescription,
    string CashFinanceNumber)
{
    public int CashRegisterNo => CashNo;

    public byte CashRegisterType => CashType;

    public string CashRegisterTypeName => CashTypeName;

    public string CashRegisterTypeDescription => CashTypeDescription;
}

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
    string CashTypeName,
    string CashTypeDescription,
    IReadOnlyCollection<CashRegisterTerminalDto> Terminals);

public sealed record CashRegisterTerminalDto(
    int Id,
    string TerminalNo,
    string Bank,
    string TerminalId,
    string MerchantNo,
    int? CashNo)
{
    public string CashRegisterNo => TerminalNo;
}

public sealed record CashRegisterMessageStatusDto(
    int BranchNo,
    int CashNo,
    byte CashType,
    string CashTypeName,
    string CashTypeDescription,
    int? State,
    string? StateName,
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

public sealed record B2BBulletinDto(
    int Id,
    string Definition,
    string Link,
    DateTime CreateDate);

public sealed record SaveB2BBulletinRequest(
    string Definition,
    string Link,
    DateTime? CreateDate);

public sealed record B2BUserDto(
    Guid UserId,
    string UserFullName,
    string UserMail,
    bool Status,
    DateTime CreateDate,
    string Menus,
    DateTime UserEndDate,
    int AccountCount,
    IReadOnlyCollection<string> Categories);

public sealed record B2BUserDetailDto(
    Guid UserId,
    string UserFullName,
    string UserMail,
    bool Status,
    DateTime CreateDate,
    string Menus,
    DateTime UserEndDate,
    IReadOnlyCollection<B2BUserAccountDto> Accounts);

public sealed record B2BUserAccountDto(
    int Id,
    Guid AccountId,
    string Category);

public sealed record UpdateB2BUserRequest(
    string UserFullName,
    string UserMail,
    bool Status,
    string? Menus,
    DateTime UserEndDate);
