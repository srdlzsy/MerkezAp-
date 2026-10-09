using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal static class MikroDocumentSequenceLock
{
    private const int LockTimeoutMilliseconds = 30000;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> LocalLocks = new(StringComparer.Ordinal);

    public static async Task<IAsyncDisposable> AcquireAsync(
        MikroWriteDbContext dbContext,
        string operationCode,
        string documentSerie,
        CancellationToken cancellationToken)
    {
        return await AcquireCoreAsync(
            dbContext,
            operationCode,
            documentSerie,
            localWaitTimeout: null,
            LockTimeoutMilliseconds,
            cancellationToken);
    }

    public static async Task<IAsyncDisposable> AcquireAsync(
        MikroWriteDbContext dbContext,
        string operationCode,
        string documentSerie,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        if (waitTimeout <= TimeSpan.Zero || waitTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(waitTimeout));
        }

        var timeoutMilliseconds = (int)Math.Ceiling(waitTimeout.TotalMilliseconds);
        return await AcquireCoreAsync(
            dbContext,
            operationCode,
            documentSerie,
            waitTimeout,
            timeoutMilliseconds,
            cancellationToken);
    }

    private static async Task<IAsyncDisposable> AcquireCoreAsync(
        MikroWriteDbContext dbContext,
        string operationCode,
        string documentSerie,
        TimeSpan? localWaitTimeout,
        int sqlLockTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var resource = $"FurpaMerkezApi:DocumentSequence:{operationCode}:{documentSerie}";
        var localLock = LocalLocks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        var waitStopwatch = Stopwatch.StartNew();
        var localLockAcquired = localWaitTimeout.HasValue
            ? await localLock.WaitAsync(localWaitTimeout.Value, cancellationToken)
            : await WaitWithoutTimeoutAsync(localLock, cancellationToken);

        if (!localLockAcquired)
        {
            throw new TimeoutException($"Mikro application lock wait timed out for resource '{resource}'.");
        }

        if (!dbContext.Database.IsSqlServer())
        {
            return new LocalLockLease(localLock);
        }

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        try
        {
            var effectiveSqlLockTimeoutMilliseconds = localWaitTimeout.HasValue
                ? sqlLockTimeoutMilliseconds - (int)Math.Min(waitStopwatch.ElapsedMilliseconds, int.MaxValue)
                : sqlLockTimeoutMilliseconds;
            if (effectiveSqlLockTimeoutMilliseconds <= 0)
            {
                throw new TimeoutException($"Mikro application lock wait timed out for resource '{resource}'.");
            }

            if (closeConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            if (localWaitTimeout.HasValue)
            {
                effectiveSqlLockTimeoutMilliseconds =
                    sqlLockTimeoutMilliseconds - (int)Math.Min(waitStopwatch.ElapsedMilliseconds, int.MaxValue);
                if (effectiveSqlLockTimeoutMilliseconds <= 0)
                {
                    throw new TimeoutException($"Mikro application lock wait timed out for resource '{resource}'.");
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Session',
                    @LockTimeout = @lockTimeout;
                SELECT @result;
                """;
            command.CommandTimeout = (effectiveSqlLockTimeoutMilliseconds / 1000) + 10;
            AddParameter(command, "@resource", DbType.String, resource);
            AddParameter(command, "@lockTimeout", DbType.Int32, effectiveSqlLockTimeoutMilliseconds);

            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                throw new TimeoutException(
                    $"Mikro document sequence lock could not be acquired. SQL result: {result}.");
            }

            return new SqlLockLease(connection, closeConnection, localLock, resource);
        }
        catch
        {
            if (closeConnection && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }

            localLock.Release();
            throw;
        }
    }

    private static async Task<bool> WaitWithoutTimeoutAsync(
        SemaphoreSlim localLock,
        CancellationToken cancellationToken)
    {
        await localLock.WaitAsync(cancellationToken);
        return true;
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed class LocalLockLease(SemaphoreSlim localLock) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            localLock.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SqlLockLease(
        DbConnection connection,
        bool closeConnection,
        SemaphoreSlim localLock,
        string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    EXEC sys.sp_releaseapplock
                        @Resource = @resource,
                        @LockOwner = 'Session';
                    """;
                AddParameter(command, "@resource", DbType.String, resource);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally
            {
                if (closeConnection && connection.State != ConnectionState.Closed)
                {
                    await connection.CloseAsync();
                }

                localLock.Release();
            }
        }
    }
}
