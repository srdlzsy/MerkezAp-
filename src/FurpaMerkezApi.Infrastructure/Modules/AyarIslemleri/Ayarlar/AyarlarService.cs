using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Ayarlar;

public sealed class AyarlarService(
    FurpaDbContext furpaDbContext,
    MikroWriteDbContext mikroWriteDbContext)
    : IAyarlarService
{
    private const int DevicePingTimeoutMilliseconds = 1000;
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly IReadOnlyCollection<SettingsTypeOptionDto> ScalesTypeOptions =
    [
        new(
            0,
            "cas-16",
            "CAS 16",
            "Terazi.plu formatinda CAS 16 terazi dosyasi uretir.",
            true),
        new(
            1,
            "cas-500",
            "CAS 500",
            "ART_STM.txt formatinda CAS 500 terazi dosyasi uretir.",
            true)
    ];
    private static readonly IReadOnlyCollection<SettingsTypeOptionDto> CashTypeOptions =
    [
        new(
            0,
            "cash-type-0",
            "Kasa Tipi 0",
            "Mevcut veri sozlugunde is kurali adi netlestirilmemis kasa tipi.",
            false),
        new(
            1,
            "cash-type-1",
            "Kasa Tipi 1",
            "Mevcut veri sozlugunde is kurali adi netlestirilmemis kasa tipi.",
            false)
    ];

    public async Task<BranchSettingsLookupsDto> GetBranchSettingsLookupsAsync(
        CancellationToken cancellationToken) =>
        new(
            await ListScalesTypeOptionsAsync(cancellationToken),
            await ListCashTypeOptionsAsync(cancellationToken));

    public async Task<CashRegisterSettingsLookupsDto> GetCashRegisterSettingsLookupsAsync(
        CancellationToken cancellationToken) =>
        new(await ListCashTypeOptionsAsync(cancellationToken));

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

        return cashRegisters
            .Select(ToCashRegistryDto)
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
            .Select(item => new CashRegisterDetailEntity
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

        return await mikroWriteDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo == cashNo)
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new CashRegisterTerminalDto(
                item.Id,
                item.CashRegisterNo,
                item.Bank,
                item.TerminalId,
                item.MerchantNo,
                item.CashNo))
            .ToArrayAsync(cancellationToken);
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
            .Where(item => removableTerminalNos.Contains(item.CashRegisterNo))
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

        return MergeTypeOptions(ScalesTypeOptions, configuredValues, ResolveScalesTypeOption);
    }

    private async Task<IReadOnlyCollection<SettingsTypeOptionDto>> ListCashTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        var configuredValues = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Select(item => item.CashRegisterType)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return MergeTypeOptions(CashTypeOptions, configuredValues, ResolveCashTypeOption);
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

    private static CashRegistryDto ToCashRegistryDto(CashRegistryDetailEntity item) =>
        new(
            item.DetailId,
            item.BranchNo,
            item.CashRegisterNo,
            item.CashRegisterType,
            ResolveCashTypeName(item.CashRegisterType),
            ResolveCashTypeDescription(item.CashRegisterType));

    private static CashRegisterTerminalDto ToTerminalDto(CashRegisterDetailEntity item) =>
        new(
            item.Id,
            item.CashRegisterNo,
            item.Bank,
            item.TerminalId,
            item.MerchantNo,
            item.CashNo);

    private static CashierDto ToCashierDto(CashierEntity item) =>
        new(
            item.CashierCode,
            item.CashierName,
            item.CashierAuthorization,
            item.CashierState);

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
        if (!ScalesTypeOptions.Any(item => item.Value == value))
        {
            throw new ArgumentException("Scales type must be 0 (CAS 16) or 1 (CAS 500).", parameterName);
        }
    }

    private static string ResolveScalesTypeName(byte value) =>
        ResolveScalesTypeOption(value).Name;

    private static string ResolveScalesTypeDescription(byte value) =>
        ResolveScalesTypeOption(value).Description;

    private static string ResolveCashTypeName(byte value) =>
        ResolveCashTypeOption(value).Name;

    private static string ResolveCashTypeDescription(byte value) =>
        ResolveCashTypeOption(value).Description;

    private static SettingsTypeOptionDto ResolveScalesTypeOption(byte value) =>
        ScalesTypeOptions.FirstOrDefault(item => item.Value == value)
        ?? new SettingsTypeOptionDto(
            value,
            $"scales-type-{value.ToString(CultureInfo.InvariantCulture)}",
            $"Terazi Tipi {value.ToString(CultureInfo.InvariantCulture)}",
            "Tanimlanmamis terazi tipi.",
            false);

    private static SettingsTypeOptionDto ResolveCashTypeOption(byte value) =>
        CashTypeOptions.FirstOrDefault(item => item.Value == value)
        ?? new SettingsTypeOptionDto(
            value,
            $"cash-type-{value.ToString(CultureInfo.InvariantCulture)}",
            $"Kasa Tipi {value.ToString(CultureInfo.InvariantCulture)}",
            "Tanimlanmamis kasa tipi.",
            false);

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
