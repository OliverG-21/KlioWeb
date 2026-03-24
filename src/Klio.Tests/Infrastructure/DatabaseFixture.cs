using Microsoft.EntityFrameworkCore;
using Klio.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace Klio.Tests.Infrastructure;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Run EF Core migrations to set up the schema
        using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
