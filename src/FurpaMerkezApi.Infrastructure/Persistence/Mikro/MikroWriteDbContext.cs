using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro;

public sealed class MikroWriteDbContext(DbContextOptions<MikroWriteDbContext> options)
    : MikroDbContext(options)
{
}
