using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backlog.Api;
using Backlog.Application;
using Backlog.Domain;
using Backlog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Backlog.IntegrationTests;

public sealed class AuthenticationTests : ApiTestBase
{
    [Fact]
    public async Task ManagerRegistersEveryRoleAndOtherRolesCannotRegister()
    {
        using var manager = await Api.LoginAsync();

        foreach (var role in Roles.All)
        {
            var registered = await Api.RegisterAsync(manager, role);

            Assert.Equal(role, registered.Role);

            using var user = await Api.LoginAsync(registered.Email);

            var me = await user.GetFromJsonAsync<UserResponse>("/users/me");

            Assert.Equal(registered.Id, me!.Id);

            Assert.Equal(role, me.Role);

            if (role != Roles.Manager)
            {
                var response = await user.PostAsJsonAsync("/users", new RegisterUserRequest("Negado", "denied@tests.local", ApiHarness.Password, Roles.Manager));

                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }

        var anonymous = await Api.Client.PostAsJsonAsync("/users", new RegisterUserRequest("Negado", "anonymous@tests.local", ApiHarness.Password, Roles.Manager));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var duplicate = await manager.PostAsJsonAsync("/users", new RegisterUserRequest("Duplicado", "MANAGER@tests.local", ApiHarness.Password, Roles.Manager));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await manager.PostAsJsonAsync("/users", new RegisterUserRequest("", "bad", "short", "unknown"));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task LoginAndLogoutValidateLiveSessionAndInactiveUser()
    {
        using var manager = await Api.LoginAsync();

        var invalid = await Api.Client.PostAsJsonAsync("/auth/login", new LoginRequest("manager@tests.local", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        var inactive = await Api.RegisterAsync(manager, Roles.Developer, isActive: false);

        var disabled = await Api.Client.PostAsJsonAsync("/auth/login", new LoginRequest(inactive.Email, ApiHarness.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);

        var response = await manager.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await manager.GetAsync("/users/me")).StatusCode);
    }

    [Fact]
    public async Task CurrentProfileAndActivityOverrideTokenClaims()
    {
        using var manager = await Api.LoginAsync();

        var user = await Api.RegisterAsync(manager, Roles.Developer);

        using var developer = await Api.LoginAsync(user.Email);

        await ApiHarness.ExecuteAsync(Api.ConnectionString, $"UPDATE users SET role = 'tesouraria' WHERE id = '{user.Id}'");

        Assert.Equal(Roles.Treasury, (await developer.GetFromJsonAsync<UserResponse>("/users/me"))!.Role);

        Assert.Equal(HttpStatusCode.OK, (await developer.GetAsync("/users/developers")).StatusCode);

        await ApiHarness.ExecuteAsync(Api.ConnectionString, $"UPDATE users SET is_active = false WHERE id = '{user.Id}'");

        Assert.Equal(HttpStatusCode.Unauthorized, (await developer.GetAsync("/users/me")).StatusCode);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public async Task InvalidTokensAreRejected(string variant)
    {
        var settings = new JwtSettings
        {
            Key = variant == "signature" ? "wrong-signature-test-key-at-least-thirty-two-characters" : ApiHarness.JwtKey,
            Issuer = variant == "issuer" ? "wrong" : "backlog-api",
            Audience = variant == "audience" ? "wrong" : "backlog-client"
        };

        var now = DateTimeOffset.UtcNow;

        var token = new JwtTokenIssuer(settings).Issue(new User("Test", "test@tests.local", "hash", Roles.Manager, now), variant == "expired" ? now.AddHours(-1) : now);

        using var client = Api.Factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/users/me")).StatusCode);
    }

    [Fact]
    public async Task BootstrapIsIdempotentSerializedAndDoesNotChangePassword()
    {
        await ApiHarness.ExecuteAsync(Api.ConnectionString, "DELETE FROM users");

        using var first = Api.Factory.Services.CreateScope();

        using var second = Api.Factory.Services.CreateScope();

        await Task.WhenAll(first.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(default), second.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(default));

        await Api.RestartAsync();

        using var manager = await Api.LoginAsync();

        await using var connection = new NpgsqlConnection(Api.ConnectionString);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT count(*) FROM users WHERE role = 'gestor'", connection);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task MissingBootstrapSecretFailsOnEmptyDatabase()
    {
        await Api.ResetDatabaseAsync();

        Api.Client.Dispose();

        await Api.Factory.DisposeAsync();

        var exception = Assert.ThrowsAny<Exception>(() => Api.Start(""));

        Assert.Contains("gestor", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LostDatabaseRequiresApiRestartAndRecreatesOnlyManager()
    {
        using var manager = await Api.LoginAsync();

        await Api.RegisterAsync(manager, Roles.Treasury);

        await Api.ResetDatabaseAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Api.Client.GetAsync("/health/ready")).StatusCode);

        await Api.RestartAsync();

        using var stale = Api.Factory.CreateClient();

        stale.DefaultRequestHeaders.Authorization = manager.DefaultRequestHeaders.Authorization;

        Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync("/users/me")).StatusCode);

        using var recovered = await Api.LoginAsync();

        await using var connection = new NpgsqlConnection(Api.ConnectionString);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT count(*) FROM users", connection);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }
}
