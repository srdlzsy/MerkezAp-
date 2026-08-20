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

        await using var transaction = await furpaDbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);

        return ToBranchDto(entity);
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
        var terminalNos = terminalRequests.Select(item => item.TerminalNo).ToArray();

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

        var existingTerminals = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => terminalNos.Contains(item.CashRegisterNo))
            .Select(item => item.CashRegisterNo)
            .ToArrayAsync(cancellationToken);

        if (existingTerminals.Length > 0)
        {
            throw new InvalidOperationException(
                $"Terminal already exists: {string.Join(", ", existingTerminals.OrderBy(item => item))}");
        }

        var existingBranchMappings = await mikroWriteDbContext.CashRegisterBranches
            .AsNoTracking()
            .Where(item => terminalNos.Contains(item.CashRegisterNo))
            .Select(item => item.CashRegisterNo)
            .ToArrayAsync(cancellationToken);

        if (existingBranchMappings.Length > 0)
        {
            throw new InvalidOperationException(
                $"Terminal branch mapping already exists: {string.Join(", ", existingBranchMappings.OrderBy(item => item))}");
        }

        await using var furpaTransaction = await furpaDbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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
            .Select(item => new CashRegisterBranchEntity
            {
                CashRegisterNo = item.TerminalNo,
                BranchNo = request.BranchNo
            })
            .ToArray();

        await mikroWriteDbContext.CashRegisterDetails.AddRangeAsync(terminalEntities, cancellationToken);
        await mikroWriteDbContext.CashRegisterBranches.AddRangeAsync(branchEntities, cancellationToken);

        await furpaDbContext.SaveChangesAsync(cancellationToken);
        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
        await furpaTransaction.CommitAsync(cancellationToken);

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
    }

    public async Task<IReadOnlyCollection<CashRegisterTerminalDto>> ListCashRegisterTerminalsAsync(
        int cashNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(cashNo, nameof(cashNo));

        var furpaRows = await furpaDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo == cashNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

        var selectedRows = furpaRows.Length > 0
            ? furpaRows
            : await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo == cashNo)
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

    public async Task DeleteCashRegisterAsync(
        int branchNo,
        int cashNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        ValidatePositive(cashNo, nameof(cashNo));

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

        var terminalNosMappedToOtherBranches = terminalMappings
            .Where(item => item.BranchNo != branchNo)
            .Select(item => item.CashRegisterNo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removableTerminalNos = terminalNos
            .Where(item => !terminalNosMappedToOtherBranches.Contains(item))
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

        await using var furpaTransaction = await furpaDbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        furpaDbContext.CashRegistryDetails.Remove(cashRegistry);
        mikroWriteDbContext.CashRegisterBranches.RemoveRange(terminalMappingsToRemove);
        mikroWriteDbContext.CashRegisterDetails.RemoveRange(terminalDetailsToRemove);

        await furpaDbContext.SaveChangesAsync(cancellationToken);
        await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
        await furpaTransaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteCashRegisterTerminalAsync(
        int branchNo,
        string terminalNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(branchNo, nameof(branchNo));
        var normalizedTerminalNo = NormalizeText(terminalNo, 40, nameof(terminalNo));

        var mapping = await mikroWriteDbContext.CashRegisterBranches
            .FirstOrDefaultAsync(
                item =>
                    item.BranchNo == branchNo &&
                    item.CashRegisterNo == normalizedTerminalNo,
                cancellationToken)
            ?? throw new KeyNotFoundException("Terminal branch mapping was not found.");

        var detail = await mikroWriteDbContext.CashRegisterDetails
            .FirstOrDefaultAsync(item => item.CashRegisterNo == normalizedTerminalNo, cancellationToken)
            ?? throw new KeyNotFoundException("Terminal detail was not found.");

        mikroWriteDbContext.CashRegisterBranches.Remove(mapping);
        mikroWriteDbContext.CashRegisterDetails.Remove(detail);
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

    public async Task<CashierPasswordMutationDto> CreateCashierAsync(
        CreateCashierRequest request,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken)
    {
        ValidateNonNegative(currentUserWarehouseNo, nameof(currentUserWarehouseNo));
        var cashierName = NormalizeCashierName(request.CashierName);
        var authorization = NormalizeText(request.CashierAuthorization, 100, nameof(request.CashierAuthorization));

        await using var transaction = await furpaDbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);

        var cashier = ToCashierDto(entity);
        return new CashierPasswordMutationDto(
            cashier.CashierCode,
            generatedPassword,
            cashier);
    }

    public async Task<CashierDto> UpdateCashierAsync(
        int cashierCode,
        UpdateCashierRequest request,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(cashierCode, nameof(cashierCode));
        ValidateNonNegative(currentUserWarehouseNo, nameof(currentUserWarehouseNo));

        var entity = await furpaDbContext.Cashiers
            .FirstOrDefaultAsync(item => item.CashierCode == cashierCode, cancellationToken)
            ?? throw new KeyNotFoundException("Cashier was not found.");

        entity.UpdateUser = currentUserWarehouseNo;
        entity.UpdateDate = DateTime.Now;
        entity.CashierName = NormalizeCashierName(request.CashierName);
        entity.CashierAuthorization = NormalizeText(request.CashierAuthorization, 100, nameof(request.CashierAuthorization));
        entity.CashierState = request.CashierState;

        await furpaDbContext.SaveChangesAsync(cancellationToken);
        return ToCashierDto(entity);
    }

    public async Task<CashierPasswordMutationDto> ResetCashierPasswordAsync(
        int cashierCode,
        int currentUserWarehouseNo,
        CancellationToken cancellationToken)
    {
        ValidatePositive(cashierCode, nameof(cashierCode));
        ValidateNonNegative(currentUserWarehouseNo, nameof(currentUserWarehouseNo));

        var entity = await furpaDbContext.Cashiers
            .FirstOrDefaultAsync(item => item.CashierCode == cashierCode, cancellationToken)
            ?? throw new KeyNotFoundException("Cashier was not found.");

        var generatedPassword = GenerateNumericPassword();
        entity.CashierPassword = generatedPassword;
        entity.UpdateUser = currentUserWarehouseNo;
        entity.UpdateDate = DateTime.Now;

        await furpaDbContext.SaveChangesAsync(cancellationToken);

        var cashier = ToCashierDto(entity);
        return new CashierPasswordMutationDto(
            cashier.CashierCode,
            generatedPassword,
            cashier);
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
        IReadOnlyCollection<int> cashNos,
        CancellationToken cancellationToken)
    {
        if (cashNos.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var furpaRows = await furpaDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo.HasValue && cashNos.Contains(item.CashNo.Value))
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                CashNo = item.CashNo!.Value,
                CashRegisterNo = item.CashRegisterNo ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        var result = furpaRows
            .Where(item => !string.IsNullOrWhiteSpace(item.CashRegisterNo))
            .GroupBy(item => item.CashNo)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.CashRegisterNo).First(),
                EqualityComparer<int>.Default);

        var missingCashNos = cashNos
            .Where(item => !result.ContainsKey(item))
            .ToArray();

        if (missingCashNos.Length == 0)
        {
            return result;
        }

        var mikroRows = await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo.HasValue && missingCashNos.Contains(item.CashNo.Value))
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                CashNo = item.CashNo!.Value,
                CashRegisterNo = item.CashRegisterNo ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        foreach (var group in mikroRows
                     .Where(item => !string.IsNullOrWhiteSpace(item.CashRegisterNo))
                     .GroupBy(item => item.CashNo))
        {
            result[group.Key] = group.Select(item => item.CashRegisterNo).First();
        }

        return result;
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
            .Select(item => new NormalizedTerminalRequest(
                NormalizeText(item.TerminalNo, 40, nameof(item.TerminalNo)),
                NormalizeText(item.Bank, 100, nameof(item.Bank)),
                NormalizeText(item.TerminalId, 40, nameof(item.TerminalId)),
                NormalizeText(item.MerchantNo, 40, nameof(item.MerchantNo))))
            .ToArray();

        var duplicateTerminalNos = normalized
            .GroupBy(item => item.TerminalNo, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateTerminalNos.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate terminal numbers were found: {string.Join(", ", duplicateTerminalNos)}");
        }

        return normalized;
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
