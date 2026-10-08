using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed partial class DatabaseMonitoringService(
    MikroDbContext mikroDbContext,
    AuthDbContext authDbContext,
    IClock clock,
    IOptionsMonitor<DatabaseMonitoringOptions> options) : IDatabaseMonitoringService
{
    private const string SnapshotSql =
        """
        SET NOCOUNT ON;

        SELECT
            CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS server_name,
            DB_NAME() AS database_name;

        SELECT
            r.session_id,
            r.request_id,
            s.login_time,
            TRY_CONVERT(int, s.host_process_id) AS host_process_id,
            DB_NAME(r.database_id) AS database_name,
            r.status,
            r.command,
            r.start_time,
            CONVERT(bigint, r.total_elapsed_time) AS elapsed_ms,
            r.wait_type,
            CONVERT(bigint, r.wait_time) AS wait_ms,
            r.wait_resource,
            NULLIF(r.blocking_session_id, 0) AS blocking_session_id,
            CONVERT(bigint, r.cpu_time) AS cpu_ms,
            CONVERT(bigint, r.reads) AS reads,
            CONVERT(bigint, r.logical_reads) AS logical_reads,
            CONVERT(bigint, r.writes) AS writes,
            r.open_transaction_count,
            CONVERT(decimal(9,2), r.percent_complete) AS percent_complete,
            s.login_name,
            s.host_name,
            s.program_name,
            c.client_net_address,
            txt.text AS sql_text
        FROM sys.dm_exec_requests AS r
        INNER JOIN sys.dm_exec_sessions AS s ON s.session_id = r.session_id
        LEFT JOIN sys.dm_exec_connections AS c ON c.session_id = r.session_id
        OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS txt
        WHERE r.session_id <> @@SPID
          AND s.is_user_process = 1
        ORDER BY r.total_elapsed_time DESC;

        SELECT
            s.session_id,
            s.login_time,
            TRY_CONVERT(int, s.host_process_id) AS host_process_id,
            at.transaction_id,
            at.transaction_begin_time,
            DATEDIFF_BIG(millisecond, at.transaction_begin_time, SYSDATETIME()) AS age_ms,
            at.transaction_state,
            s.open_transaction_count,
            s.login_name,
            s.host_name,
            s.program_name,
            c.client_net_address,
            lasttxt.text AS last_sql_text
        FROM sys.dm_tran_session_transactions AS st
        INNER JOIN sys.dm_tran_active_transactions AS at ON at.transaction_id = st.transaction_id
        INNER JOIN sys.dm_exec_sessions AS s ON s.session_id = st.session_id
        LEFT JOIN sys.dm_exec_connections AS c ON c.session_id = s.session_id
        OUTER APPLY sys.dm_exec_sql_text(c.most_recent_sql_handle) AS lasttxt
        WHERE s.session_id <> @@SPID
          AND s.is_user_process = 1
          AND st.is_user_transaction = 1
        ORDER BY at.transaction_begin_time;
        """;

    private const string SessionSql =
        """
        SELECT
            s.session_id,
            s.login_time,
            TRY_CONVERT(int, s.host_process_id) AS host_process_id,
            s.is_user_process,
            s.login_name,
            s.host_name,
            s.program_name,
            DB_NAME(COALESCE(r.database_id, s.database_id)) AS database_name,
            txt.text AS sql_text,
            @@SPID AS monitor_session_id
        FROM sys.dm_exec_sessions AS s
        LEFT JOIN sys.dm_exec_requests AS r ON r.session_id = s.session_id
        LEFT JOIN sys.dm_exec_connections AS c ON c.session_id = s.session_id
        OUTER APPLY sys.dm_exec_sql_text(COALESCE(r.sql_handle, c.most_recent_sql_handle)) AS txt
        WHERE s.session_id = @session_id;
        """;

    public async Task<DatabaseMonitoringSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var settings = CurrentSettings();
        var requests = new List<DatabaseActiveRequestDto>();
        var transactions = new List<DatabaseOpenTransactionDto>();
        var serverName = string.Empty;
        var databaseName = string.Empty;

        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SnapshotSql;
            command.CommandTimeout = settings.CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                serverName = GetString(reader, "server_name") ?? string.Empty;
                databaseName = GetString(reader, "database_name") ?? string.Empty;
            }

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var elapsed = GetInt64(reader, "elapsed_ms");
                var waitType = GetString(reader, "wait_type");
                var blockingSessionId = GetNullableInt32(reader, "blocking_session_id");
                var assessment = AssessRequest(waitType, blockingSessionId, elapsed, settings);

                requests.Add(new DatabaseActiveRequestDto(
                    GetInt32(reader, "session_id"),
                    GetInt32(reader, "request_id"),
                    GetDateTime(reader, "login_time"),
                    GetNullableInt32(reader, "host_process_id"),
                    GetString(reader, "database_name"),
                    GetString(reader, "status") ?? string.Empty,
                    GetString(reader, "command") ?? string.Empty,
                    GetDateTime(reader, "start_time"),
                    elapsed,
                    waitType,
                    GetInt64(reader, "wait_ms"),
                    GetString(reader, "wait_resource"),
                    blockingSessionId,
                    GetInt64(reader, "cpu_ms"),
                    GetInt64(reader, "reads"),
                    GetInt64(reader, "logical_reads"),
                    GetInt64(reader, "writes"),
                    GetInt32(reader, "open_transaction_count"),
                    GetDecimal(reader, "percent_complete"),
                    GetString(reader, "login_name") ?? string.Empty,
                    GetString(reader, "host_name"),
                    GetString(reader, "program_name"),
                    GetString(reader, "client_net_address"),
                    SanitizeSql(GetString(reader, "sql_text"), settings.SqlTextMaxLength),
                    assessment.Severity,
                    assessment.Recommendation,
                    GetInt32(reader, "session_id") > 50));
            }

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var age = GetInt64(reader, "age_ms");
                var severe = age >= settings.OpenTransactionThresholdSeconds * 2000L;
                transactions.Add(new DatabaseOpenTransactionDto(
                    GetInt32(reader, "session_id"),
                    GetDateTime(reader, "login_time"),
                    GetNullableInt32(reader, "host_process_id"),
                    GetInt64(reader, "transaction_id"),
                    GetDateTime(reader, "transaction_begin_time"),
                    age,
                    TransactionStateName(GetInt32(reader, "transaction_state")),
                    GetInt32(reader, "open_transaction_count"),
                    GetString(reader, "login_name") ?? string.Empty,
                    GetString(reader, "host_name"),
                    GetString(reader, "program_name"),
                    GetString(reader, "client_net_address"),
                    SanitizeSql(GetString(reader, "last_sql_text"), settings.SqlTextMaxLength),
                    severe ? "critical" : age >= settings.OpenTransactionThresholdSeconds * 1000L ? "warning" : "info",
                    "Uzun acik transaction uygulama tarafinda commit/rollback edilmelidir; sonlandirma yalniz etkisi kontrol edildikten sonra kullanilmalidir.",
                    GetInt32(reader, "session_id") > 50));
            }
        }
        catch (SqlException exception) when (exception.Number is 297 or 300)
        {
            throw new InvalidOperationException(
                "Mikro SQL kullanicisinin DMV verilerini okuyabilmesi icin VIEW SERVER STATE (SQL Server 2022+ icin VIEW SERVER PERFORMANCE STATE) yetkisi gerekir.",
                exception);
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open) await connection.CloseAsync();
        }

        var blocking = BuildBlockingEdges(requests);
        var recommendations = BuildRecommendations(requests, transactions, settings);
        var blockedCount = requests.Count(item => item.BlockingSessionId.HasValue);
        var rootCount = blocking.Select(item => item.RootSessionId).Distinct().Count();
        var longCount = requests.Count(item => item.ElapsedMilliseconds >= settings.LongRunningThresholdSeconds * 1000L);
        var overallStatus = recommendations.Any(item => item.Severity == "critical")
            ? "critical"
            : recommendations.Count > 0 ? "warning" : "healthy";

        return new DatabaseMonitoringSnapshotDto(
            clock.UtcNow,
            serverName,
            databaseName,
            overallStatus,
            requests.Count,
            blockedCount,
            rootCount,
            longCount,
            transactions.Count,
            new DatabaseMonitoringThresholdsDto(
                settings.BlockingThresholdSeconds,
                settings.LongRunningThresholdSeconds,
                settings.OpenTransactionThresholdSeconds,
                settings.UiRefreshSeconds,
                settings.RetentionHours,
                settings.MaxIncidentCount),
            requests,
            blocking,
            transactions,
            recommendations);
    }

    public async Task<IReadOnlyCollection<DatabaseMonitoringIncidentDto>> GetIncidentsAsync(
        int take,
        CancellationToken cancellationToken) =>
        await authDbContext.DatabaseMonitoringIncidents
            .AsNoTracking()
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(item => new DatabaseMonitoringIncidentDto(
                item.Id,
                item.Type,
                item.Severity,
                item.SessionId,
                item.BlockingSessionId,
                item.DatabaseName,
                item.LoginName,
                item.HostName,
                item.ProgramName,
                item.WaitType,
                item.ElapsedMilliseconds,
                item.SqlText,
                item.Recommendation,
                item.FirstSeenAtUtc,
                item.LastSeenAtUtc,
                item.OccurrenceCount,
                item.ResolvedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DatabaseSessionTerminationDto>> GetTerminationHistoryAsync(
        int take,
        CancellationToken cancellationToken)
    {
        var items = await authDbContext.DatabaseSessionTerminationAudits
            .AsNoTracking()
            .OrderByDescending(item => item.RequestedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToArrayAsync(cancellationToken);

        return items.Select(ToDto).ToArray();
    }

    public async Task<DatabaseSessionTerminationDto> TerminateSessionAsync(
        int sessionId,
        TerminateDatabaseSessionRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        if (sessionId <= 50) throw new ArgumentException("System sessions can not be terminated.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            throw new ArgumentException("Termination reason must contain at least 10 characters.", nameof(request));

        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        DatabaseSessionSnapshot session;

        try
        {
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            session = await GetSessionAsync(connection, sessionId, CurrentSettings().CommandTimeoutSeconds, cancellationToken)
                ?? throw new KeyNotFoundException("SQL session was not found or already ended.");

            if (!session.IsUserProcess || session.SessionId == session.MonitorSessionId)
                throw new InvalidOperationException("This SQL session can not be terminated.");

            if (!MatchesSessionIdentity(
                    session.LoginTime,
                    session.HostProcessId,
                    session.ProgramName,
                    request))
            {
                throw new InvalidOperationException(
                    "SQL session identity changed. Refresh the screen before attempting termination again.");
            }

            var audit = new DatabaseSessionTerminationAudit(
                Guid.NewGuid(),
                session.SessionId,
                session.LoginTime,
                session.HostProcessId,
                session.LoginName,
                session.HostName,
                session.ProgramName,
                session.DatabaseName,
                SanitizeSql(session.SqlText, 4000),
                request.Reason,
                requestedByUserId,
                clock.UtcNow);
            authDbContext.DatabaseSessionTerminationAudits.Add(audit);
            await authDbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await using var killCommand = connection.CreateCommand();
                killCommand.CommandText = $"KILL {sessionId};";
                killCommand.CommandTimeout = CurrentSettings().CommandTimeoutSeconds;
                await killCommand.ExecuteNonQueryAsync(cancellationToken);
                audit.Complete(true, null, clock.UtcNow);
            }
            catch (Exception exception)
            {
                audit.Complete(false, exception.Message, clock.UtcNow);
                await authDbContext.SaveChangesAsync(CancellationToken.None);
                throw;
            }

            await authDbContext.SaveChangesAsync(CancellationToken.None);
            return ToDto(audit);
        }
        finally
        {
            if (shouldClose && connection.State == ConnectionState.Open) await connection.CloseAsync();
        }
    }

    public async Task<DatabaseRollbackStatusDto> GetRollbackStatusAsync(
        int sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId <= 0) throw new ArgumentException("Session id must be positive.", nameof(sessionId));
        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        var messages = new List<string>();
        SqlInfoMessageEventHandler? handler = null;

        try
        {
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            if (connection is SqlConnection sqlConnection)
            {
                handler = (_, args) => messages.Add(args.Message);
                sqlConnection.InfoMessage += handler;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"KILL {sessionId} WITH STATUSONLY;";
            command.CommandTimeout = CurrentSettings().CommandTimeoutSeconds;
            await command.ExecuteNonQueryAsync(cancellationToken);
            var message = messages.Count == 0 ? "Rollback is in progress." : string.Join(" ", messages);
            return new DatabaseRollbackStatusDto(sessionId, true, message, clock.UtcNow);
        }
        catch (SqlException exception) when (exception.Number is 6104 or 6120)
        {
            return new DatabaseRollbackStatusDto(sessionId, false, "No active rollback was found for this session.", clock.UtcNow);
        }
        finally
        {
            if (handler is not null && connection is SqlConnection sqlConnection) sqlConnection.InfoMessage -= handler;
            if (shouldClose && connection.State == ConnectionState.Open) await connection.CloseAsync();
        }
    }

    internal async Task CollectIncidentsAsync(CancellationToken cancellationToken)
    {
        var settings = CurrentSettings();
        if (!settings.Enabled) return;

        var snapshot = await GetSnapshotAsync(cancellationToken);
        var observedAt = clock.UtcNow;
        var candidates = DeduplicateIncidentCandidates(BuildIncidentCandidates(snapshot, settings));

        try
        {
            await ApplyIncidentCandidatesAsync(candidates, observedAt, cancellationToken);
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // Another worker may have inserted the same fingerprint after our read.
            authDbContext.ChangeTracker.Clear();
            await ApplyIncidentCandidatesAsync(candidates, observedAt, cancellationToken);
            await authDbContext.SaveChangesAsync(cancellationToken);
        }

        var retentionStart = observedAt.AddHours(-settings.RetentionHours);
        await authDbContext.DatabaseMonitoringIncidents
            .Where(item => item.LastSeenAtUtc < retentionStart)
            .ExecuteDeleteAsync(cancellationToken);

        var excessIds = await authDbContext.DatabaseMonitoringIncidents
            .OrderByDescending(item => item.LastSeenAtUtc)
            .Skip(settings.MaxIncidentCount)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (excessIds.Length > 0)
        {
            await authDbContext.DatabaseMonitoringIncidents
                .Where(item => excessIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task ApplyIncidentCandidatesAsync(
        IReadOnlyCollection<IncidentCandidate> candidates,
        DateTime observedAt,
        CancellationToken cancellationToken)
    {
        var fingerprints = candidates.Select(item => item.Fingerprint).ToArray();
        var existing = fingerprints.Length == 0
            ? new Dictionary<string, DatabaseMonitoringIncident>(StringComparer.Ordinal)
            : await authDbContext.DatabaseMonitoringIncidents
                .Where(item => fingerprints.Contains(item.Fingerprint))
                .ToDictionaryAsync(item => item.Fingerprint, StringComparer.Ordinal, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (existing.TryGetValue(candidate.Fingerprint, out var incident))
            {
                incident.Observe(
                    candidate.Severity,
                    candidate.BlockingSessionId,
                    candidate.WaitType,
                    candidate.ElapsedMilliseconds,
                    candidate.SqlText,
                    candidate.Recommendation,
                    observedAt);
                continue;
            }

            var newIncident = new DatabaseMonitoringIncident(
                Guid.NewGuid(),
                candidate.Fingerprint,
                candidate.Type,
                candidate.Severity,
                candidate.SessionId,
                candidate.BlockingSessionId,
                candidate.DatabaseName,
                candidate.LoginName,
                candidate.HostName,
                candidate.ProgramName,
                candidate.WaitType,
                candidate.ElapsedMilliseconds,
                candidate.SqlText,
                candidate.Recommendation,
                observedAt);
            authDbContext.DatabaseMonitoringIncidents.Add(newIncident);
            existing.Add(candidate.Fingerprint, newIncident);
        }

        var active = await authDbContext.DatabaseMonitoringIncidents
            .Where(item => item.ResolvedAtUtc == null && !fingerprints.Contains(item.Fingerprint))
            .ToArrayAsync(cancellationToken);
        foreach (var incident in active) incident.Resolve(observedAt);
    }

    private DatabaseMonitoringOptions CurrentSettings()
    {
        var value = options.CurrentValue;
        return new DatabaseMonitoringOptions
        {
            Enabled = value.Enabled,
            CollectionIntervalSeconds = Math.Clamp(value.CollectionIntervalSeconds, 15, 3600),
            UiRefreshSeconds = Math.Clamp(value.UiRefreshSeconds, 5, 300),
            BlockingThresholdSeconds = Math.Clamp(value.BlockingThresholdSeconds, 5, 3600),
            LongRunningThresholdSeconds = Math.Clamp(value.LongRunningThresholdSeconds, 5, 86400),
            OpenTransactionThresholdSeconds = Math.Clamp(value.OpenTransactionThresholdSeconds, 10, 86400),
            RetentionHours = Math.Clamp(value.RetentionHours, 1, 720),
            MaxIncidentCount = Math.Clamp(value.MaxIncidentCount, 100, 50000),
            SqlTextMaxLength = Math.Clamp(value.SqlTextMaxLength, 200, 4000),
            CommandTimeoutSeconds = Math.Clamp(value.CommandTimeoutSeconds, 2, 30)
        };
    }

    private static async Task<DatabaseSessionSnapshot?> GetSessionAsync(
        DbConnection connection,
        int sessionId,
        int commandTimeout,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SessionSql;
        command.CommandTimeout = commandTimeout;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@session_id";
        parameter.DbType = DbType.Int32;
        parameter.Value = sessionId;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new DatabaseSessionSnapshot(
            GetInt32(reader, "session_id"),
            GetDateTime(reader, "login_time"),
            GetNullableInt32(reader, "host_process_id"),
            GetBoolean(reader, "is_user_process"),
            GetString(reader, "login_name"),
            GetString(reader, "host_name"),
            GetString(reader, "program_name"),
            GetString(reader, "database_name"),
            GetString(reader, "sql_text"),
            GetInt32(reader, "monitor_session_id"));
    }

    private static IReadOnlyCollection<DatabaseBlockingEdgeDto> BuildBlockingEdges(
        IReadOnlyCollection<DatabaseActiveRequestDto> requests)
    {
        var bySession = requests.GroupBy(item => item.SessionId).ToDictionary(group => group.Key, group => group.First());
        var result = new List<DatabaseBlockingEdgeDto>();
        foreach (var request in requests.Where(item => item.BlockingSessionId.HasValue))
        {
            var blocker = request.BlockingSessionId!.Value;
            var root = blocker;
            var depth = 1;
            var seen = new HashSet<int> { request.SessionId };
            while (seen.Add(root) && bySession.TryGetValue(root, out var parent) && parent.BlockingSessionId.HasValue)
            {
                root = parent.BlockingSessionId.Value;
                depth++;
            }

            result.Add(new DatabaseBlockingEdgeDto(
                root,
                blocker,
                request.SessionId,
                depth,
                request.WaitType,
                request.WaitMilliseconds,
                request.SqlText));
        }

        return result.OrderBy(item => item.RootSessionId).ThenBy(item => item.Depth).ToArray();
    }

    private static IReadOnlyCollection<DatabaseRecommendationDto> BuildRecommendations(
        IReadOnlyCollection<DatabaseActiveRequestDto> requests,
        IReadOnlyCollection<DatabaseOpenTransactionDto> transactions,
        DatabaseMonitoringOptions settings)
    {
        var result = new List<DatabaseRecommendationDto>();
        var blocked = requests.Where(item => item.BlockingSessionId.HasValue && item.WaitMilliseconds >= settings.BlockingThresholdSeconds * 1000L).ToArray();
        if (blocked.Length > 0)
            result.Add(new("blocking", "critical", "SQL kilit zinciri var", "Root blocker oturumunu, acik transaction'i ve uygulama islemini kontrol edin. Etkisi dogrulanmadan KILL kullanmayin.", blocked.Length));

        var io = requests.Where(item => item.WaitType?.Contains("PAGEIOLATCH", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        if (io.Length > 0)
            result.Add(new("io-wait", "warning", "Disk okuma beklemesi var", "Execution plan, eksik/uygunsuz index ve SQL Server disk gecikmesini birlikte inceleyin.", io.Length));

        var log = requests.Where(item => item.WaitType?.StartsWith("WRITELOG", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        if (log.Length > 0)
            result.Add(new("log-wait", "warning", "Transaction log beklemesi var", "Log diski gecikmesini, log buyumesini ve gereksiz buyuk transaction'lari kontrol edin.", log.Length));

        var longRunning = requests.Where(item => item.ElapsedMilliseconds >= settings.LongRunningThresholdSeconds * 1000L).ToArray();
        if (longRunning.Length > 0)
            result.Add(new("long-running", "warning", "Uzun suren sorgular var", "En yuksek elapsed/read degerli sorgularin execution plan ve filtrelerini inceleyin.", longRunning.Length));

        var oldTransactions = transactions.Where(item => item.AgeMilliseconds >= settings.OpenTransactionThresholdSeconds * 1000L).ToArray();
        if (oldTransactions.Length > 0)
            result.Add(new("open-transaction", "warning", "Uzun acik transaction var", "Uygulama tarafindaki commit/rollback akislarini ve baglanti havuzuna acik transaction donup donmedigini kontrol edin.", oldTransactions.Length));

        return result;
    }

    private static IEnumerable<IncidentCandidate> BuildIncidentCandidates(
        DatabaseMonitoringSnapshotDto snapshot,
        DatabaseMonitoringOptions settings)
    {
        foreach (var request in snapshot.Requests)
        {
            string? type = null;
            if (request.BlockingSessionId.HasValue && request.WaitMilliseconds >= settings.BlockingThresholdSeconds * 1000L)
                type = "Blocking";
            else if (request.ElapsedMilliseconds >= settings.LongRunningThresholdSeconds * 1000L)
                type = "LongRunning";

            if (type is null) continue;
            var identity = $"{type}|{request.SessionId}|{request.LoginTime:O}|{request.BlockingSessionId}";
            yield return new IncidentCandidate(
                Hash(identity), type, request.Severity, request.SessionId, request.BlockingSessionId,
                request.DatabaseName, request.LoginName, request.HostName, request.ProgramName,
                request.WaitType, request.ElapsedMilliseconds, request.SqlText, request.Recommendation);
        }

        foreach (var transaction in snapshot.OpenTransactions.Where(item =>
                     item.AgeMilliseconds >= settings.OpenTransactionThresholdSeconds * 1000L))
        {
            var identity = $"OpenTransaction|{transaction.SessionId}|{transaction.LoginTime:O}|{transaction.TransactionId}";
            yield return new IncidentCandidate(
                Hash(identity), "OpenTransaction", transaction.Severity, transaction.SessionId, null,
                snapshot.DatabaseName, transaction.LoginName, transaction.HostName, transaction.ProgramName,
                null, transaction.AgeMilliseconds, transaction.LastSqlText, transaction.Recommendation);
        }
    }

    internal static IReadOnlyCollection<IncidentCandidate> DeduplicateIncidentCandidates(
        IEnumerable<IncidentCandidate> candidates) =>
        candidates
            .GroupBy(item => item.Fingerprint, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => SeverityRank(item.Severity))
                .ThenByDescending(item => item.ElapsedMilliseconds)
                .First())
            .ToArray();

    private static int SeverityRank(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 3,
        "warning" => 2,
        "info" => 1,
        _ => 0
    };

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 }) return true;
        }

        return false;
    }

    private static (string Severity, string Recommendation) AssessRequest(
        string? waitType,
        int? blockingSessionId,
        long elapsedMilliseconds,
        DatabaseMonitoringOptions settings)
    {
        if (blockingSessionId.HasValue)
            return ("critical", "Kilitleyen oturumu ve root blocker'i inceleyin; KILL son secenek olmalidir.");
        if (waitType?.StartsWith("LCK_", StringComparison.OrdinalIgnoreCase) == true)
            return ("critical", "Lock zincirini ve uzun acik transaction'lari inceleyin.");
        if (waitType?.Contains("PAGEIOLATCH", StringComparison.OrdinalIgnoreCase) == true)
            return ("warning", "Disk gecikmesi ve execution plan/index uygunlugunu inceleyin.");
        if (waitType?.StartsWith("WRITELOG", StringComparison.OrdinalIgnoreCase) == true)
            return ("warning", "Transaction log diski ve transaction boyutunu inceleyin.");
        if (waitType?.StartsWith("RESOURCE_SEMAPHORE", StringComparison.OrdinalIgnoreCase) == true)
            return ("warning", "Memory grant ve sorgu planini inceleyin.");
        if (waitType?.StartsWith("THREADPOOL", StringComparison.OrdinalIgnoreCase) == true)
            return ("critical", "Worker thread tuketimini ve paralel sorgulari inceleyin.");
        if (waitType?.StartsWith("ASYNC_NETWORK_IO", StringComparison.OrdinalIgnoreCase) == true)
            return ("warning", "Sonuclari yavas tuketen istemciyi ve ag akislarini inceleyin.");
        if (elapsedMilliseconds >= settings.LongRunningThresholdSeconds * 1000L)
            return ("warning", "Execution plan, okuma sayisi ve filtre/index uygunlugunu inceleyin.");
        return ("info", "Anlik calisan istek; esik asilmadikca mudahale gerekmez.");
    }

    private static DatabaseSessionTerminationDto ToDto(DatabaseSessionTerminationAudit item) =>
        new(item.Id, item.SessionId, item.LoginTime, item.HostProcessId, item.LoginName, item.HostName,
            item.ProgramName, item.DatabaseName, item.SqlText, item.Reason, item.RequestedByUserId,
            item.RequestedAtUtc, item.IsSucceeded, item.CompletedAtUtc, item.Error);

    internal static bool MatchesSessionIdentity(
        DateTime actualLoginTime,
        int? actualHostProcessId,
        string? actualProgramName,
        TerminateDatabaseSessionRequest request)
    {
        var expectedLogin = DateTime.SpecifyKind(request.ExpectedLoginTime, DateTimeKind.Unspecified);
        var actualLogin = DateTime.SpecifyKind(actualLoginTime, DateTimeKind.Unspecified);
        return Math.Abs((actualLogin - expectedLogin).TotalSeconds) <= 1 &&
               actualHostProcessId == request.ExpectedHostProcessId &&
               (string.IsNullOrWhiteSpace(request.ExpectedProgramName) ||
                string.Equals(actualProgramName, request.ExpectedProgramName.Trim(), StringComparison.Ordinal));
    }

    internal static string SanitizeSql(string? sql, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
        var normalized = WhitespaceRegex().Replace(StringLiteralRegex().Replace(sql, "'***'"), " ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string TransactionStateName(int value) => value switch
    {
        0 => "NotInitialized", 1 => "Initialized", 2 => "Active", 3 => "Ended", 4 => "CommitInitiated",
        5 => "Prepared", 6 => "Committed", 7 => "RollingBack", 8 => "RolledBack", _ => $"Unknown:{value}"
    };

    private static int Ordinal(DbDataReader reader, string name) => reader.GetOrdinal(name);
    private static string? GetString(DbDataReader reader, string name) => reader.IsDBNull(Ordinal(reader, name)) ? null : Convert.ToString(reader.GetValue(Ordinal(reader, name)));
    private static int GetInt32(DbDataReader reader, string name) => Convert.ToInt32(reader.GetValue(Ordinal(reader, name)));
    private static int? GetNullableInt32(DbDataReader reader, string name) => reader.IsDBNull(Ordinal(reader, name)) ? null : Convert.ToInt32(reader.GetValue(Ordinal(reader, name)));
    private static long GetInt64(DbDataReader reader, string name) => Convert.ToInt64(reader.GetValue(Ordinal(reader, name)));
    private static decimal GetDecimal(DbDataReader reader, string name) => Convert.ToDecimal(reader.GetValue(Ordinal(reader, name)));
    private static DateTime GetDateTime(DbDataReader reader, string name) => Convert.ToDateTime(reader.GetValue(Ordinal(reader, name)));
    private static bool GetBoolean(DbDataReader reader, string name) => Convert.ToBoolean(reader.GetValue(Ordinal(reader, name)));

    [GeneratedRegex("N?'(?:''|[^'])*'", RegexOptions.CultureInvariant)]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record DatabaseSessionSnapshot(
        int SessionId, DateTime LoginTime, int? HostProcessId, bool IsUserProcess, string? LoginName,
        string? HostName, string? ProgramName, string? DatabaseName, string? SqlText, int MonitorSessionId);

    internal sealed record IncidentCandidate(
        string Fingerprint, string Type, string Severity, int SessionId, int? BlockingSessionId,
        string? DatabaseName, string? LoginName, string? HostName, string? ProgramName, string? WaitType,
        long ElapsedMilliseconds, string? SqlText, string Recommendation);
}
