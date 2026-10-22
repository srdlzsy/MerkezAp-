using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B;
using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B.Models;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using MikroCashRegisterDetailEntity = FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models.CashRegisterDetailEntity;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Ayarlar;

public sealed class AyarlarService(
    FurpaDbContext furpaDbContext,
    FurpaB2BDbContext furpaB2BDbContext,
    MikroWriteDbContext mikroWriteDbContext)
    : IAyarlarService
{
    private const int DevicePingTimeoutMilliseconds = 1000;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<BranchSettingsLookupsDto> GetBranchSettingsLookupsAsync(
        CancellationToken cancellationToken) =>
        new(
            await ListScalesTypeOptionsAsync(cancellationToken),
            await ListCashTypeOptionsAsync(cancellationToken));

    public async Task<CashRegisterSettingsLookupsDto> GetCashRegisterSettingsLookupsAsync(
        CancellationToken cancellationToken) =>
        new(
            await ListCashTypeOptionsAsync(cancellationToken),
            await ListTerminalBankOptionsAsync(cancellationToken));

    public async Task<IReadOnlyCollection<DeviceTypeDto>> ListDeviceTypesAsync(
        CancellationToken cancellationToken) =>
        await furpaDbContext.DeviceTypes
            .AsNoTracking()
            .OrderBy(item => item.DeviceName)
            .Select(item => new DeviceTypeDto(
                item.Id,
                item.DeviceName))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DeviceDto>> ListDevicesAsync(
        int? branchNo,
        CancellationToken cancellationToken)
    {
        if (branchNo is <= 0)
        {
            throw new ArgumentException("Branch no must be greater than zero.", nameof(branchNo));
        }

        var devices = furpaDbContext.DeviceDetails
            .AsNoTracking()
            .AsQueryable();

        if (branchNo is { } requestedBranchNo)
        {
            devices = devices.Where(item => item.BranchNo == requestedBranchNo);
        }

        return await devices
            .Join(
                furpaDbContext.DeviceTypes.AsNoTracking(),
                device => device.DeviceTypeId,
                deviceType => deviceType.Id,
                (device, deviceType) => new
                {
                    device.Id,
                    device.BranchNo,
                    device.DeviceTypeId,
                    DeviceTypeName = deviceType.DeviceName,
                    device.IpAddress,
                    device.Description
                })
            .OrderBy(item => item.BranchNo)
            .ThenBy(item => item.DeviceTypeName)
            .ThenBy(item => item.IpAddress)
            .Select(item => new DeviceDto(
                item.Id,
                item.BranchNo,
                item.DeviceTypeId,
                item.DeviceTypeName,
                item.IpAddress,
                item.Description))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DeviceDto> CreateDeviceAsync(
        CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(request.BranchNo, nameof(request.BranchNo));
        ValidatePositive(request.DeviceTypeId, nameof(request.DeviceTypeId));
        var ipAddress = NormalizeIpAddress(request.IpAddress);
        var description = NormalizeText(request.Description, 255, nameof(request.Description));

        var deviceType = await furpaDbContext.DeviceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DeviceTypeId, cancellationToken)
            ?? throw new KeyNotFoundException("Device type was not found.");

        var exists = await furpaDbContext.DeviceDetails
            .AnyAsync(
                item =>
                    item.BranchNo == request.BranchNo &&
                    item.DeviceTypeId == request.DeviceTypeId &&
                    item.IpAddress == ipAddress,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Device already exists for the same branch, type and IP address.");
        }

        var entity = new DeviceDetailEntity
        {
            BranchNo = request.BranchNo,
            DeviceTypeId = request.DeviceTypeId,
            IpAddress = ipAddress,
            Description = description
        };

        await furpaDbContext.DeviceDetails.AddAsync(entity, cancellationToken);
        await furpaDbContext.SaveChangesAsync(cancellationToken);

        return new DeviceDto(
            entity.Id,
            entity.BranchNo,
            entity.DeviceTypeId,
            deviceType.DeviceName,
            entity.IpAddress,
            entity.Description);
    }

    public async Task DeleteDeviceAsync(int id, CancellationToken cancellationToken)
    {
        ValidatePositive(id, nameof(id));

        var entity = await furpaDbContext.DeviceDetails
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Device was not found.");

        furpaDbContext.DeviceDetails.Remove(entity);
        await furpaDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeviceStatusDto>> CheckDeviceStatusAsync(
        int branchNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));

        var devices = await furpaDbContext.DeviceDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == branchNo)
            .Join(
                furpaDbContext.DeviceTypes.AsNoTracking(),
                device => device.DeviceTypeId,
                deviceType => deviceType.Id,
                (device, deviceType) => new
                {
                    device.BranchNo,
                    device.DeviceTypeId,
                    DeviceTypeName = deviceType.DeviceName,
                    device.IpAddress,
                    device.Description
                })
            .OrderBy(item => item.DeviceTypeName)
            .ThenBy(item => item.IpAddress)
            .Select(item => new DeviceStatusSource(
                item.BranchNo,
                item.DeviceTypeId,
                item.DeviceTypeName,
                item.IpAddress,
                item.Description))
            .ToArrayAsync(cancellationToken);

        var result = new List<DeviceStatusDto>(devices.Length);
        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await CheckSingleDeviceStatusAsync(device));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<BranchDetailDto>> ListBranchesAsync(
        CancellationToken cancellationToken)
    {
        var branches = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .OrderBy(item => item.BranchNo)
            .ToArrayAsync(cancellationToken);

        return branches
            .Select(ToBranchDto)
            .ToArray();
    }

    public async Task<BranchDetailDto> GetBranchAsync(
        int branchNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));

        var branch = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BranchNo == branchNo, cancellationToken)
            ?? throw new KeyNotFoundException("Branch settings were not found.");

        return ToBranchDto(branch);
    }

    public async Task<BranchDetailDto> CreateBranchAsync(
        CreateBranchSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ValidateBranchRequest(request);
        ValidateCashRegistryRequests(request.CashRegisters);

        var exists = await furpaDbContext.BranchDetails
            .AnyAsync(item => item.BranchNo == request.BranchNo, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("Branch settings already exist.");
        }

        return await ExecuteFurpaTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                var entity = new BranchDetailEntity
                {
                    BranchNo = request.BranchNo,
                    BranchIpAddress = NormalizeText(request.BranchIpAddress, 100, nameof(request.BranchIpAddress)),
                    BranchScalesFolderPath = NormalizeText(request.BranchScalesFolderPath, 255, nameof(request.BranchScalesFolderPath)),
                    ScalesType = request.ScalesType,
                    PoskonFolderPath = NormalizeText(request.PoskonFolderPath, 255, nameof(request.PoskonFolderPath)),
                    PosGenelFolderPath = NormalizeText(request.PosGenelFolderPath, 255, nameof(request.PosGenelFolderPath))
                };

                await furpaDbContext.BranchDetails.AddAsync(entity, cancellationToken);

                if (request.CashRegisters.Count > 0)
                {
                    await furpaDbContext.CashRegistryDetails.AddRangeAsync(
                        request.CashRegisters.Select(item => new CashRegistryDetailEntity
                        {
                            BranchNo = request.BranchNo,
                            CashRegisterNo = item.CashNo,
                            CashRegisterType = item.CashType
                        }),
                        cancellationToken);
                }

                await furpaDbContext.SaveChangesAsync(cancellationToken);
                return ToBranchDto(entity);
            },
            cancellationToken);
    }

    public async Task<BranchDetailDto> UpdateBranchAsync(
        int branchNo,
        UpdateBranchSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidateScalesType(request.ScalesType, nameof(request.ScalesType));

        var entity = await furpaDbContext.BranchDetails
            .FirstOrDefaultAsync(item => item.BranchNo == branchNo, cancellationToken)
            ?? throw new KeyNotFoundException("Branch settings were not found.");

        entity.BranchIpAddress = NormalizeText(request.BranchIpAddress, 100, nameof(request.BranchIpAddress));
        entity.BranchScalesFolderPath = NormalizeText(request.BranchScalesFolderPath, 255, nameof(request.BranchScalesFolderPath));
        entity.ScalesType = request.ScalesType;
        entity.PoskonFolderPath = NormalizeText(request.PoskonFolderPath, 255, nameof(request.PoskonFolderPath));
        entity.PosGenelFolderPath = NormalizeText(request.PosGenelFolderPath, 255, nameof(request.PosGenelFolderPath));

        await furpaDbContext.SaveChangesAsync(cancellationToken);
        return ToBranchDto(entity);
    }

    public async Task<IReadOnlyCollection<CashRegistryDto>> ListBranchCashRegistersAsync(
        int branchNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));

        var cashRegisters = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == branchNo)
            .OrderBy(item => item.CashRegisterNo)
            .ToArrayAsync(cancellationToken);

        var cashFinanceNumbers = await ResolveCashFinanceNumbersAsync(
            branchNo,
            cashRegisters.Select(item => item.CashRegisterNo).ToArray(),
            cancellationToken);

        return cashRegisters
            .Select(item => ToCashRegistryDto(
                item,
                cashFinanceNumbers.GetValueOrDefault(item.CashRegisterNo) ?? string.Empty))
            .ToArray();
    }

    public async Task<CashRegisterResponse> CreateCashRegisterAsync(
        CreateCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCreateCashRegisterRequest(request);
        var terminalRequests = NormalizeTerminalRequests(request.Terminals);

        var branchExists = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .AnyAsync(item => item.BranchNo == request.BranchNo, cancellationToken);

        if (!branchExists)
        {
            throw new KeyNotFoundException("Branch settings were not found.");
        }

        var cashRegistryExists = await furpaDbContext.CashRegistryDetails
            .AnyAsync(
                item =>
                    item.BranchNo == request.BranchNo &&
                    item.CashRegisterNo == request.CashNo,
                cancellationToken);

        if (cashRegistryExists)
        {
            throw new InvalidOperationException("Cash register already exists for this branch.");
        }

        foreach (var terminal in terminalRequests)
        {
            await ValidateMikroTerminalScopeAsync(
                request.BranchNo,
                request.CashNo,
                terminal,
                excludedTerminalId: null,
                cancellationToken);
        }

        return await ExecuteFurpaTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                mikroWriteDbContext.ChangeTracker.Clear();

                var cashRegistry = new CashRegistryDetailEntity
                {
                    BranchNo = request.BranchNo,
                    CashRegisterNo = request.CashNo,
                    CashRegisterType = request.CashType
                };

                await furpaDbContext.CashRegistryDetails.AddAsync(cashRegistry, cancellationToken);

                var terminalEntities = terminalRequests
                    .Select(item => new MikroCashRegisterDetailEntity
                    {
                        CashRegisterNo = item.TerminalNo,
                        Bank = item.Bank,
                        TerminalId = item.TerminalId,
                        MerchantNo = item.MerchantNo,
                        CashNo = request.CashNo
                    })
                    .ToArray();

                var branchEntities = terminalRequests
                    .Select(item => item.TerminalNo)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(item => new CashRegisterBranchEntity
                    {
                        CashRegisterNo = item,
                        BranchNo = request.BranchNo
                    })
                    .ToArray();

                await mikroWriteDbContext.CashRegisterDetails.AddRangeAsync(terminalEntities, cancellationToken);
                await mikroWriteDbContext.CashRegisterBranches.AddRangeAsync(branchEntities, cancellationToken);

                await furpaDbContext.SaveChangesAsync(cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

                return new CashRegisterResponse(
                    request.BranchNo,
                    request.CashNo,
                    request.CashType,
                    ResolveCashTypeName(request.CashType),
                    ResolveCashTypeDescription(request.CashType),
                    terminalEntities
                        .OrderBy(item => item.CashRegisterNo)
                        .Select(ToTerminalDto)
                        .ToArray());
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashRegisterTerminalDto>> ListCashRegisterTerminalsAsync(
        int branchNo,
        int cashNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidatePositive(cashNo, nameof(cashNo));

        var selectedRows = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item =>
                item.CashNo == cashNo &&
                mikroWriteDbContext.CashRegisterBranches.Any(mapping =>
                    mapping.BranchNo == branchNo && mapping.CashRegisterNo == item.CashRegisterNo))
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

        return selectedRows
            .OrderBy(item => item.CashRegisterNo)
            .ThenBy(item => item.Bank)
            .ThenBy(item => item.TerminalId)
            .Select(ToTerminalDto)
            .ToArray();
    }

    public async Task<CashRegistryDto> UpdateCashRegisterTypeAsync(
        int branchNo,
        int cashNo,
        UpdateCashRegisterTypeRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidatePositive(cashNo, nameof(cashNo));
        _ = SettingsTypeCatalog.ResolveCashTypeOption(request.CashType);

        var cashRegistry = await furpaDbContext.CashRegistryDetails
            .FirstOrDefaultAsync(
                item => item.BranchNo == branchNo && item.CashRegisterNo == cashNo,
                cancellationToken)
            ?? throw new KeyNotFoundException("Cash register was not found for this branch.");

        cashRegistry.CashRegisterType = request.CashType;
        await furpaDbContext.SaveChangesAsync(cancellationToken);

        var financeNumbers = await ResolveCashFinanceNumbersAsync(
            branchNo,
            [cashNo],
            cancellationToken);
        return ToCashRegistryDto(
            cashRegistry,
            financeNumbers.GetValueOrDefault(cashNo) ?? string.Empty);
    }

    public async Task<CashRegisterTerminalDto> CreateCashRegisterTerminalAsync(
        int branchNo,
        int cashNo,
        CreateCashRegisterTerminalRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCashRegisterExistsAsync(branchNo, cashNo, cancellationToken);
        var terminal = NormalizeTerminalRequest(request);

        await ValidateMikroTerminalScopeAsync(
            branchNo,
            cashNo,
            terminal,
            excludedTerminalId: null,
            cancellationToken);

        var mikroEntity = new MikroCashRegisterDetailEntity
        {
            CashRegisterNo = terminal.TerminalNo,
            Bank = terminal.Bank,
            TerminalId = terminal.TerminalId,
            MerchantNo = terminal.MerchantNo,
            CashNo = cashNo
        };

        var mappingExists = await mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .AnyAsync(
                item => item.BranchNo == branchNo && item.CashRegisterNo == terminal.TerminalNo,
                cancellationToken);

        await mikroWriteDbContext.CashRegisterDetails.AddAsync(mikroEntity, cancellationToken);
        if (!mappingExists)
        {
            await mikroWriteDbContext.CashRegisterBranches.AddAsync(
                new CashRegisterBranchEntity
                {
                    BranchNo = branchNo,
                    CashRegisterNo = terminal.TerminalNo
                },
                cancellationToken);
        }

        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
        return ToTerminalDto(mikroEntity);
    }

    public async Task<CashRegisterTerminalDto> UpdateCashRegisterTerminalAsync(
        int branchNo,
        int cashNo,
        int terminalId,
        CreateCashRegisterTerminalRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(terminalId, nameof(terminalId));
        await EnsureCashRegisterExistsAsync(branchNo, cashNo, cancellationToken);
        var terminal = NormalizeTerminalRequest(request);

        var mikroEntity = await mikroWriteDbContext.CashRegisterDetails
            .FirstOrDefaultAsync(
                item => item.Id == terminalId && item.CashNo == cashNo,
                cancellationToken)
            ?? throw new KeyNotFoundException("Terminal detail was not found.");
        var previousTerminalNo = mikroEntity.CashRegisterNo ?? string.Empty;

        var currentBranchMappingExists = await mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .AnyAsync(
                item => item.BranchNo == branchNo && item.CashRegisterNo == previousTerminalNo,
                cancellationToken);
        if (!currentBranchMappingExists)
        {
            throw new KeyNotFoundException("Terminal detail was not found for this branch.");
        }

        if (!string.Equals(previousTerminalNo, terminal.TerminalNo, StringComparison.OrdinalIgnoreCase))
        {
            var previousGroupRowCount = await mikroWriteDbContext.CashRegisterDetails
                .AsNoTracking()
                .CountAsync(
                    item => item.CashRegisterNo == previousTerminalNo,
                    cancellationToken);
            if (previousGroupRowCount > 1)
            {
                throw new InvalidOperationException(
                    $"{previousTerminalNo} birden fazla banka satiri olan POS/PAV grubudur. Grup numarasi tek satirdan degistirilemez; konum duzeltmesi icin 'Dogru Kasaya Tasi' islemini kullanin.");
            }
        }

        await ValidateMikroTerminalScopeAsync(
            branchNo,
            cashNo,
            terminal,
            terminalId,
            cancellationToken);

        ApplyTerminalValues(mikroEntity, terminal);

        var newMappingExists = await mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .AnyAsync(
                item => item.BranchNo == branchNo && item.CashRegisterNo == terminal.TerminalNo,
                cancellationToken);

        if (!newMappingExists)
        {
            await mikroWriteDbContext.CashRegisterBranches.AddAsync(
                new CashRegisterBranchEntity
                {
                    BranchNo = branchNo,
                    CashRegisterNo = terminal.TerminalNo
                },
                cancellationToken);
        }

        if (!string.Equals(previousTerminalNo, terminal.TerminalNo, StringComparison.OrdinalIgnoreCase))
        {
            var oldGroupHasOtherRows = await mikroWriteDbContext.CashRegisterDetails
                .AsNoTracking()
                .AnyAsync(
                    item => item.Id != terminalId && item.CashRegisterNo == previousTerminalNo,
                    cancellationToken);

            if (!oldGroupHasOtherRows)
            {
                var oldMappings = await mikroWriteDbContext.CashRegisterBranches
                    .Where(item => item.BranchNo == branchNo && item.CashRegisterNo == previousTerminalNo)
                    .ToArrayAsync(cancellationToken);
                mikroWriteDbContext.CashRegisterBranches.RemoveRange(oldMappings);
            }
        }

        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
        return ToTerminalDto(mikroEntity);
    }

    public async Task<IReadOnlyCollection<CashRegisterTerminalLocationDto>> FindCashRegisterTerminalLocationsAsync(
        string terminalNo,
        int? branchNo,
        CancellationToken cancellationToken)
    {
        var normalizedTerminalNo = NormalizeText(terminalNo, 40, nameof(terminalNo));
        if (branchNo.HasValue)
        {
            ValidatePositive(branchNo.Value, nameof(branchNo));
        }

        return await ResolveTerminalLocationsAsync(
            normalizedTerminalNo,
            branchNo,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashRegisterTerminalDto>> MoveCashRegisterTerminalGroupAsync(
        int sourceBranchNo,
        int sourceCashNo,
        string terminalNo,
        MoveCashRegisterTerminalGroupRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(sourceBranchNo, nameof(sourceBranchNo));
        ValidatePositive(sourceCashNo, nameof(sourceCashNo));
        ValidatePositive(request.TargetBranchNo, nameof(request.TargetBranchNo));
        ValidatePositive(request.TargetCashNo, nameof(request.TargetCashNo));
        var normalizedTerminalNo = NormalizeText(terminalNo, 40, nameof(terminalNo));

        if (sourceBranchNo == request.TargetBranchNo && sourceCashNo == request.TargetCashNo)
        {
            throw new ArgumentException("Hedef sube ve kasa mevcut konumdan farkli olmalidir.", nameof(request));
        }

        await EnsureCashRegisterExistsAsync(sourceBranchNo, sourceCashNo, cancellationToken);
        await EnsureCashRegisterExistsAsync(request.TargetBranchNo, request.TargetCashNo, cancellationToken);

        var groupRows = await mikroWriteDbContext.CashRegisterDetails
            .Where(item => item.CashRegisterNo == normalizedTerminalNo)
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        if (groupRows.Length == 0)
        {
            throw new KeyNotFoundException($"{normalizedTerminalNo} POS/PAV terminal grubu bulunamadi.");
        }

        if (groupRows.Any(item => item.CashNo != sourceCashNo))
        {
            var locations = await ResolveTerminalLocationsAsync(
                normalizedTerminalNo,
                branchNo: null,
                cancellationToken);
            throw new InvalidOperationException(BuildTerminalLocationConflictMessage(normalizedTerminalNo, locations));
        }

        var mappings = await mikroWriteDbContext.CashRegisterBranches
            .Where(item => item.CashRegisterNo == normalizedTerminalNo)
            .ToArrayAsync(cancellationToken);

        if (!mappings.Any(item => item.BranchNo == sourceBranchNo))
        {
            throw new KeyNotFoundException(
                $"{normalizedTerminalNo} POS/PAV grubu {sourceBranchNo} subesinde bulunamadi.");
        }

        var otherBranchMappings = mappings
            .Where(item => item.BranchNo != sourceBranchNo)
            .Select(item => item.BranchNo)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        if (otherBranchMappings.Length > 0)
        {
            throw new InvalidOperationException(
                $"{normalizedTerminalNo} POS/PAV grubu birden fazla subeye bagli ({string.Join(", ", otherBranchMappings)}). Tasimadan once eslemeler duzeltilmelidir.");
        }

        foreach (var row in groupRows)
        {
            row.CashNo = request.TargetCashNo;
        }

        if (sourceBranchNo != request.TargetBranchNo)
        {
            mikroWriteDbContext.CashRegisterBranches.RemoveRange(mappings);
            await mikroWriteDbContext.CashRegisterBranches.AddAsync(
                new CashRegisterBranchEntity
                {
                    BranchNo = request.TargetBranchNo,
                    CashRegisterNo = normalizedTerminalNo
                },
                cancellationToken);
        }

        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);

        return groupRows.Select(ToTerminalDto).ToArray();
    }

    public async Task DeleteCashRegisterAsync(
        int branchNo,
        int cashNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidatePositive(cashNo, nameof(cashNo));

        _ = await ExecuteFurpaTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                mikroWriteDbContext.ChangeTracker.Clear();

                var cashRegistry = await furpaDbContext.CashRegistryDetails
                    .FirstOrDefaultAsync(
                        item =>
                            item.BranchNo == branchNo &&
                            item.CashRegisterNo == cashNo,
                        cancellationToken)
                    ?? throw new KeyNotFoundException("Cash register was not found for this branch.");

                var terminalDetails = await mikroWriteDbContext.CashRegisterDetails
                    .Where(item => item.CashNo == cashNo)
                    .ToArrayAsync(cancellationToken);
                var terminalNos = terminalDetails
                    .Select(item => item.CashRegisterNo)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var terminalMappings = terminalNos.Length == 0
                    ? Array.Empty<CashRegisterBranchEntity>()
                    : await mikroWriteDbContext.CashRegisterBranches
                        .Where(item => terminalNos.Contains(item.CashRegisterNo))
                        .ToArrayAsync(cancellationToken);

                var mappedToOtherBranches = terminalMappings
                    .Where(item => item.BranchNo != branchNo)
                    .Select(item => item.CashRegisterNo)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var removableTerminalNos = terminalNos
                    .Where(item => !mappedToOtherBranches.Contains(item))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var terminalDetailsToRemove = terminalDetails
                    .Where(item =>
                        !string.IsNullOrWhiteSpace(item.CashRegisterNo) &&
                        removableTerminalNos.Contains(item.CashRegisterNo))
                    .ToArray();
                var terminalMappingsToRemove = terminalMappings
                    .Where(item =>
                        item.BranchNo == branchNo &&
                        removableTerminalNos.Contains(item.CashRegisterNo))
                    .ToArray();

                furpaDbContext.CashRegistryDetails.Remove(cashRegistry);
                mikroWriteDbContext.CashRegisterBranches.RemoveRange(terminalMappingsToRemove);
                mikroWriteDbContext.CashRegisterDetails.RemoveRange(terminalDetailsToRemove);

                await furpaDbContext.SaveChangesAsync(cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public async Task DeleteCashRegisterTerminalAsync(
        int branchNo,
        int cashNo,
        int terminalId,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidatePositive(cashNo, nameof(cashNo));
        ValidatePositive(terminalId, nameof(terminalId));

        var cashRegistryExists = await furpaDbContext.CashRegistryDetails
            .AnyAsync(
                item => item.BranchNo == branchNo && item.CashRegisterNo == cashNo,
                cancellationToken);

        if (!cashRegistryExists)
        {
            throw new KeyNotFoundException("Cash register was not found for this branch.");
        }

        var mikroDetail = await mikroWriteDbContext.CashRegisterDetails
            .FirstOrDefaultAsync(
                item => item.Id == terminalId && item.CashNo == cashNo,
                cancellationToken)
            ?? throw new KeyNotFoundException("Terminal detail was not found.");

        var mapping = await mikroWriteDbContext.CashRegisterBranches
            .Where(
                item =>
                    item.BranchNo == branchNo &&
                    item.CashRegisterNo == mikroDetail.CashRegisterNo)
            .ToArrayAsync(cancellationToken);

        if (mapping.Length == 0)
        {
            throw new KeyNotFoundException("Terminal detail was not found for this branch.");
        }

        var groupHasOtherRows = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.Id != terminalId &&
                    item.CashRegisterNo == mikroDetail.CashRegisterNo,
                cancellationToken);

        mikroWriteDbContext.CashRegisterDetails.Remove(mikroDetail);
        if (!groupHasOtherRows)
        {
            mikroWriteDbContext.CashRegisterBranches.RemoveRange(mapping);
        }

        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashRegisterMessageStatusDto>> ReadCashRegisterMessageStatusAsync(
        int branchNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));

        var branch = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BranchNo == branchNo, cancellationToken)
            ?? throw new KeyNotFoundException("Branch settings were not found.");

        var cashRegisters = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == branchNo)
            .OrderBy(item => item.CashRegisterNo)
            .ToArrayAsync(cancellationToken);

        var result = new List<CashRegisterMessageStatusDto>(cashRegisters.Length);
        foreach (var cashRegister in cashRegisters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await ReadSingleMessageStatusAsync(branch, cashRegister, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<CashierDto>> ListCashiersAsync(
        CancellationToken cancellationToken) =>
        await furpaDbContext.Cashiers
            .AsNoTracking()
            .OrderBy(item => item.CashierCode)
            .Select(item => new CashierDto(
                item.CashierCode,
                item.CashierName,
                item.CashierAuthorization,
                item.CashierState))
            .ToArrayAsync(cancellationToken);

    public async Task<CashierPasswordDto> GetCashierPasswordAsync(
        int cashierCode,
        CancellationToken cancellationToken)
    {
        ValidatePositive(cashierCode, nameof(cashierCode));

        return await furpaDbContext.Cashiers
            .AsNoTracking()
            .Where(item => item.CashierCode == cashierCode)
            .Select(item => new CashierPasswordDto(
                item.CashierCode,
                item.CashierName,
                item.CashierPassword))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Cashier was not found.");
    }

    public async Task<CashierPasswordMutationDto> CreateCashierAsync(
        CreateCashierRequest request,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken)
    {
        ValidateNonNegative(currentUserWarehouseNo, nameof(currentUserWarehouseNo));
        var cashierName = NormalizeCashierName(request.CashierName);
        var authorization = NormalizeText(request.CashierAuthorization, 1, nameof(request.CashierAuthorization));

        return await ExecuteFurpaTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                var lastCode = await furpaDbContext.Cashiers
                    .Select(item => (int?)item.CashierCode)
                    .MaxAsync(cancellationToken) ?? 0;
                var generatedPassword = GenerateNumericPassword();
                var now = DateTime.Now;

                var entity = new CashierEntity
                {
                    CreateUser = currentUserWarehouseNo,
                    CreateDate = now,
                    UpdateUser = currentUserWarehouseNo,
                    UpdateDate = now,
                    CashierCode = lastCode + 1,
                    CashierName = cashierName,
                    CashierPassword = generatedPassword,
                    CashierAuthorization = authorization,
                    CashierState = true
                };

                await furpaDbContext.Cashiers.AddAsync(entity, cancellationToken);
                await furpaDbContext.SaveChangesAsync(cancellationToken);
                await SynchronizeCashierDefinitionAsync(entity, cancellationToken);
                await furpaDbContext.SaveChangesAsync(cancellationToken);

                var cashier = ToCashierDto(entity);
                return new CashierPasswordMutationDto(
                    cashier.CashierCode,
                    generatedPassword,
                    cashier);
            },
            cancellationToken);
    }

    public async Task<CashierDto> UpdateCashierAsync(
        int cashierCode,
        UpdateCashierRequest request,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(cashierCode, nameof(cashierCode));
        ValidateNonNegative(currentUserWarehouseNo, nameof(currentUserWarehouseNo));

        return await ExecuteFurpaTransactionAsync(
            IsolationLevel.ReadCommitted,
            async () =>
            {
                var entity = await furpaDbContext.Cashiers
                    .FirstOrDefaultAsync(item => item.CashierCode == cashierCode, cancellationToken)
                    ?? throw new KeyNotFoundException("Cashier was not found.");

                entity.UpdateUser = currentUserWarehouseNo;
                entity.UpdateDate = DateTime.Now;
                entity.CashierName = NormalizeCashierName(request.CashierName);
                entity.CashierAuthorization = NormalizeText(request.CashierAuthorization, 1, nameof(request.CashierAuthorization));
                entity.CashierState = request.CashierState;

                await SynchronizeCashierDefinitionAsync(entity, cancellationToken);
                await furpaDbContext.SaveChangesAsync(cancellationToken);
                return ToCashierDto(entity);
            },
            cancellationToken);
    }

    public async Task<CashierPasswordMutationDto> ResetCashierPasswordAsync(
        int cashierCode,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(cashierCode, nameof(cashierCode));
        ValidateNonNegative(currentUserWarehouseNo, nameof(currentUserWarehouseNo));

        return await ExecuteFurpaTransactionAsync(
            IsolationLevel.ReadCommitted,
            async () =>
            {
                var entity = await furpaDbContext.Cashiers
                    .FirstOrDefaultAsync(item => item.CashierCode == cashierCode, cancellationToken)
                    ?? throw new KeyNotFoundException("Cashier was not found.");

                var generatedPassword = GenerateNumericPassword();
                entity.CashierPassword = generatedPassword;
                entity.UpdateUser = currentUserWarehouseNo;
                entity.UpdateDate = DateTime.Now;

                await SynchronizeCashierDefinitionAsync(entity, cancellationToken);
                await furpaDbContext.SaveChangesAsync(cancellationToken);

                var cashier = ToCashierDto(entity);
                return new CashierPasswordMutationDto(
                    cashier.CashierCode,
                    generatedPassword,
                    cashier);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<B2BBulletinDto>> ListB2BBulletinsAsync(
        string? search,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedTake = NormalizeTake(take, 100, 500);
        var filter = search?.Trim();

        var query = furpaB2BDbContext.Bulletins.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(item =>
                (item.BultenDefination != null && EF.Functions.Like(item.BultenDefination, $"%{filter}%")) ||
                (item.BultenLink != null && EF.Functions.Like(item.BultenLink, $"%{filter}%")));
        }

        return await query
            .OrderByDescending(item => item.BultenCreateDate)
            .ThenByDescending(item => item.Id)
            .Take(normalizedTake)
            .Select(item => ToB2BBulletinDto(item))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<B2BBulletinDto> CreateB2BBulletinAsync(
        SaveB2BBulletinRequest request,
        CancellationToken cancellationToken)
    {
        var entity = new B2BBulletinEntity
        {
            BultenDefination = NormalizeOptionalText(request.Definition, nameof(request.Definition)),
            BultenLink = NormalizeOptionalText(request.Link, nameof(request.Link)),
            BultenCreateDate = request.CreateDate ?? DateTime.Now
        };

        await furpaB2BDbContext.Bulletins.AddAsync(entity, cancellationToken);
        await furpaB2BDbContext.SaveChangesAsync(cancellationToken);

        return ToB2BBulletinDto(entity);
    }

    public async Task<B2BBulletinDto> UpdateB2BBulletinAsync(
        int id,
        SaveB2BBulletinRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(id, nameof(id));

        var entity = await furpaB2BDbContext.Bulletins
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("B2B bulletin was not found.");

        entity.BultenDefination = NormalizeOptionalText(request.Definition, nameof(request.Definition));
        entity.BultenLink = NormalizeOptionalText(request.Link, nameof(request.Link));
        entity.BultenCreateDate = request.CreateDate ?? entity.BultenCreateDate;

        await furpaB2BDbContext.SaveChangesAsync(cancellationToken);

        return ToB2BBulletinDto(entity);
    }

    public async Task DeleteB2BBulletinAsync(int id, CancellationToken cancellationToken)
    {
        ValidatePositive(id, nameof(id));

        var entity = await furpaB2BDbContext.Bulletins
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("B2B bulletin was not found.");

        furpaB2BDbContext.Bulletins.Remove(entity);
        await furpaB2BDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<B2BUserDto>> ListB2BUsersAsync(
        string? search,
        bool includeInactive,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedTake = NormalizeTake(take, 100, 500);
        var filter = search?.Trim();

        var query = furpaB2BDbContext.Users.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(item => item.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(item =>
                EF.Functions.Like(item.UserFullName, $"%{filter}%") ||
                EF.Functions.Like(item.UserMail, $"%{filter}%") ||
                (item.Menus != null && EF.Functions.Like(item.Menus, $"%{filter}%")));
        }

        var users = await query
            .OrderBy(item => item.UserFullName)
            .Take(normalizedTake)
            .ToArrayAsync(cancellationToken);

        var userIds = users.Select(item => item.UserId).ToArray();
        var accounts = await furpaB2BDbContext.UserAccounts
            .AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .ToArrayAsync(cancellationToken);

        var accountsByUserId = accounts
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return users
            .Select(item =>
            {
                var userAccounts = accountsByUserId.GetValueOrDefault(item.UserId) ?? [];
                return ToB2BUserDto(item, userAccounts);
            })
            .ToArray();
    }

    public async Task<B2BUserDetailDto> GetB2BUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await furpaB2BDbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("B2B user was not found.");

        var accounts = await furpaB2BDbContext.UserAccounts
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return ToB2BUserDetailDto(user, accounts);
    }

    public async Task<B2BUserDetailDto> UpdateB2BUserAsync(
        Guid userId,
        UpdateB2BUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await furpaB2BDbContext.Users
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("B2B user was not found.");

        user.UserFullName = NormalizeText(request.UserFullName, 70, nameof(request.UserFullName));
        user.UserMail = NormalizeText(request.UserMail, 150, nameof(request.UserMail));
        user.Status = request.Status;
        user.Menus = NormalizeNullableText(request.Menus);
        user.UserEndDate = request.UserEndDate;

        await furpaB2BDbContext.SaveChangesAsync(cancellationToken);

        var accounts = await furpaB2BDbContext.UserAccounts
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return ToB2BUserDetailDto(user, accounts);
    }

    private static async Task<DeviceStatusDto> CheckSingleDeviceStatusAsync(DeviceStatusSource device)
    {
        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            return new DeviceStatusDto(
                device.BranchNo,
                device.DeviceTypeId,
                device.DeviceTypeName,
                device.IpAddress,
                device.Description,
                false,
                null,
                "IP address is empty.");
        }

        try
        {
            using var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(device.IpAddress, DevicePingTimeoutMilliseconds);
            stopwatch.Stop();

            var online = reply.Status == IPStatus.Success;
            return new DeviceStatusDto(
                device.BranchNo,
                device.DeviceTypeId,
                device.DeviceTypeName,
                device.IpAddress,
                device.Description,
                online,
                online ? reply.RoundtripTime : null,
                online ? null : reply.Status.ToString());
        }
        catch (Exception exception) when (exception is PingException or InvalidOperationException or ArgumentException)
        {
            return new DeviceStatusDto(
                device.BranchNo,
                device.DeviceTypeId,
                device.DeviceTypeName,
                device.IpAddress,
                device.Description,
                false,
                null,
                exception.Message);
        }
    }

    private static async Task<CashRegisterMessageStatusDto> ReadSingleMessageStatusAsync(
        BranchDetailEntity branch,
        CashRegistryDetailEntity cashRegister,
        CancellationToken cancellationToken)
    {
        var filePath = BuildMessageFilePath(branch, cashRegister.CashRegisterNo);

        if (string.IsNullOrWhiteSpace(branch.BranchIpAddress) ||
            string.IsNullOrWhiteSpace(branch.PoskonFolderPath))
        {
            return new CashRegisterMessageStatusDto(
                branch.BranchNo,
                cashRegister.CashRegisterNo,
                cashRegister.CashRegisterType,
                ResolveCashTypeName(cashRegister.CashRegisterType),
                ResolveCashTypeDescription(cashRegister.CashRegisterType),
                null,
                null,
                filePath,
                "Branch IP address or POSKON folder path is empty.");
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);
            using var reader = new StreamReader(stream);
            var firstLine = await reader.ReadLineAsync(cancellationToken);
            var state = firstLine?.Contains("1071", StringComparison.OrdinalIgnoreCase) == true ? 0 : 1;

            return new CashRegisterMessageStatusDto(
                branch.BranchNo,
                cashRegister.CashRegisterNo,
                cashRegister.CashRegisterType,
                ResolveCashTypeName(cashRegister.CashRegisterType),
                ResolveCashTypeDescription(cashRegister.CashRegisterType),
                state,
                ResolveMessageStateName(state),
                filePath,
                null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CashRegisterMessageStatusDto(
                branch.BranchNo,
                cashRegister.CashRegisterNo,
                cashRegister.CashRegisterType,
                ResolveCashTypeName(cashRegister.CashRegisterType),
                ResolveCashTypeDescription(cashRegister.CashRegisterType),
                null,
                null,
                filePath,
                exception.Message);
        }
    }

    private static string BuildMessageFilePath(BranchDetailEntity branch, int cashNo)
    {
        var host = branch.BranchIpAddress.Trim().Trim('\\', '/');
        var folder = branch.PoskonFolderPath.Trim().Trim('\\', '/');
        return $@"\\{host}\{folder}\MESAJ.{cashNo.ToString("000", CultureInfo.InvariantCulture)}";
    }

    private async Task<IReadOnlyCollection<SettingsTypeOptionDto>> ListScalesTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        var configuredValues = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .Select(item => item.ScalesType)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return MergeTypeOptions(
            SettingsTypeCatalog.GetScalesTypeOptions(),
            configuredValues,
            SettingsTypeCatalog.ResolveScalesTypeOption);
    }

    private async Task<IReadOnlyCollection<SettingsTypeOptionDto>> ListCashTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        var configuredValues = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Select(item => item.CashRegisterType)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return MergeTypeOptions(
            SettingsTypeCatalog.GetCashTypeOptions(),
            configuredValues,
            SettingsTypeCatalog.ResolveCashTypeOption);
    }

    private async Task<IReadOnlyCollection<TerminalBankOptionDto>> ListTerminalBankOptionsAsync(
        CancellationToken cancellationToken)
    {
        var paymentTypes = await mikroWriteDbContext.PaymentTypes
            .AsNoTracking()
            .Where(item => item.PaymentGenus == 1)
            .OrderBy(item => item.PaymentName)
            .Select(item => new
            {
                PaymentName = item.PaymentName ?? string.Empty,
                item.PaymentTypeNo,
                AccountCode = item.AccountCode ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        return paymentTypes
            .Where(item => !string.IsNullOrWhiteSpace(item.PaymentName))
            .Select(item => new TerminalBankOptionDto(
                item.PaymentName,
                item.PaymentTypeNo,
                item.AccountCode,
                string.IsNullOrWhiteSpace(item.AccountCode)
                    ? item.PaymentName
                    : item.PaymentName + " - " + item.AccountCode))
            .ToArray();
    }
    private static BranchDetailDto ToBranchDto(BranchDetailEntity item) =>
        new(
            item.BranchNo,
            item.BranchIpAddress,
            item.BranchScalesFolderPath,
            item.ScalesType,
            ResolveScalesTypeName(item.ScalesType),
            ResolveScalesTypeDescription(item.ScalesType),
            item.PoskonFolderPath,
            item.PosGenelFolderPath);

    private async Task<IReadOnlyDictionary<int, string>> ResolveCashFinanceNumbersAsync(
        int branchNo,
        IReadOnlyCollection<int> cashNos,
        CancellationToken cancellationToken)
    {
        if (cashNos.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var mikroRows = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item =>
                item.CashNo.HasValue &&
                cashNos.Contains(item.CashNo.Value) &&
                mikroWriteDbContext.CashRegisterBranches.Any(mapping =>
                    mapping.BranchNo == branchNo && mapping.CashRegisterNo == item.CashRegisterNo))
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                CashNo = item.CashNo!.Value,
                CashRegisterNo = item.CashRegisterNo ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        var financeNumbersByCashNo = mikroRows
            .Where(item => !string.IsNullOrWhiteSpace(item.CashRegisterNo))
            .GroupBy(item => item.CashNo)
            .Select(group => new
            {
                CashNo = group.Key,
                FinanceNumbers = group.Select(item => item.CashRegisterNo)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .Where(group => group.FinanceNumbers.Length == 1)
            .ToDictionary(
                group => group.CashNo,
                group => group.FinanceNumbers[0],
                EqualityComparer<int>.Default);

        return financeNumbersByCashNo;
    }

    private static CashRegistryDto ToCashRegistryDto(CashRegistryDetailEntity item, string cashFinanceNumber) =>
        new(
            item.DetailId,
            item.BranchNo,
            item.CashRegisterNo,
            item.CashRegisterType,
            ResolveCashTypeName(item.CashRegisterType),
            ResolveCashTypeDescription(item.CashRegisterType),
            cashFinanceNumber);

    private static CashRegisterTerminalDto ToTerminalDto(MikroCashRegisterDetailEntity item) =>
        new(
            item.Id,
            item.CashRegisterNo ?? string.Empty,
            item.Bank ?? string.Empty,
            item.TerminalId ?? string.Empty,
            item.MerchantNo ?? string.Empty,
            item.CashNo);

    private static CashRegisterTerminalDto ToTerminalDto(CashRegisterTerminalSource item) =>
        new(
            item.Id,
            item.CashRegisterNo,
            item.Bank,
            item.TerminalId,
            item.MerchantNo,
            item.CashNo);

    private sealed record CashRegisterTerminalSource(
        int Id,
        string CashRegisterNo,
        string Bank,
        string TerminalId,
        string MerchantNo,
        int? CashNo);

    private static CashierDto ToCashierDto(CashierEntity item) =>
        new(
            item.CashierCode,
            item.CashierName,
            item.CashierAuthorization,
            item.CashierState);

    private static B2BBulletinDto ToB2BBulletinDto(B2BBulletinEntity item) =>
        new(
            item.Id,
            item.BultenDefination ?? string.Empty,
            item.BultenLink ?? string.Empty,
            item.BultenCreateDate);

    private static B2BUserDto ToB2BUserDto(
        B2BUserEntity item,
        IReadOnlyCollection<B2BUserAccountEntity> accounts) =>
        new(
            item.UserId,
            item.UserFullName,
            item.UserMail,
            item.Status,
            item.CreateDate,
            item.Menus ?? string.Empty,
            item.UserEndDate,
            accounts.Count,
            accounts
                .Select(account => account.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category)
                .ToArray());

    private static B2BUserDetailDto ToB2BUserDetailDto(
        B2BUserEntity item,
        IReadOnlyCollection<B2BUserAccountEntity> accounts) =>
        new(
            item.UserId,
            item.UserFullName,
            item.UserMail,
            item.Status,
            item.CreateDate,
            item.Menus ?? string.Empty,
            item.UserEndDate,
            accounts
                .Select(account => new B2BUserAccountDto(
                    account.Id,
                    account.AccountId,
                    account.Category))
                .ToArray());

    private static void ValidateBranchRequest(CreateBranchSettingsRequest request)
    {
        ValidatePositive(request.BranchNo, nameof(request.BranchNo));
        _ = NormalizeText(request.BranchIpAddress, 100, nameof(request.BranchIpAddress));
        _ = NormalizeText(request.BranchScalesFolderPath, 255, nameof(request.BranchScalesFolderPath));
        ValidateScalesType(request.ScalesType, nameof(request.ScalesType));
        _ = NormalizeText(request.PoskonFolderPath, 255, nameof(request.PoskonFolderPath));
        _ = NormalizeText(request.PosGenelFolderPath, 255, nameof(request.PosGenelFolderPath));
    }

    private static void ValidateCashRegistryRequests(IReadOnlyCollection<CreateCashRegistryRequest> cashRegisters)
    {
        var duplicateCashNos = cashRegisters
            .GroupBy(item => item.CashNo)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateCashNos.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate cash register numbers were found: {string.Join(", ", duplicateCashNos)}");
        }

        foreach (var cashRegister in cashRegisters)
        {
            ValidatePositive(cashRegister.CashNo, nameof(cashRegister.CashNo));
        }
    }

    private static void ValidateCreateCashRegisterRequest(CreateCashRegisterRequest request)
    {
        ValidatePositive(request.BranchNo, nameof(request.BranchNo));
        ValidatePositive(request.CashNo, nameof(request.CashNo));

        if (request.Terminals.Count == 0)
        {
            throw new ArgumentException("At least one terminal is required.", nameof(request.Terminals));
        }
    }

    private static IReadOnlyCollection<NormalizedTerminalRequest> NormalizeTerminalRequests(
        IReadOnlyCollection<CreateCashRegisterTerminalRequest> terminals)
    {
        var normalized = terminals
            .Select(NormalizeTerminalRequest)
            .ToArray();

        var duplicateRows = normalized
            .GroupBy(
                item => $"{item.TerminalNo}\u001f{item.Bank}\u001f{item.TerminalId}\u001f{item.MerchantNo}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First().TerminalNo)
            .ToArray();

        if (duplicateRows.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate terminal bank rows were found: {string.Join(", ", duplicateRows)}");
        }

        return normalized;
    }

    private static NormalizedTerminalRequest NormalizeTerminalRequest(
        CreateCashRegisterTerminalRequest terminal) =>
        new(
            NormalizeText(terminal.TerminalNo, 40, nameof(terminal.TerminalNo)),
            NormalizeText(terminal.Bank, 100, nameof(terminal.Bank)),
            NormalizeText(terminal.TerminalId, 40, nameof(terminal.TerminalId)),
            NormalizeText(terminal.MerchantNo, 40, nameof(terminal.MerchantNo)));

    private async Task EnsureCashRegisterExistsAsync(
        int branchNo,
        int cashNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidatePositive(cashNo, nameof(cashNo));

        var exists = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .AnyAsync(
                item => item.BranchNo == branchNo && item.CashRegisterNo == cashNo,
                cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException("Cash register was not found for this branch.");
        }
    }

    private async Task ValidateMikroTerminalScopeAsync(
        int branchNo,
        int cashNo,
        NormalizedTerminalRequest terminal,
        int? excludedTerminalId,
        CancellationToken cancellationToken)
    {
        var usedByAnotherCashRegister = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.CashRegisterNo == terminal.TerminalNo &&
                    (!item.CashNo.HasValue || item.CashNo.Value != cashNo),
                cancellationToken);

        if (usedByAnotherCashRegister)
        {
            var locations = await ResolveTerminalLocationsAsync(
                terminal.TerminalNo,
                branchNo: null,
                cancellationToken);
            throw new InvalidOperationException(
                BuildTerminalLocationConflictMessage(terminal.TerminalNo, locations));
        }

        var duplicateExists = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .AnyAsync(
                item =>
                    (!excludedTerminalId.HasValue || item.Id != excludedTerminalId.Value) &&
                    item.CashNo == cashNo &&
                    item.CashRegisterNo == terminal.TerminalNo &&
                    item.Bank == terminal.Bank &&
                    item.TerminalId == terminal.TerminalId &&
                    item.MerchantNo == terminal.MerchantNo,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                $"{terminal.TerminalNo} icin ayni banka, terminal ID ve uye isyeri satiri bu kasada zaten var.");
        }

        var mappedToAnotherBranch = await mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .AnyAsync(
                item => item.CashRegisterNo == terminal.TerminalNo && item.BranchNo != branchNo,
                cancellationToken);

        if (mappedToAnotherBranch)
        {
            var locations = await ResolveTerminalLocationsAsync(
                terminal.TerminalNo,
                branchNo: null,
                cancellationToken);
            throw new InvalidOperationException(
                BuildTerminalLocationConflictMessage(terminal.TerminalNo, locations));
        }
    }

    private async Task<IReadOnlyCollection<CashRegisterTerminalLocationDto>> ResolveTerminalLocationsAsync(
        string terminalNo,
        int? branchNo,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashRegisterNo == terminalNo && item.CashNo.HasValue)
            .Select(item => new
            {
                CashNo = item.CashNo!.Value,
                Bank = item.Bank ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        var mappingsQuery = mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .Where(item => item.CashRegisterNo == terminalNo);
        if (branchNo.HasValue)
        {
            mappingsQuery = mappingsQuery.Where(item => item.BranchNo == branchNo.Value);
        }

        var branches = await mappingsQuery
            .Select(item => item.BranchNo)
            .Distinct()
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);

        return branches
            .SelectMany(currentBranchNo => rows
                .GroupBy(item => item.CashNo)
                .Select(group => new CashRegisterTerminalLocationDto(
                    terminalNo,
                    currentBranchNo,
                    group.Key,
                    group.Count(),
                    group
                        .Select(item => item.Bank)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(item => item)
                        .ToArray())))
            .OrderBy(item => item.BranchNo)
            .ThenBy(item => item.CashNo)
            .ToArray();
    }

    private static string BuildTerminalLocationConflictMessage(
        string terminalNo,
        IReadOnlyCollection<CashRegisterTerminalLocationDto> locations)
    {
        if (locations.Count == 0)
        {
            return $"{terminalNo} POS/PAV numarasi Mikro'da baska bir eslemeyle kullaniliyor.";
        }

        var locationText = string.Join(
            "; ",
            locations.Select(item =>
                $"{item.BranchNo} sube / {item.CashNo} kasa ({string.Join(", ", item.Banks)})"));
        return $"{terminalNo} POS/PAV numarasi zaten {locationText} altinda tanimli. Mevcut kaydi acip 'Dogru Kasaya Tasi' islemini kullanin.";
    }

    private static void ApplyTerminalValues(
        MikroCashRegisterDetailEntity entity,
        NormalizedTerminalRequest terminal)
    {
        entity.CashRegisterNo = terminal.TerminalNo;
        entity.Bank = terminal.Bank;
        entity.TerminalId = terminal.TerminalId;
        entity.MerchantNo = terminal.MerchantNo;
    }

    private static string NormalizeIpAddress(string value)
    {
        var normalized = NormalizeText(value, 100, nameof(value));
        if (!IPAddress.TryParse(normalized, out _))
        {
            throw new ArgumentException("IP address format is invalid.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeCashierName(string value) =>
        NormalizeText(value, 100, nameof(value)).ToUpper(TurkishCulture);

    private static int NormalizeTake(int value, int defaultValue, int maxValue)
    {
        if (value <= 0)
        {
            return defaultValue;
        }

        return Math.Min(value, maxValue);
    }

    private static string? NormalizeNullableText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalText(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return NormalizeNullableText(value);
    }

    private static string NormalizeText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not be longer than {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Value must be greater than zero.", parameterName);
        }
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentException("Value can not be negative.", parameterName);
        }
    }

    private static void ValidateScalesType(byte value, string parameterName)
    {
        if (!SettingsTypeCatalog.GetScalesTypeOptions().Any(item => item.Value == value))
        {
            throw new ArgumentException("Scales type must be 0 (CAS 16) or 1 (CAS 500).", parameterName);
        }
    }

    private static string ResolveScalesTypeName(byte value) =>
        SettingsTypeCatalog.ResolveScalesTypeOption(value).Name;

    private static string ResolveScalesTypeDescription(byte value) =>
        SettingsTypeCatalog.ResolveScalesTypeOption(value).Description;

    private static string ResolveCashTypeName(byte value) =>
        SettingsTypeCatalog.ResolveCashTypeOption(value).Name;

    private static string ResolveCashTypeDescription(byte value) =>
        SettingsTypeCatalog.ResolveCashTypeOption(value).Description;


    private static IReadOnlyCollection<SettingsTypeOptionDto> MergeTypeOptions(
        IReadOnlyCollection<SettingsTypeOptionDto> defaultOptions,
        IEnumerable<byte> configuredValues,
        Func<byte, SettingsTypeOptionDto> resolveOption) =>
        defaultOptions
            .Select(item => item.Value)
            .Concat(configuredValues)
            .Distinct()
            .OrderBy(item => item)
            .Select(resolveOption)
            .ToArray();

    private static string? ResolveMessageStateName(int? state) =>
        state switch
        {
            0 => "1071 bulundu",
            1 => "1071 bulunmadi",
            _ => null
        };

    private static string GenerateNumericPassword()
    {
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(chars);
    }

    private async Task<T> ExecuteFurpaTransactionAsync<T>(
        IsolationLevel isolationLevel,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var executionStrategy = furpaDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            furpaDbContext.ChangeTracker.Clear();
            await using var transaction = await furpaDbContext.Database.BeginTransactionAsync(
                isolationLevel,
                cancellationToken);

            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task SynchronizeCashierDefinitionAsync(
        CashierEntity cashier,
        CancellationToken cancellationToken)
    {
        var definition = await furpaDbContext.CashierDefinitions
            .FirstOrDefaultAsync(item => item.CashierId == cashier.CashierId, cancellationToken);
        var (firstName, lastName) = SplitCashierName(cashier.CashierName);

        if (definition is null)
        {
            definition = new CashierDefinitionEntity
            {
                CashierId = cashier.CashierId,
                Address = string.Empty,
                Phone = string.Empty
            };
            await furpaDbContext.CashierDefinitions.AddAsync(definition, cancellationToken);
        }

        definition.CreatedByUser = cashier.CreateUser.ToString(CultureInfo.InvariantCulture);
        definition.CreatedAt = cashier.CreateDate;
        definition.UpdatedByUser = cashier.UpdateUser.ToString(CultureInfo.InvariantCulture);
        definition.UpdatedAt = cashier.UpdateDate;
        definition.CashierCode = cashier.CashierCode.ToString(CultureInfo.InvariantCulture);
        definition.FirstName = firstName;
        definition.LastName = lastName;
        definition.Password = cashier.CashierPassword;
        definition.Authorization = cashier.CashierAuthorization;
        definition.IsActive = cashier.CashierState;
    }

    private static (string FirstName, string LastName) SplitCashierName(string cashierName)
    {
        var normalizedName = cashierName.Trim();
        var lastSpaceIndex = normalizedName.LastIndexOf(' ');

        return lastSpaceIndex > 0
            ? (normalizedName[..lastSpaceIndex].Trim(), normalizedName[(lastSpaceIndex + 1)..].Trim())
            : (normalizedName, string.Empty);
    }

    private sealed record DeviceStatusSource(
        int BranchNo,
        int DeviceTypeId,
        string DeviceTypeName,
        string IpAddress,
        string Description);

    private sealed record NormalizedTerminalRequest(
        string TerminalNo,
        string Bank,
        string TerminalId,
        string MerchantNo);
}
