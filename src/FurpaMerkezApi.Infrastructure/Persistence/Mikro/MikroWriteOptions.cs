namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro;

public sealed record MikroWriteOptions(
    string ConnectionString,
    string ConnectionStringName);
