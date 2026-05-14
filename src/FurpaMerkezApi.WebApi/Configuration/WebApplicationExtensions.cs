using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.WebApi.Configuration;

public static class WebApplicationExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var startupTasks = app.Configuration.GetSection("StartupTasks").Get<StartupTasksOptions>() ?? new StartupTasksOptions();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        if (startupTasks.ApplyAuthMigrations)
        {
            await dbContext.Database.MigrateAsync();
        }

        if (startupTasks.SynchronizePermissionCatalog)
        {
            await dbContext.SynchronizePermissionCatalogAsync();
        }

        if (startupTasks.SynchronizeWarehouseUsers)
        {
            var mikroDbContext = scope.ServiceProvider.GetRequiredService<MikroDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            await dbContext.SynchronizeWarehouseUsersAsync(mikroDbContext, passwordHasher);
        }
    }
}
