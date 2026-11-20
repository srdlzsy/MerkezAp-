using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Interfaces;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.YeniKasaYonetimi.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.YeniKasaYonetimi;

#pragma warning disable CA1416 // System.Drawing calls are protected by explicit Windows 6.1+ runtime guards.
public sealed class YeniKasaYonetimiService : IYeniKasaYonetimiService
{
    private const string DefaultCustomerDisplayPath =
        @"\\10.0.0.55\kasa\Furpa Kasa Sync\musteri_gorselleri";
    private const string DefaultProductImagePath =
        @"\\10.0.0.55\kasa\Furpa Kasa Sync\urun_gorselleri";
    private const int MaxDisplayImageBytes = 20 * 1024 * 1024;
    private const int MaxDisplayVideoBytes = 50 * 1024 * 1024;
    private const int MaxProductImageBytes = 10 * 1024 * 1024;

    private readonly string _shopigoConnectionString;
    private readonly string? _mikroConnectionString;
    private readonly string _customerDisplayPath;
    private readonly string _productImagePath;
    private readonly int _ftpPort;
    private readonly string _ftpFolderPath;
    private readonly ILogger<YeniKasaYonetimiService> _logger;

    public YeniKasaYonetimiService(
        IConfiguration configuration,
        ILogger<YeniKasaYonetimiService> logger)
    {
        _logger = logger;
        _shopigoConnectionString = configuration.GetConnectionString("ShopigoCiroConnection")
            ?? configuration.GetConnectionString("ShopigoConnection")
            ?? string.Empty;
        _mikroConnectionString = configuration.GetConnectionString("MikroConnection");

        _customerDisplayPath = configuration["ShopigoPosSettings:CustomerDisplayPath"] ?? DefaultCustomerDisplayPath;
        _productImagePath = configuration["ShopigoPosSettings:ProductImagePath"] ?? DefaultProductImagePath;
        _ftpPort = configuration.GetValue<int?>("ShopigoPosSettings:FtpPort") is > 0 and <= 65535
            ? configuration.GetValue<int>("ShopigoPosSettings:FtpPort")
            : 21;
        _ftpFolderPath = configuration["ShopigoPosSettings:FtpFolderPath"] ?? "/kasa/";
    }

    private SqlConnection CreateShopigoConnection()
    {
        if (string.IsNullOrWhiteSpace(_shopigoConnectionString))
        {
            throw new InvalidOperationException(
                "ShopigoCiroConnection bağlantı dizesi yapılandırılmamış. Yeni Kasa Yönetimi işlemi başlatılamadı.");
        }

        return new SqlConnection(_shopigoConnectionString);
    }
    private SqlConnection? CreateMikroConnection() =>
        string.IsNullOrWhiteSpace(_mikroConnectionString) ? null : new(_mikroConnectionString);

    #region Genel Ayarlar & FTP Testi & Sync Push

