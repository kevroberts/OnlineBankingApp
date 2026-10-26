using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OnlineBankingAPI.Data;

namespace OnlineBankingAPI.Tests;

/// <summary>
/// Runs the API against a throwaway PostgreSQL database, migrated and seeded at startup.
/// Set OBK_TEST_DB to a server connection string (no Database=) to point at another server.
/// </summary>
public class BankApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string ServerConnection =
        Environment.GetEnvironmentVariable("OBK_TEST_DB")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private readonly string _database = $"onlinebanking_test_{Guid.NewGuid():N}";

    public TestClock Clock { get; } = new();

    public string ConnectionString => new NpgsqlConnectionStringBuilder(ServerConnection) { Database = _database }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:BankDb", ConnectionString);
        builder.UseSetting("Database:MigrateAndSeedOnStartup", "true");
        builder.UseSetting("Bank:LoginsPerMinutePerIp", "10000");
        builder.UseSetting("Bank:RequestsPerMinutePerSession", "10000");
        // Never call the real model from tests, even when ANTHROPIC_API_KEY is set on the machine.
        builder.UseSetting("Bank:Assistant:Provider", "Mock");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public ValueTask InitializeAsync()
    {
        _ = Server; // Starts the host: migrates and seeds.
        return ValueTask.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(ServerConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
        GC.SuppressFinalize(this);
    }

    public HttpClient Anonymous() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>A client that signs in and sends the session token as a bearer token.</summary>
    public async Task<HttpClient> SignInAsync(string username)
    {
        var client = Anonymous();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = DbSeeder.DemoPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("sessionToken").GetString());
        return client;
    }

    public async Task WithDbAsync(Func<BankDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<BankDbContext>());
    }
}

/// <summary>A clock tests can move forward.</summary>
public class TestClock : TimeProvider
{
    private TimeSpan _offset;
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + _offset;
    public void Advance(TimeSpan by) => _offset += by;
    public void Reset() => _offset = TimeSpan.Zero;
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<BankApiFactory>
{
    public const string Name = "api";
}
