using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Tickecheck.Infrastructure.Persistence;

namespace Tickecheck.IntegrationTests;

/// <summary>
/// Starts one real SQL Server in Docker for the whole test run and applies the migrations.
/// A real server is used (not an in-memory provider) so the tests exercise the same
/// SQL, indexes and foreign keys as production.
/// </summary>
public class IntegrationFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext(tenantId: null);
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates a DbContext that behaves as if the current user belonged to the given tenant.
    /// </summary>
    public AppDbContext CreateDbContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options, new FixedTenantProvider(tenantId));
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    public const string Name = "Integration";
}
