using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class OfflineLogicalRequestLock
{
    private const int LockTimeoutMilliseconds = 30000;
    private const int LocalLockPartitionCount = 256;
    private static readonly SemaphoreSlim[] LocalLocks = Enumerable
        .Range(0, LocalLockPartitionCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public static async Task<IAsyncDisposable> AcquireAsync(
        AuthDbContext dbContext,
        string operationCode,
        Guid requestedByUserId,
        int warehouseNo,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var rawKey = $"{operationCode}|{requestedByUserId:D}|{warehouseNo}|{requestFingerprint}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        var keyHash = Convert.ToHexString(hashBytes);
        var resource = $"FurpaMerkezApi:OfflineLogicalRequest:{keyHash}";
        var partition = BitConverter.ToUInt32(hashBytes, 0) % LocalLockPartitionCount;
        var localLock = LocalLocks[partition];
        await localLock.WaitAsync(cancellationToken);

        if (!dbContext.Database.IsSqlServer())
        {
            return new LocalLockLease(localLock);
        }

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        try
        {
            if (closeConnection)
            {
                await connection.OpenAsync(cancellationToken);
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
            command.CommandTimeout = 40;
            AddParameter(command, "@resource", DbType.String, resource);
            AddParameter(command, "@lockTimeout", DbType.Int32, LockTimeoutMilliseconds);

            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                throw new TimeoutException("Ayni icerikteki guvenli yazma kilidi bekleme suresinde alinamadi.");
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
