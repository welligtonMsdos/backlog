using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backlog.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Backlog.IntegrationTests;

internal sealed class ApiHarness : IAsyncLifetime
{
    public const string Password = "integration-test-password-2026";

    public const string JwtKey = "integration-only-key-at-least-thirty-two-characters";

    private readonly string databaseName = "backlog_it_" + Guid.NewGuid().ToString("N");

    private string adminConnection = "";

    public string ConnectionString { get; private set; } = "";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE")
            ?? throw new InvalidOperationException("Execute testes de integração pelo serviço tests do Docker Compose.");

        var connection = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };

        adminConnection = connection.ConnectionString;

        await ExecuteAsync(adminConnection, $"CREATE DATABASE \"{databaseName}\"");

        connection.Database = databaseName;

        ConnectionString = connection.ConnectionString;

        Start();
    }

    public void Start(string? bootstrapPassword = Password)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Database", ConnectionString);

        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);

        Environment.SetEnvironmentVariable("Jwt__Issuer", "backlog-api");

        Environment.SetEnvironmentVariable("Jwt__Audience", "backlog-client");

        Environment.SetEnvironmentVariable("Bootstrap__Name", "Gestor de teste");

        Environment.SetEnvironmentVariable("Bootstrap__Email", "manager@tests.local");

        Environment.SetEnvironmentVariable("Bootstrap__Password", bootstrapPassword);

        Environment.SetEnvironmentVariable("Documentation__Enabled", "true");

        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Error");

        Factory = new WebApplicationFactory<Program>();

        Client = Factory.CreateClient();
    }

    public async Task RestartAsync()
    {
        Client.Dispose();

        await Factory.DisposeAsync();

        Start();
    }

    public async Task ResetDatabaseAsync()
    {
        await ExecuteAsync(ConnectionString, "DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
    }

    public async Task<HttpClient> LoginAsync(string email = "manager@tests.local")
    {
        var response = await Client.PostAsJsonAsync("/auth/login", new LoginRequest(email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var client = Factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        return client;
    }

    public async Task<UserResponse> RegisterAsync(HttpClient manager, string role, string? email = null, bool isActive = true)
    {
        var request = new RegisterUserRequest("Usuário " + role, email ?? Guid.NewGuid().ToString("N") + "@tests.local", Password, role, isActive);

        var response = await manager.PostAsJsonAsync("/users", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await ExecuteAsync(adminConnection, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync();
    }

    internal sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);
}

public abstract class ApiTestBase : IAsyncLifetime
{
    internal ApiHarness Api { get; } = new();

    public Task InitializeAsync()
    {
        return Api.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return Api.DisposeAsync();
    }
}

public sealed class InfrastructureTests : ApiTestBase
{
    [Fact]
    public async Task RealPostgresRunsMigrationsAndServesReadiness()
    {
        var response = await Api.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var connection = new NpgsqlConnection(Api.ConnectionString);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT count(*) FROM users", connection);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}
