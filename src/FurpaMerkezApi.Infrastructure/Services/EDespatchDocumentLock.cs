using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace FurpaMerkezApi.Infrastructure.Services;

internal sealed class EDespatchDocumentLock(SqlConnection connection, string resource) : IAsyncDisposable
{
    public static async Task<EDespatchDocumentLock?> TryAcquireAsync(
        string connectionString, string documentKey, CancellationToken cancellationToken)
    {
        var resource = "Furpa:EDespatch:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(documentKey.ToUpperInvariant())));
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
            command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = resource;
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result >= 0) return new EDespatchDocumentLock(connection, resource);
            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
            command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = resource;
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // A pooled SQL session must never retain an application lock.
            SqlConnection.ClearPool(connection);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
