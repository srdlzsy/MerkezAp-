using FurpaMerkezApi.Infrastructure.Persistence.Puan;
using FurpaMerkezApi.Infrastructure.Persistence.Puan.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Persistence;

public sealed class PuanDbContextModelTests
{
    [Fact]
    public void InterbonusIndirimCek_DisablesSqlOutputClauseForTriggeredTable()
    {
        var options = new DbContextOptionsBuilder<PuanDbContext>()
            .UseSqlServer("Server=(local);Database=PuanModelTest;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PuanDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(InterbonusIndirimCek));

        Assert.NotNull(entityType);
        Assert.False(entityType.IsSqlOutputClauseUsed());
    }

    [Fact]
    public void InterbonusIndirimCek_UsesDatabaseDecimalPrecision()
    {
        var options = new DbContextOptionsBuilder<PuanDbContext>()
            .UseSqlServer("Server=(local);Database=PuanModelTest;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new PuanDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(InterbonusIndirimCek));

        Assert.NotNull(entityType);
        foreach (var propertyName in new[] { "Tutar", "Puan", "Belge_Tutar" })
        {
            var property = entityType.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(18, property.GetPrecision());
            Assert.Equal(2, property.GetScale());
        }
    }
}
