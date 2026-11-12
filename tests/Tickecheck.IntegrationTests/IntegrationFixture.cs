using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Tickecheck.Application.Auth;
using Tickecheck.Infrastructure.Persistence;
using Tickecheck.Infrastructure.Persistence.Seed;

namespace Tickecheck.IntegrationTests;

/// <summary>
/// Starts one real SQL Server in Docker for the whole test run and the API in memory
/// on top of it. A real server is used (not an in-memory provider) so the tests
/// exercise the same SQL, indexes and foreign keys as production.
/// </summary>
public class IntegrationFixture : IAsyncLifetime
{
    // Same JSON settings as the API: enums travel as text.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The API hosted in memory: requests go straight to it, without a network port.</summary>
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Program.cs reads its configuration while it starts, so the values must exist
        // before the host is created. Environment variables are the simplest way.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-tests-signing-key-0123456789abcdef");
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("Database__SeedDemoData", "true");

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

        // Creating a client starts the API, which applies the migrations and seeds the demo data.
        Factory.CreateClient().Dispose();
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

    /// <summary>
    /// Logs in through the real endpoint and returns the tokens.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(string email)
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, DbSeeder.DemoPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    /// <summary>
    /// Returns an HTTP client that sends the access token of the given demo user.
    /// </summary>
    public async Task<HttpClient> CreateClientAsync(string email)
    {
        var tokens = await LoginAsync(email);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
    public const string Name = "Integration";
}

/// <summary>
/// Emails of the users created by DbSeeder. All of them use DbSeeder.DemoPassword.
/// </summary>
public static class DemoUsers
{
    public const string SuperAdmin = DbSeeder.SuperAdminEmail;

    public const string AcmeAdmin = "admin@acme.demo";
    public const string AcmeAgent1 = "agent1@acme.demo";
    public const string AcmeAgent2 = "agent2@acme.demo";
    public const string AcmeUser1 = "user1@acme.demo";
    public const string AcmeUser2 = "user2@acme.demo";

    public const string GlobexAdmin = "admin@globex.demo";
    public const string GlobexAgent1 = "agent1@globex.demo";
    public const string GlobexUser1 = "user1@globex.demo";
}