    public async Task<YeniKasaAppSettingsDto?> GetAppSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT TOP 1
                id, pavo_fis, fis_genislik, printer_dots_per_line,
                fis_fatura_limit, nakit_limit,
                ftp_host, ftp_user, ftp_password,
                created_at, updated_at
            FROM dbo.app_settings
            ORDER BY id ASC;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new YeniKasaAppSettingsDto(
            Id: reader.GetInt32(0),
            PavoFis: reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
            FisGenislik: reader.IsDBNull(2) ? null : reader.GetString(2),
            PrinterDotsPerLine: reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
            FisFaturaLimit: reader.IsDBNull(4) ? null : reader.GetDecimal(4),
            NakitLimit: reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            FtpHost: reader.IsDBNull(6) ? null : reader.GetString(6),
            FtpPort: _ftpPort,
            FtpUsername: reader.IsDBNull(7) ? null : reader.GetString(7),
            FtpPassword: null,
            FtpFolderPath: _ftpFolderPath,
            CreatedAt: reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            UpdatedAt: reader.IsDBNull(10) ? null : reader.GetDateTime(10));
    }

    public async Task UpdateAppSettingsAsync(UpdateYeniKasaAppSettingsRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            IF EXISTS (SELECT 1 FROM dbo.app_settings)
            BEGIN
                UPDATE dbo.app_settings
                SET pavo_fis = @PavoFis,
                    fis_genislik = @FisGenislik,
                    printer_dots_per_line = @PrinterDotsPerLine,
                    fis_fatura_limit = @FisFaturaLimit,
                    nakit_limit = @NakitLimit,
                    ftp_host = @FtpHost,
                    ftp_user = @FtpUser,
                    ftp_password = COALESCE(NULLIF(@FtpPassword, ''), ftp_password),
                    updated_at = GETDATE()
                WHERE id = (SELECT TOP 1 id FROM dbo.app_settings ORDER BY id ASC);
            END
            ELSE
            BEGIN
                INSERT INTO dbo.app_settings (
                    pavo_fis, fis_genislik, printer_dots_per_line,
                    fis_fatura_limit, nakit_limit,
                    ftp_host, ftp_user, ftp_password,
                    created_at, updated_at
                ) VALUES (
                    @PavoFis, @FisGenislik, @PrinterDotsPerLine,
                    @FisFaturaLimit, @NakitLimit,
                    @FtpHost, @FtpUser, @FtpPassword,
                    GETDATE(), GETDATE()
                );
            END";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@PavoFis", request.PavoFis);
        cmd.Parameters.AddWithValue("@FisGenislik", (object?)request.FisGenislik ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PrinterDotsPerLine", (object?)request.PrinterDotsPerLine ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FisFaturaLimit", (object?)request.FisFaturaLimit ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NakitLimit", (object?)request.NakitLimit ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FtpHost", (object?)request.FtpHost ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FtpUser", (object?)request.FtpUsername ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FtpPassword", (object?)request.FtpPassword?.Trim() ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TestFtpConnectionResultDto> TestFtpConnectionAsync(TestFtpConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FtpHost))
        {
            return new TestFtpConnectionResultDto(false, 0, "FTP Sunucu adresi (Host) boş bırakılamaz.");
        }

        var rawHost = request.FtpHost.Trim();
        var port = request.FtpPort is > 0 and <= 65535 ? request.FtpPort.Value : 21;

        var host = rawHost;
        if (rawHost.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(rawHost, UriKind.Absolute, out var uri))
            {
                host = uri.Host;
                if (!uri.IsDefaultPort && uri.Port > 0 && (request.FtpPort == null || request.FtpPort <= 0))
                {
                    port = uri.Port;
                }
            }
        }
        else if (rawHost.Contains('/'))
        {
            host = rawHost.Split('/')[0];
        }

        if (host.Contains(':') && !host.Contains('['))
        {
            var parts = host.Split(':');
            host = parts[0];
            if (int.TryParse(parts[1], out var parsedPort) && (request.FtpPort == null || request.FtpPort <= 0))
            {
                port = parsedPort;
            }
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
            var timeoutTask = Task.Delay(4000, cancellationToken);

            var completed = await Task.WhenAny(connectTask, timeoutTask);
            sw.Stop();

            if (completed == timeoutTask)
            {
                return new TestFtpConnectionResultDto(false, sw.ElapsedMilliseconds, $"{host}:{port} adresine bağlantı zaman aşımına uğradı (4 sn).");
            }

            await connectTask;

            if (client.Connected)
            {
                return new TestFtpConnectionResultDto(
                    true,
                    sw.ElapsedMilliseconds,
                    $"{host}:{port} FTP sunucu bağlantısı başarılı ({sw.ElapsedMilliseconds} ms). Port erişilebilir durumda.");
            }

            return new TestFtpConnectionResultDto(false, sw.ElapsedMilliseconds, "Bağlantı kurulamadı.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "FTP bağlantı testi başarısız: {Host}:{Port}", host, port);
            return new TestFtpConnectionResultDto(false, sw.ElapsedMilliseconds, $"FTP bağlantı hatası: {ex.Message}");
        }
    }

    public async Task<SyncPushResultDto> TriggerSyncPushAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        try
        {
            await using var connection = CreateShopigoConnection();
            await connection.OpenAsync(cancellationToken);

            const string syncSql = @"
                IF EXISTS (SELECT 1 FROM dbo.sync_status WHERE type = 'pos_settings')
                BEGIN
                    UPDATE dbo.sync_status
                    SET last_sync = GETDATE(), updated_at = GETDATE()
                    WHERE type = 'pos_settings';
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.sync_status (type, last_sync, is_initial_sync_done, created_at, updated_at)
                    VALUES ('pos_settings', GETDATE(), 1, GETDATE(), GETDATE());
                END

                IF EXISTS (SELECT 1 FROM dbo.sync_status WHERE type = 'media_sync')
                BEGIN
                    UPDATE dbo.sync_status
                    SET last_sync = GETDATE(), updated_at = GETDATE()
                    WHERE type = 'media_sync';
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.sync_status (type, last_sync, is_initial_sync_done, created_at, updated_at)
                    VALUES ('media_sync', GETDATE(), 1, GETDATE(), GETDATE());
                END";

            await using var cmd = new SqlCommand(syncSql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            return new SyncPushResultDto(true, "Tüm kasalara anlık canlı dağıtım sinyali başarıyla iletildi.", nowUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kasalara senkronizasyon tetiklenirken hata oluştu.");
            return new SyncPushResultDto(false, $"Dağıtım sinyali gönderilemedi: {ex.Message}", nowUtc);
        }
    }

    #endregion

    #region Ödeme Yöntemleri (payment_methods)

    public async Task<IReadOnlyList<YeniKasaPaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, name, notes, status, sirano, pavo_is_active, pavo_type, pavo_mediator, Brand, [group]
            FROM dbo.payment_methods
            ORDER BY sirano ASC, id ASC;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var list = new List<YeniKasaPaymentMethodDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new YeniKasaPaymentMethodDto(
                Id: reader.GetInt32(0),
                Name: reader.IsDBNull(1) ? "" : reader.GetString(1),
                Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                Status: reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                Sirano: reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                PavoIsActive: reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                PavoType: reader.IsDBNull(6) ? null : reader.GetValue(6)?.ToString(),
                PavoMediator: reader.IsDBNull(7) ? null : reader.GetValue(7)?.ToString(),
                Brand: reader.IsDBNull(8) ? null : reader.GetValue(8)?.ToString(),
                Group: reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return list;
    }

    public async Task UpdatePaymentMethodAsync(UpdateYeniKasaPaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id <= 0)
            throw new ArgumentException("Geçerli bir ödeme yöntemi seçilmelidir.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Ödeme yöntemi adı boş olamaz.", nameof(request));
        if (request.Status is not (0 or 1) || request.PavoIsActive is not (0 or 1))
            throw new ArgumentException("Durum alanları yalnızca 0 veya 1 olabilir.", nameof(request));
        if (!int.TryParse(request.PavoType, out var pavoTypeVal))
            throw new ArgumentException("Pavo tipi sayısal ve zorunlu olmalıdır.", nameof(request));
        if (!int.TryParse(request.PavoMediator, out var pavoMediatorVal))
            throw new ArgumentException("Pavo aracı sayısal ve zorunlu olmalıdır.", nameof(request));

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        int? brandVal = int.TryParse(request.Brand, out var br) ? br : null;

        const string sql = @"
            UPDATE dbo.payment_methods
            SET name = @Name,
                notes = @Notes,
                status = @Status,
                sirano = @Sirano,
                pavo_is_active = @PavoIsActive,
                pavo_type = @PavoType,
                pavo_mediator = @PavoMediator,
                Brand = @Brand,
                [group] = @Group
            WHERE id = @Id;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", request.Id);
        cmd.Parameters.AddWithValue("@Name", request.Name);
        cmd.Parameters.AddWithValue("@Notes", (object?)request.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", request.Status);
        cmd.Parameters.AddWithValue("@Sirano", request.Sirano);
        cmd.Parameters.AddWithValue("@PavoIsActive", request.PavoIsActive);
        cmd.Parameters.AddWithValue("@PavoType", pavoTypeVal);
        cmd.Parameters.AddWithValue("@PavoMediator", pavoMediatorVal);
        cmd.Parameters.AddWithValue("@Brand", (object?)brandVal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Group", (object?)request.Group ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdatePaymentMethodsOrderAsync(UpdatePaymentMethodsOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0) return;

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            const string sql = @"
                UPDATE dbo.payment_methods
                SET sirano = @Sirano,
                    status = @Status
                WHERE id = @Id;";

            foreach (var item in request.Items)
            {
                await using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Id", item.Id);
                cmd.Parameters.AddWithValue("@Sirano", item.Sirano);
                cmd.Parameters.AddWithValue("@Status", item.Status);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<YeniKasaPaymentMethodLookupsDto> GetPaymentMethodLookupsAsync(CancellationToken cancellationToken = default)
    {
        var defaultGroups = new List<string> { "ana", "banka", "yemek", "online", "diger" };
        var brandLookupMap = new Dictionary<string, string>
        {
            ["0"] = "0 - Standart / Tanımsız",
            ["6"] = "6 - Multinet",
            ["7"] = "7 - Metropol",
            ["8"] = "8 - Pluxee",
            ["9"] = "9 - TicketRestaurant",
            ["10"] = "10 - Setcard",
            ["11"] = "11 - TokenFlex",
            ["12"] = "12 - Paye",
            ["200"] = "200 - Yemek Sepeti Online",
            ["201"] = "201 - Getir Online",
            ["202"] = "202 - Trendyol Online",
            ["203"] = "203 - Migros Yemek Online",
            ["204"] = "204 - Web Sitesi Online"
        };

        try
        {
            await using var connection = CreateShopigoConnection();
            await connection.OpenAsync(cancellationToken);

            const string groupSql = "SELECT DISTINCT [group] FROM dbo.payment_methods WHERE [group] IS NOT NULL AND LTRIM(RTRIM([group])) <> '';";
            await using (var gCmd = new SqlCommand(groupSql, connection))
            await using (var reader = await gCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var g = reader.GetString(0).Trim().ToLowerInvariant();
                    if (!defaultGroups.Contains(g))
                    {
                        defaultGroups.Add(g);
                    }
                }
            }

            const string brandSql = "SELECT DISTINCT Brand FROM dbo.payment_methods WHERE Brand IS NOT NULL;";
            await using (var bCmd = new SqlCommand(brandSql, connection))
            await using (var reader = await bCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var bVal = reader.GetValue(0)?.ToString();
                    if (!string.IsNullOrWhiteSpace(bVal) && !brandLookupMap.ContainsKey(bVal))
                    {
                        brandLookupMap[bVal] = $"{bVal} - Tanımlı Marka {bVal}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ödeme yöntemleri lookup verileri sorgulanırken hata oluştu.");
        }

        var brandItems = brandLookupMap
            .OrderBy(kvp => int.TryParse(kvp.Key, out var n) ? n : 9999)
            .Select(kvp => new YeniKasaPaymentLookupItemDto(kvp.Key, kvp.Value))
            .ToList();

        return new YeniKasaPaymentMethodLookupsDto(defaultGroups, brandItems);
    }

    #endregion

    #region Kasa Yetki Matrisi (roles, permissions, role_permissions) & Rol Kopyalama

    public async Task<YeniKasaRolePermissionMatrixDto> GetRolePermissionMatrixAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        var roles = new List<YeniKasaRoleDto>();
        const string rolesSql = "SELECT id, name, NULL FROM dbo.roles WHERE deleted_at IS NULL ORDER BY id ASC;";
        await using (var cmd = new SqlCommand(rolesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(new YeniKasaRoleDto(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    null));
            }
        }

        var permissions = new List<YeniKasaPermissionDto>();
        const string permissionsSql = "SELECT id, name, code, description FROM dbo.permissions WHERE deleted_at IS NULL ORDER BY id ASC;";
        await using (var cmd = new SqlCommand(permissionsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new YeniKasaPermissionDto(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        var rolePermissions = new List<YeniKasaRolePermissionDto>();
        const string rpSql = @"
            SELECT DISTINCT r.id AS role_id, p.id AS permission_id
            FROM dbo.role_permissions rp
            JOIN dbo.roles r ON r.uuid = rp.role_id OR CAST(r.id AS nvarchar(50)) = rp.role_id
            JOIN dbo.permissions p ON p.code = rp.permission_code OR p.uuid = rp.permission_code OR CAST(p.id AS nvarchar(50)) = rp.permission_code
            WHERE rp.deleted_at IS NULL;";

        await using (var cmd = new SqlCommand(rpSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rolePermissions.Add(new YeniKasaRolePermissionDto(
                    reader.GetInt32(0),
                    reader.GetInt32(1)));
            }
        }

        return new YeniKasaRolePermissionMatrixDto(roles, permissions, rolePermissions);
    }

    public async Task UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Rolün uuid'sini bul
            string? roleUuid = null;
            const string getRoleUuidSql = "SELECT uuid FROM dbo.roles WHERE id = @RoleId;";
            await using (var cmd = new SqlCommand(getRoleUuidSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
                var res = await cmd.ExecuteScalarAsync(cancellationToken);
                roleUuid = res?.ToString();
            }

            if (string.IsNullOrWhiteSpace(roleUuid))
                throw new InvalidOperationException($"Rol bulunamadı: {request.RoleId}");

            var requestedPermissionIds = request.PermissionIds?
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            var permissionCodes = new List<string>();

            if (requestedPermissionIds.Length > 0)
            {
                var paramNames = requestedPermissionIds.Select((_, idx) => $"@pid{idx}").ToList();
                var getPermsSql = $"SELECT id, code, uuid FROM dbo.permissions WHERE id IN ({string.Join(",", paramNames)});";

                await using var cmd = new SqlCommand(getPermsSql, connection, transaction);
                for (var i = 0; i < requestedPermissionIds.Length; i++)
                {
                    cmd.Parameters.AddWithValue(paramNames[i], requestedPermissionIds[i]);
                }

                var foundIds = new HashSet<int>();
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    foundIds.Add(reader.GetInt32(0));
                    permissionCodes.Add(reader.IsDBNull(1) ? reader.GetString(2) : reader.GetString(1));
                }

                var missingIds = requestedPermissionIds.Except(foundIds).ToArray();
                if (missingIds.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Yetki kaydı bulunamadı: {string.Join(", ", missingIds)}");
                }
            }

            // 2. Rolün mevcut izinlerini sil
            const string deleteSql = "DELETE FROM dbo.role_permissions WHERE role_id = @RoleUuid OR role_id = CAST(@RoleId AS nvarchar(50));";
            await using (var cmd = new SqlCommand(deleteSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@RoleUuid", roleUuid);
                cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 3. Doğrulanmış izinleri ekle
            if (permissionCodes.Count > 0)
            {
                const string insertSql = @"
                    INSERT INTO dbo.role_permissions (role_id, permission_code)
                    VALUES (@RoleUuid, @PermissionCode);";

                foreach (var code in permissionCodes)
                {
                    await using var cmd = new SqlCommand(insertSql, connection, transaction);
                    cmd.Parameters.AddWithValue("@RoleUuid", roleUuid);
                    cmd.Parameters.AddWithValue("@PermissionCode", code);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task BulkUpdateRolePermissionsAsync(BulkUpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Roles == null || request.Roles.Count == 0)
            return;

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            var duplicateRoleIds = request.Roles
                .GroupBy(item => item.RoleId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateRoleIds.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Aynı rol toplu yetki isteğinde birden fazla kez gönderilemez: {string.Join(", ", duplicateRoleIds)}");
            }

            // Tüm rollerin id -> uuid eşlemesini çek
            var roleMap = new Dictionary<int, string>();
            const string getRolesSql = "SELECT id, uuid FROM dbo.roles;";
            await using (var cmd = new SqlCommand(getRolesSql, connection, transaction))
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = reader.GetInt32(0);
                    var uuid = reader.IsDBNull(1) ? id.ToString() : reader.GetString(1);
                    roleMap[id] = uuid;
                }
            }

            // Tüm permission id -> code eşlemesini çek
            var permMap = new Dictionary<int, string>();
            const string getPermsSql = "SELECT id, code, uuid FROM dbo.permissions;";
            await using (var cmd = new SqlCommand(getPermsSql, connection, transaction))
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = reader.GetInt32(0);
                    var code = !reader.IsDBNull(1) ? reader.GetString(1) : (reader.IsDBNull(2) ? id.ToString() : reader.GetString(2));
                    permMap[id] = code;
                }
            }

            const string deleteSql = "DELETE FROM dbo.role_permissions WHERE role_id = @RoleUuid OR role_id = CAST(@RoleId AS nvarchar(50));";
            const string insertSql = "INSERT INTO dbo.role_permissions (role_id, permission_code) VALUES (@RoleUuid, @PermissionCode);";

            var requestedRoleIds = request.Roles.Select(item => item.RoleId).Distinct().ToArray();
            var missingRoleIds = requestedRoleIds.Except(roleMap.Keys).ToArray();
            if (missingRoleIds.Length > 0)
            {
                throw new InvalidOperationException($"Rol bulunamadı: {string.Join(", ", missingRoleIds)}");
            }

            var requestedPermissionIds = request.Roles
                .SelectMany(item => item.PermissionIds ?? Array.Empty<int>())
                .Distinct()
                .ToArray();
            var missingPermissionIds = requestedPermissionIds.Except(permMap.Keys).ToArray();
            if (missingPermissionIds.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Yetki kaydı bulunamadı: {string.Join(", ", missingPermissionIds)}");
            }

            foreach (var roleItem in request.Roles)
            {
                var roleUuid = roleMap[roleItem.RoleId];

                // 1. Rolün mevcut izinlerini sil
                await using (var cmd = new SqlCommand(deleteSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@RoleUuid", roleUuid);
                    cmd.Parameters.AddWithValue("@RoleId", roleItem.RoleId);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 2. Yeni izinleri ekle
                if (roleItem.PermissionIds != null && roleItem.PermissionIds.Count > 0)
                {
                    foreach (var permId in roleItem.PermissionIds.Distinct())
                    {
                        var permCode = permMap[permId];
                        await using var cmd = new SqlCommand(insertSql, connection, transaction);
                        cmd.Parameters.AddWithValue("@RoleUuid", roleUuid);
                        cmd.Parameters.AddWithValue("@PermissionCode", permCode);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CopyRolePermissionsResultDto> CopyRolePermissionsAsync(CopyRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SourceRoleId == request.TargetRoleId)
        {
            return new CopyRolePermissionsResultDto(false, 0, "Kaynak ve hedef rol aynı olamaz.");
        }

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Kaynak ve hedef rollerin uuid'lerini bul
            string? sourceUuid = null, targetUuid = null;
            const string getRolesSql = "SELECT id, uuid FROM dbo.roles WHERE id IN (@SourceId, @TargetId);";
            await using (var cmd = new SqlCommand(getRolesSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@SourceId", request.SourceRoleId);
                cmd.Parameters.AddWithValue("@TargetId", request.TargetRoleId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var id = reader.GetInt32(0);
                    var uuid = reader.GetString(1);
                    if (id == request.SourceRoleId) sourceUuid = uuid;
                    if (id == request.TargetRoleId) targetUuid = uuid;
                }
            }

            if (string.IsNullOrWhiteSpace(sourceUuid) || string.IsNullOrWhiteSpace(targetUuid))
                return new CopyRolePermissionsResultDto(false, 0, "Kaynak veya hedef rol bulunamadı.");

            // 2. Kaynak rolün permission_code'larını oku
            var sourcePermCodes = new List<string>();
            const string readSql = "SELECT permission_code FROM dbo.role_permissions WHERE (role_id = @SourceUuid OR role_id = CAST(@SourceId AS nvarchar(50))) AND deleted_at IS NULL;";
            await using (var cmd = new SqlCommand(readSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@SourceUuid", sourceUuid);
                cmd.Parameters.AddWithValue("@SourceId", request.SourceRoleId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    sourcePermCodes.Add(reader.GetString(0));
                }
            }

            // 3. Hedef rolün izinlerini sil
            const string deleteSql = "DELETE FROM dbo.role_permissions WHERE role_id = @TargetUuid OR role_id = CAST(@TargetId AS nvarchar(50));";
            await using (var cmd = new SqlCommand(deleteSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@TargetUuid", targetUuid);
                cmd.Parameters.AddWithValue("@TargetId", request.TargetRoleId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 4. Hedef role ekle
            if (sourcePermCodes.Count > 0)
            {
                const string insertSql = @"
                    INSERT INTO dbo.role_permissions (role_id, permission_code)
                    VALUES (@TargetUuid, @PermissionCode);";

                foreach (var code in sourcePermCodes)
                {
                    await using var cmd = new SqlCommand(insertSql, connection, transaction);
                    cmd.Parameters.AddWithValue("@TargetUuid", targetUuid);
                    cmd.Parameters.AddWithValue("@PermissionCode", code);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new CopyRolePermissionsResultDto(true, sourcePermCodes.Count, $"{sourcePermCodes.Count} adet yetki başarıyla kopyalandı.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Rol yetkileri kopyalanırken hata.");
            return new CopyRolePermissionsResultDto(false, 0, $"Kopyalama hatası: {ex.Message}");
        }
    }

    #endregion

    #region Müşteri Ekranı (display_settings & display_content) & Sıralama

    public async Task<IReadOnlyList<YeniKasaDisplaySettingsDto>> GetDisplaySettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, page_type, is_two_slider, created_at, updated_at
            FROM dbo.display_settings
            ORDER BY id ASC;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var list = new List<YeniKasaDisplaySettingsDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new YeniKasaDisplaySettingsDto(
                Id: reader.GetInt32(0),
                PageType: reader.GetString(1),
                IsTwoSlider: Convert.ToInt32(reader.GetValue(2)),
                CreatedAt: reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                UpdatedAt: reader.IsDBNull(4) ? null : reader.GetDateTime(4)));
        }

        return list;
    }

    public async Task UpdateDisplaySettingsAsync(UpdateYeniKasaDisplaySettingsRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.display_settings
            SET is_two_slider = @IsTwoSlider,
                updated_at = GETDATE()
            WHERE id = @Id;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", request.Id);
        cmd.Parameters.AddWithValue("@IsTwoSlider", request.IsTwoSlider);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<YeniKasaDisplayContentDto>> GetDisplayContentsAsync(string? pageType = null, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT id, type, name, sirano, start_date, end_date, page_type, created_at, updated_at
            FROM dbo.display_content";

        if (!string.IsNullOrWhiteSpace(pageType))
        {
            sql += " WHERE page_type = @PageType";
        }
        sql += " ORDER BY sirano ASC, id DESC;";

        await using var cmd = new SqlCommand(sql, connection);
        if (!string.IsNullOrWhiteSpace(pageType))
        {
            cmd.Parameters.AddWithValue("@PageType", pageType);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var list = new List<YeniKasaDisplayContentDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var fileName = reader.IsDBNull(2) ? "" : reader.GetString(2);
            list.Add(new YeniKasaDisplayContentDto(
                Id: reader.GetInt32(0),
                Type: reader.IsDBNull(1) ? null : reader.GetString(1),
                Name: fileName,
                Sirano: reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                StartDate: reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                EndDate: reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                PageType: reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt: reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                UpdatedAt: reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                ImageUrl: string.IsNullOrWhiteSpace(fileName) ? null : $"/api/ayar-islemleri/yeni-kasa-yonetimi/musteri-gorsel/{fileName}"));
        }

        return list;
    }

    public async Task<YeniKasaDisplayContentDto> CreateDisplayContentAsync(CreateYeniKasaDisplayContentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Base64Data))
        {
            throw new ArgumentException("Müşteri ekranı içeriği için dosya seçilmelidir.", nameof(request));
        }

        var startDate = request.StartDate?.Date ?? DateTime.Today;
        var endDate = request.EndDate ?? new DateTime(2099, 12, 31, 23, 59, 59);
        if (endDate < startDate)
        {
            throw new ArgumentException("Bitiş tarihi başlangıç tarihinden önce olamaz.", nameof(request));
        }

        var isVideo = string.Equals(request.Type, "video", StringComparison.OrdinalIgnoreCase)
            || (request.Name != null && (request.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || request.Name.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)));

        var finalType = isVideo ? "video" : "image";
        var requestedExtension = Path.GetExtension(request.Name ?? string.Empty).ToLowerInvariant();
        var extension = isVideo && requestedExtension == ".webm" ? ".webm" : isVideo ? ".mp4" : ".jpg";
        var safeStem = SanitizeFileStem(Path.GetFileNameWithoutExtension(request.Name ?? string.Empty), "icerik");
        var rawFileName = $"{safeStem}-{Guid.NewGuid():N}{extension}";

        await SaveCustomerDisplayContentAsync(request.Base64Data, rawFileName, finalType, cancellationToken);

        try
        {
            await using var connection = CreateShopigoConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO dbo.display_content (type, name, sirano, start_date, end_date, page_type, created_at, updated_at)
                OUTPUT INSERTED.id, INSERTED.created_at, INSERTED.updated_at
                VALUES (@Type, @Name, @Sirano, @StartDate, @EndDate, @PageType, GETDATE(), GETDATE());";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Type", finalType);
            cmd.Parameters.AddWithValue("@Name", rawFileName);
            cmd.Parameters.AddWithValue("@Sirano", request.Sirano);
            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);
            cmd.Parameters.AddWithValue("@PageType", (object?)request.PageType ?? "sale_page");

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Müşteri ekranı kaydı eklenemedi.");

            return new YeniKasaDisplayContentDto(
                Id: reader.GetInt32(0),
                Type: finalType,
                Name: rawFileName,
                Sirano: request.Sirano,
                StartDate: startDate,
                EndDate: endDate,
                PageType: request.PageType ?? "sale_page",
                CreatedAt: reader.GetDateTime(1),
                UpdatedAt: reader.GetDateTime(2),
                ImageUrl: $"/api/ayar-islemleri/yeni-kasa-yonetimi/musteri-gorsel/{rawFileName}");
        }
        catch
        {
            TryDeleteCustomerDisplayFile(rawFileName);
            throw;
        }
    }

    public async Task UpdateDisplayContentsOrderAsync(UpdateDisplayContentsOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0) return;

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            const string sql = @"
                UPDATE dbo.display_content
                SET sirano = @Sirano,
                    updated_at = GETDATE()
                WHERE id = @Id;";

            foreach (var item in request.Items)
            {
                await using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Id", item.Id);
                cmd.Parameters.AddWithValue("@Sirano", item.Sirano);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteDisplayContentAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        string? fileName = null;
        const string selectSql = "SELECT name FROM dbo.display_content WHERE id = @Id;";
        await using (var cmd = new SqlCommand(selectSql, connection))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                fileName = result.ToString();
            }
        }

        const string deleteSql = "DELETE FROM dbo.display_content WHERE id = @Id;";
        await using (var cmd = new SqlCommand(deleteSql, connection))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            try
            {
                var filePath = Path.Combine(_customerDisplayPath, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Müşteri ekranı görsel dosyası silinirken hata: {FileName}", fileName);
            }
        }
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetDisplayContentImageAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var extensions = new[] { "", ".jpg", ".jpeg", ".png", ".mp4", ".webm" };

        string? foundPath = null;
        string contentType = "image/jpeg";

        foreach (var ext in extensions)
        {
            var testName = ext == "" ? safeFileName : $"{Path.GetFileNameWithoutExtension(safeFileName)}{ext}";
            var testPath = Path.Combine(_customerDisplayPath, testName);
            if (File.Exists(testPath))
            {
                foundPath = testPath;
                var foundExt = Path.GetExtension(testPath).ToLowerInvariant();
                contentType = foundExt switch
                {
                    ".png" => "image/png",
                    ".mp4" => "video/mp4",
                    ".webm" => "video/webm",
                    _ => "image/jpeg"
                };
                break;
            }
        }

        if (foundPath == null)
            return null;

        var bytes = await File.ReadAllBytesAsync(foundPath, cancellationToken);
        return (bytes, contentType);
    }

    private async Task SaveCustomerDisplayContentAsync(string base64Data, string fileName, string type, CancellationToken cancellationToken)
    {
        var isVideo = string.Equals(type, "video", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);

        var fileBytes = DecodeBase64Payload(
            base64Data,
            isVideo ? MaxDisplayVideoBytes : MaxDisplayImageBytes,
            isVideo ? "Video" : "Görsel");

        if (isVideo)
        {
            if (!HasSupportedVideoSignature(fileBytes, Path.GetExtension(fileName)))
            {
                throw new ArgumentException("Dosya içeriği seçilen MP4/WebM video biçimiyle uyuşmuyor.", nameof(base64Data));
            }

            try
            {
                if (!Directory.Exists(_customerDisplayPath))
                {
                    Directory.CreateDirectory(_customerDisplayPath);
                }

                var destPath = Path.Combine(_customerDisplayPath, fileName);
                await File.WriteAllBytesAsync(destPath, fileBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri ekranı videosu ağ paylaşımına ({Path}) kaydedilemedi: {FileName}", _customerDisplayPath, fileName);
                throw new InvalidOperationException($"Müşteri ekranı videosu ağ paylaşımına ({_customerDisplayPath}\\{fileName}) yazılamadı: {ex.Message}", ex);
            }
        }
        else
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                throw new PlatformNotSupportedException(
                    "Yeni Kasa Yönetimi görsel işleme özelliği yalnızca Windows sunucuda desteklenir.");
            }
            // Görsel: 1920x1080 Full HD JPEG optimizasyonu (azami 1.5 MB)
            try
            {
                using var inStream = new MemoryStream(fileBytes);
                using var original = (Bitmap)System.Drawing.Image.FromStream(inStream);
                ValidateImageDimensions(original, "Müşteri ekranı görseli");

                using var resized = new Bitmap(1920, 1080);
                using (var g = Graphics.FromImage(resized))
                {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(original, 0, 0, 1920, 1080);
                }

                var jpegEncoder = GetEncoder(ImageFormat.Jpeg)
                    ?? throw new InvalidOperationException("JPEG encoder bulunamadı.");

                long quality = 85;
                byte[] finalBytes;
                using (var ms = new MemoryStream())
                {
                    using var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    resized.Save(ms, jpegEncoder, encoderParameters);
                    finalBytes = ms.ToArray();
                }

                const int maxBytes = (int)(1.5 * 1024 * 1024);
                while (finalBytes.Length > maxBytes && quality > 35)
                {
                    quality -= 10;
                    using var ms = new MemoryStream();
                    using var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    resized.Save(ms, jpegEncoder, encoderParameters);
                    finalBytes = ms.ToArray();
                }

                if (!Directory.Exists(_customerDisplayPath))
                {
                    Directory.CreateDirectory(_customerDisplayPath);
                }

                var destPath = Path.Combine(_customerDisplayPath, fileName);
                await File.WriteAllBytesAsync(destPath, finalBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Müşteri ekranı görseli kaydedilirken hata: {FileName}", fileName);
                throw new InvalidOperationException($"Görsel ağ paylaşımına ({_customerDisplayPath}\\{fileName}) yazılamadı: {ex.Message}", ex);
            }
        }
    }

    #endregion

    #region Hızlı Tuş Grupları & Ürünler & Mikro Stok Arama

    public async Task<IReadOnlyList<YeniKasaFastAccessCategoryDto>> GetFastAccessCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT c.id, c.name, c.description, c.category_key, c.created_at, c.updated_at,
                   COUNT(i.id) AS item_count
            FROM dbo.fast_access_items_categories c
            LEFT JOIN dbo.fast_access_items i ON i.category_key = c.category_key AND i.deleted_at IS NULL
            WHERE c.deleted_at IS NULL
            GROUP BY c.id, c.name, c.description, c.category_key, c.created_at, c.updated_at
            ORDER BY c.id ASC;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var list = new List<YeniKasaFastAccessCategoryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new YeniKasaFastAccessCategoryDto(
                Id: reader.GetInt32(0),
                Name: reader.GetString(1),
                Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                CategoryKey: reader.GetString(3),
                ItemCount: reader.GetInt32(6),
                CreatedAt: reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                UpdatedAt: reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
        }

        return list;
    }

    public async Task<YeniKasaFastAccessCategoryDto> CreateFastAccessCategoryAsync(CreateFastAccessCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var categoryKey = GenerateCategoryKey(20);

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO dbo.fast_access_items_categories (name, description, category_key, created_at, updated_at)
            OUTPUT INSERTED.id, INSERTED.created_at, INSERTED.updated_at
            VALUES (@Name, @Description, @CategoryKey, GETDATE(), GETDATE());";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Name", request.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CategoryKey", categoryKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Kategori eklenemedi.");

        var id = reader.GetInt32(0);
        var createdAt = reader.GetDateTime(1);
        var updatedAt = reader.GetDateTime(2);

        return new YeniKasaFastAccessCategoryDto(
            Id: id,
            Name: request.Name,
            Description: request.Description,
            CategoryKey: categoryKey,
            ItemCount: 0,
            CreatedAt: createdAt,
            UpdatedAt: updatedAt);
    }

    public async Task UpdateFastAccessCategoryAsync(UpdateFastAccessCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.fast_access_items_categories
            SET name = @Name,
                description = @Description,
                updated_at = GETDATE()
            WHERE id = @Id AND deleted_at IS NULL;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", request.Id);
        cmd.Parameters.AddWithValue("@Name", request.Name);
        cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteFastAccessCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE c
            SET deleted_at = GETDATE(),
                updated_at = GETDATE()
            FROM dbo.fast_access_items_categories c
            WHERE c.id = @Id
              AND c.deleted_at IS NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.fast_access_items i
                  WHERE i.category_key = c.category_key
                    AND i.deleted_at IS NULL
              );";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        var affectedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows == 0)
        {
            throw new InvalidOperationException(
                "Kategori bulunamadı veya içinde aktif ürün bulunduğu için silinemedi.");
        }
    }

    public async Task<IReadOnlyList<YeniKasaFastAccessItemDto>> GetFastAccessItemsByCategoryKeyAsync(string categoryKey, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT id, inventory_code, image_path, category_key, barcode, created_at, updated_at
            FROM dbo.fast_access_items
            WHERE category_key = @CategoryKey AND deleted_at IS NULL
            ORDER BY id ASC;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@CategoryKey", categoryKey);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var list = new List<YeniKasaFastAccessItemDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var invCode = reader.GetString(1);
            list.Add(new YeniKasaFastAccessItemDto(
                Id: reader.GetInt32(0),
                InventoryCode: invCode,
                ProductName: null,
                Barcode: reader.IsDBNull(4) ? null : reader.GetString(4),
                ImagePath: reader.IsDBNull(2) ? null : reader.GetString(2),
                CategoryKey: reader.GetString(3),
                ImageUrl: $"/api/ayar-islemleri/yeni-kasa-yonetimi/urun-gorsel/{invCode}",
                CreatedAt: reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                UpdatedAt: reader.IsDBNull(6) ? null : reader.GetDateTime(6)));
        }

        if (list.Count > 0)
        {
            await PopulateProductNamesFromMikroAsync(list, cancellationToken);
        }

        return list;
    }

    private async Task PopulateProductNamesFromMikroAsync(List<YeniKasaFastAccessItemDto> items, CancellationToken cancellationToken)
    {
        try
        {
            await using var mikroConn = CreateMikroConnection();
            if (mikroConn == null) return;
            await mikroConn.OpenAsync(cancellationToken);

            var codes = items.Select(i => i.InventoryCode).Distinct().ToList();
            var paramNames = codes.Select((_, idx) => $"@code{idx}").ToList();

            var sql = $"SELECT sto_kod, sto_isim FROM dbo.STOKLAR WITH (NOLOCK) WHERE sto_kod IN ({string.Join(",", paramNames)});";
            await using var cmd = new SqlCommand(sql, mikroConn);
            for (var i = 0; i < codes.Count; i++)
            {
                cmd.Parameters.AddWithValue(paramNames[i], codes[i]);
            }

            var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                nameMap[reader.GetString(0).Trim()] = reader.GetString(1).Trim();
            }

            for (var i = 0; i < items.Count; i++)
            {
                if (nameMap.TryGetValue(items[i].InventoryCode.Trim(), out var name))
                {
                    items[i] = items[i] with { ProductName = name };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mikro DB'den ürün adları sorgulanırken hata oluştu.");
        }
    }

    public async Task<YeniKasaFastAccessItemDto> CreateFastAccessItemAsync(CreateFastAccessItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InventoryCode))
            throw new ArgumentException("Stok kodu boş olamaz.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.CategoryKey))
            throw new ArgumentException("Hızlı tuş grubu seçilmelidir.", nameof(request));

        var inventoryCode = request.InventoryCode.Trim();

        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        // Foreign key constraint (fast_access_items_inventory_code_fkey) için
        // inventory tablosunda ürünün varlığından emin ol (yoksa Mikro'dan çekerek oluştur)
        await EnsureInventoryExistsAsync(inventoryCode, request.Barcode, connection, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Base64Data))
        {
            await SaveProductImageAsync(request.Base64Data, inventoryCode, cancellationToken);
        }

        // Kategori içinde bu ürün zaten var mı kontrol et (varsa un-delete yap veya mevcut kaydı döndür)
        const string checkSql = @"
            SELECT id, created_at, updated_at
            FROM dbo.fast_access_items
            WHERE category_key = @CategoryKey AND inventory_code = @InventoryCode;";

        await using (var checkCmd = new SqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue("@CategoryKey", request.CategoryKey);
            checkCmd.Parameters.AddWithValue("@InventoryCode", inventoryCode);
            await using var checkReader = await checkCmd.ExecuteReaderAsync(cancellationToken);
            if (await checkReader.ReadAsync(cancellationToken))
            {
                var existingId = checkReader.GetInt32(0);
                var existingCreated = checkReader.GetDateTime(1);
                var existingUpdated = checkReader.GetDateTime(2);
                checkReader.Close();

                const string updateSql = @"
                    UPDATE dbo.fast_access_items
                    SET deleted_at = NULL,
                        barcode = COALESCE(@Barcode, barcode),
                        updated_at = GETDATE()
                    WHERE id = @Id;";
                await using var updateCmd = new SqlCommand(updateSql, connection);
                updateCmd.Parameters.AddWithValue("@Id", existingId);
                updateCmd.Parameters.AddWithValue("@Barcode", (object?)request.Barcode ?? DBNull.Value);
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                return new YeniKasaFastAccessItemDto(
                    Id: existingId,
                    InventoryCode: inventoryCode,
                    ProductName: null,
                    Barcode: request.Barcode,
                    ImagePath: null,
                    CategoryKey: request.CategoryKey,
                    ImageUrl: $"/api/ayar-islemleri/yeni-kasa-yonetimi/urun-gorsel/{inventoryCode}",
                    CreatedAt: existingCreated,
                    UpdatedAt: DateTime.Now);
            }
        }

        const string sql = @"
            INSERT INTO dbo.fast_access_items (inventory_code, image_path, category_key, barcode, created_at, updated_at)
            OUTPUT INSERTED.id, INSERTED.created_at, INSERTED.updated_at
            VALUES (@InventoryCode, @ImagePath, @CategoryKey, @Barcode, GETDATE(), GETDATE());";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InventoryCode", inventoryCode);
        cmd.Parameters.AddWithValue("@ImagePath", DBNull.Value);
        cmd.Parameters.AddWithValue("@CategoryKey", request.CategoryKey);
        cmd.Parameters.AddWithValue("@Barcode", (object?)request.Barcode ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Hızlı tuş ürünü eklenemedi.");

        var id = reader.GetInt32(0);
        var createdAt = reader.GetDateTime(1);
        var updatedAt = reader.GetDateTime(2);

        return new YeniKasaFastAccessItemDto(
            Id: id,
            InventoryCode: inventoryCode,
            ProductName: null,
            Barcode: request.Barcode,
            ImagePath: null,
            CategoryKey: request.CategoryKey,
            ImageUrl: $"/api/ayar-islemleri/yeni-kasa-yonetimi/urun-gorsel/{inventoryCode}",
            CreatedAt: createdAt,
            UpdatedAt: updatedAt);
    }

    private async Task EnsureInventoryExistsAsync(
        string inventoryCode,
        string? barcode,
        SqlConnection shopigoConn,
        CancellationToken cancellationToken)
    {
        // 1. Ürünün Mikro ERP sisteminde tanımlı olduğunu doğrula (Mikro'da olmayan ürün eklenemez)
        await using var mikroConn = CreateMikroConnection();
        if (mikroConn == null)
        {
            throw new InvalidOperationException("Mikro ERP bağlantısı kurulamadığı için ürün doğrulaması yapılamadı.");
        }

        await mikroConn.OpenAsync(cancellationToken);
        const string mikroSql = @"
            SELECT TOP 1 sto_isim, sto_birim1_ad, sto_perakende_vergi
            FROM dbo.STOKLAR WITH (NOLOCK)
            WHERE sto_kod = @Code;";
        await using var mCmd = new SqlCommand(mikroSql, mikroConn);
        mCmd.Parameters.AddWithValue("@Code", inventoryCode);

        string? productName = null;
        string unit = "ADET";
        int tax = 1;
        bool foundInMikro = false;

        await using (var mReader = await mCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await mReader.ReadAsync(cancellationToken))
            {
                foundInMikro = true;
                if (!mReader.IsDBNull(0)) productName = mReader.GetString(0).Trim();
                if (!mReader.IsDBNull(1)) unit = mReader.GetString(1).Trim();
                if (!mReader.IsDBNull(2)) tax = Convert.ToInt32(mReader.GetValue(2));
            }
        }

        if (!foundInMikro)
        {
            throw new InvalidOperationException($"'{inventoryCode}' kodlu ürün Mikro ERP sisteminde kayıtlı değildir. Yalnızca Mikro'da tanımlı geçerli ürünler hızlı tuşlara eklenebilir.");
        }

        // 2. Ürün Shopigo dbo.inventory tablosunda var mı kontrol et, yoksa Mikro bilgileriyle oluştur
        const string existsSql = "SELECT 1 FROM dbo.inventory WHERE inventory_code = @Code;";
        await using (var existsCmd = new SqlCommand(existsSql, shopigoConn))
        {
            existsCmd.Parameters.AddWithValue("@Code", inventoryCode);
            var exists = await existsCmd.ExecuteScalarAsync(cancellationToken);
            if (exists != null && exists != DBNull.Value)
                return;
        }

        const string insertSql = @"
            INSERT INTO dbo.inventory (
                inventory_code, name, unit, tax_percent, is_promotable,
                perakende_vergi_orani, perakende_vergi_kodu,
                toptan_vergi_orani, toptan_vergi_kodu,
                ctg_code, created_at, updated_at
            )
            VALUES (
                @Code, @Name, @Unit, @Tax, 0,
                @Tax, 1, @Tax, 1,
                'GENEL', GETDATE(), GETDATE()
            );";

        await using var insCmd = new SqlCommand(insertSql, shopigoConn);
        insCmd.Parameters.AddWithValue("@Code", inventoryCode);
        insCmd.Parameters.AddWithValue("@Name", productName ?? inventoryCode);
        insCmd.Parameters.AddWithValue("@Unit", string.IsNullOrWhiteSpace(unit) ? "ADET" : unit);
        insCmd.Parameters.AddWithValue("@Tax", tax);

        await insCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteFastAccessItemAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateShopigoConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE dbo.fast_access_items
            SET deleted_at = GETDATE(),
                updated_at = GETDATE()
            WHERE id = @Id AND deleted_at IS NULL;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetFastAccessItemImageAsync(string inventoryCode, CancellationToken cancellationToken = default)
    {
        var safeCode = string.Concat(inventoryCode.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(safeCode) || !string.Equals(safeCode, inventoryCode, StringComparison.Ordinal))
        {
            return null;
        }
        var extensions = new[] { ".jpg", ".jpeg", ".png" };

        string? foundPath = null;
        string contentType = "image/jpeg";

        try
        {
            foreach (var ext in extensions)
            {
                var testPath = Path.Combine(_productImagePath, $"{safeCode}{ext}");
                if (File.Exists(testPath))
                {
                    foundPath = testPath;
                    contentType = ext == ".png" ? "image/png" : "image/jpeg";
                    break;
                }
            }

            if (foundPath != null)
            {
                var bytes = await File.ReadAllBytesAsync(foundPath, cancellationToken);
                return (bytes, contentType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dosya yolundan ürün görseli okunamadı: {Code}, {Path}", safeCode, _productImagePath);
        }

        // Fallback: Shopigo dbo.inventory.resim (varbinary) kontrol et
        try
        {
            await using var connection = CreateShopigoConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT resim FROM dbo.inventory WHERE inventory_code = @Code AND resim IS NOT NULL;";
            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Code", safeCode);

            var dbResim = await cmd.ExecuteScalarAsync(cancellationToken);
            if (dbResim is byte[] resimBytes && resimBytes.Length > 0)
            {
                return (resimBytes, "image/jpeg");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "dbo.inventory.resim alanından görsel okunamadı: {Code}", safeCode);
        }

        return null;
    }

    public async Task<IReadOnlyList<YeniKasaStockLookupDto>> SearchStocksAsync(string query, int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return Array.Empty<YeniKasaStockLookupDto>();

        var rawQuery = query.Trim();
        var likeQuery = $"%{rawQuery}%";
        var startQuery = $"{rawQuery}%";

        try
        {
            await using var conn = CreateMikroConnection();
            if (conn == null)
            {
                throw new InvalidOperationException(
                    "MikroConnection bağlantı dizesi yapılandırılmamış. Stok araması yapılamadı.");
            }
            await conn.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT TOP (@Take)
                    s.sto_kod,
                    s.sto_isim,
                    s.sto_birim1_ad,
                    b.bar_kodu,
                    f.sfiyat_fiyati
                FROM dbo.STOKLAR s WITH (NOLOCK)
                OUTER APPLY (
                    SELECT TOP 1 b.bar_kodu
                    FROM dbo.BARKOD_TANIMLARI b WITH (NOLOCK)
                    WHERE b.bar_stokkodu = s.sto_kod AND ISNULL(b.bar_iptal, 0) <> 1
                    ORDER BY ISNULL(b.bar_master, 0) DESC, b.bar_create_date DESC
                ) b
                OUTER APPLY (
                    SELECT TOP 1 f.sfiyat_fiyati
                    FROM dbo.STOK_SATIS_FIYAT_LISTELERI f WITH (NOLOCK)
                    WHERE f.sfiyat_stokkod = s.sto_kod AND f.sfiyat_listesirano = 1 AND ISNULL(f.sfiyat_iptal, 0) <> 1
                    ORDER BY f.sfiyat_deposirano ASC
                ) f
                WHERE s.sto_kod LIKE @LikeQuery
                   OR s.sto_isim LIKE @LikeQuery
                   OR EXISTS (
                       SELECT 1 FROM dbo.BARKOD_TANIMLARI bx WITH (NOLOCK)
                       WHERE bx.bar_stokkodu = s.sto_kod AND bx.bar_kodu LIKE @LikeQuery AND ISNULL(bx.bar_iptal, 0) <> 1
                   )
                ORDER BY
                    CASE WHEN s.sto_kod = @RawQuery THEN 0
                         WHEN s.sto_kod LIKE @StartQuery THEN 1
                         ELSE 2 END,
                    s.sto_kod ASC;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 100));
            cmd.Parameters.AddWithValue("@LikeQuery", likeQuery);
            cmd.Parameters.AddWithValue("@StartQuery", startQuery);
            cmd.Parameters.AddWithValue("@RawQuery", rawQuery);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<YeniKasaStockLookupDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new YeniKasaStockLookupDto(
                    StockCode: reader.GetString(0).Trim(),
                    StockName: reader.GetString(1).Trim(),
                    Unit: reader.IsDBNull(2) ? null : reader.GetString(2).Trim(),
                    Barcode: reader.IsDBNull(3) ? null : reader.GetString(3).Trim(),
                    SalePrice: reader.IsDBNull(4) ? null : Convert.ToDecimal(reader.GetValue(4))));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mikro stok arama sırasında hata oluştu. Query: {Query}", query);
            throw;
        }
    }

    private void TryDeleteCustomerDisplayFile(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_customerDisplayPath, Path.GetFileName(fileName));
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Başarısız kayıt sonrası müşteri ekranı dosyası temizlenemedi: {FileName}", fileName);
        }
    }

    private async Task SaveProductImageAsync(string base64Data, string inventoryCode, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            throw new PlatformNotSupportedException(
                "Yeni Kasa Yönetimi görsel işleme özelliği yalnızca Windows sunucuda desteklenir.");
        }
        byte[] finalBytes;
        try
        {
            var imageBytes = DecodeBase64Payload(base64Data, MaxProductImageBytes, "Ürün görseli");

            using var inStream = new MemoryStream(imageBytes);
            using var original = (Bitmap)System.Drawing.Image.FromStream(inStream);
            ValidateImageDimensions(original, "Ürün görseli");

            using var resized = new Bitmap(283, 283);
            using (var g = Graphics.FromImage(resized))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(original, 0, 0, 283, 283);
            }

            var jpegEncoder = GetEncoder(ImageFormat.Jpeg)
                ?? throw new InvalidOperationException("JPEG encoder bulunamadı.");

            long quality = 75;
            using (var ms = new MemoryStream())
            {
                using var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                resized.Save(ms, jpegEncoder, encoderParameters);
                finalBytes = ms.ToArray();
            }

            const int maxBytes = 30 * 1024;
            while (finalBytes.Length > maxBytes && quality > 20)
            {
                quality -= 10;
                using var ms = new MemoryStream();
                using var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                resized.Save(ms, jpegEncoder, encoderParameters);
                finalBytes = ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün görseli optimize edilirken hata: {InventoryCode}", inventoryCode);
            throw new InvalidOperationException("Ürün görseli geçerli bir JPG veya PNG dosyası değil.", ex);
        }

        // 1. Ağ paylaşımına dosya olarak yaz: \\10.0.0.55\kasa\Furpa Kasa Sync\urun_gorselleri\[kod].jpg
        // Kasaların bu görseli alıp gösterebilmesi için ağ paylaşımına yazılması zorunludur.
        var safeCode = string.Concat(inventoryCode.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        try
        {
            if (!Directory.Exists(_productImagePath))
            {
                Directory.CreateDirectory(_productImagePath);
            }

            var destPath = Path.Combine(_productImagePath, $"{safeCode}.jpg");
            await File.WriteAllBytesAsync(destPath, finalBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ürün görseli ağ paylaşımına ({Path}) yazılamadı: {Code}", _productImagePath, safeCode);
            throw new InvalidOperationException(
                $"Kasaların görseli alabilmesi için ürün görselinin ağ paylaşımına ({_productImagePath}\\{safeCode}.jpg) yazılması zorunludur ancak dosya yazılamadı: {ex.Message}", ex);
        }

        // 2. Shopigo dbo.inventory.resim (varbinary) güncelle
        try
        {
            await using var conn = CreateShopigoConnection();
            await conn.OpenAsync(cancellationToken);
            const string sql = "UPDATE dbo.inventory SET resim = @Resim, updated_at = GETDATE() WHERE inventory_code = @Code;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Resim", SqlDbType.VarBinary, -1).Value = finalBytes;
            cmd.Parameters.AddWithValue("@Code", inventoryCode);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ürün görseli dbo.inventory.resim alanına yazılamadı: {Code}", inventoryCode);
        }
    }

    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            throw new PlatformNotSupportedException(
                "Yeni Kasa Yönetimi görsel işleme özelliği yalnızca Windows sunucuda desteklenir.");
        }
        var codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
    }

    private static byte[] DecodeBase64Payload(string base64Data, int maxBytes, string contentLabel)
    {
        if (string.IsNullOrWhiteSpace(base64Data))
        {
            throw new ArgumentException($"{contentLabel} verisi boş olamaz.", nameof(base64Data));
        }

        var separatorIndex = base64Data.IndexOf(',');
        var payload = separatorIndex >= 0 ? base64Data[(separatorIndex + 1)..] : base64Data;
        payload = payload.Trim();

        var estimatedBytes = (long)payload.Length * 3 / 4;
        if (estimatedBytes > maxBytes + 2L)
        {
            throw new ArgumentException($"{contentLabel} azami {maxBytes / (1024 * 1024)} MB olabilir.", nameof(base64Data));
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length > maxBytes)
            {
                throw new ArgumentException($"{contentLabel} azami {maxBytes / (1024 * 1024)} MB olabilir.", nameof(base64Data));
            }

            return bytes;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"{contentLabel} Base64 biçimi geçersiz.", nameof(base64Data), ex);
        }
    }

    private static void ValidateImageDimensions(System.Drawing.Image image, string contentLabel)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            throw new PlatformNotSupportedException(
                "Yeni Kasa Yönetimi görsel işleme özelliği yalnızca Windows sunucuda desteklenir.");
        }

        const int maxDimension = 12_000;
        const long maxPixels = 60_000_000;
        if (image.Width <= 0 || image.Height <= 0 || image.Width > maxDimension || image.Height > maxDimension ||
            (long)image.Width * image.Height > maxPixels)
        {
            throw new ArgumentException($"{contentLabel} boyutları güvenli sınırların dışında.");
        }
    }

    private static bool HasSupportedVideoSignature(byte[] bytes, string extension)
    {
        if (extension.Equals(".webm", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 4 && bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3;
        }

        return bytes.Length >= 12 && bytes[4] == (byte)'f' && bytes[5] == (byte)'t' &&
            bytes[6] == (byte)'y' && bytes[7] == (byte)'p';
    }

    private static string SanitizeFileStem(string value, string fallback)
    {
        var sanitized = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .Take(60)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string GenerateCategoryKey(int length = 20)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var buffer = new char[length];
        var randomBytes = new byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(buffer);
    }

    #endregion
}
#pragma warning restore CA1416
