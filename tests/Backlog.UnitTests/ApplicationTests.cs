using Backlog.Api;
using Backlog.Application;
using Backlog.Domain;
using Backlog.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Backlog.UnitTests;

public sealed class ApplicationTests
{
    private const string Password = "unit-test-password-2026";

    private static JwtSettings Settings()
    {
        return new JwtSettings { Key = "test-key-only-at-least-thirty-two-characters-2026" };
    }

    [Fact]
    public void HashUsesSaltAndRejectsWrongPassword()
    {
        var hasher = new SecurePasswordHasher();

        var first = hasher.Hash(Password);

        Assert.NotEqual(first, hasher.Hash(Password));

        Assert.NotEqual(Password, first);

        Assert.True(hasher.Verify(first, Password));

        Assert.False(hasher.Verify(first, "wrong"));

        Assert.False(hasher.Verify("broken-hash", Password));
    }

    [Fact]
    public async Task LoginLogoutAndLiveUserValidation()
    {
        var store = new MemoryStore();

        var clock = new FixedClock();

        var hasher = new SecurePasswordHasher();

        var user = new User("Gestor", "gestor@tests.local", hasher.Hash(Password), Roles.Manager, clock.UtcNow);

        store.Add(user);

        var service = new AuthenticationService(store, store, store, hasher, new JwtTokenIssuer(Settings()), clock);

        var token = await service.LoginAsync(user.Email, Password, default);

        Assert.Single(store.Sessions);

        Assert.Equal(user.Role, (await service.ResolveAsync(user.Id, token.SessionId, default))!.Role);

        await service.LogoutAsync(token.SessionId, default);

        Assert.Null(await service.ResolveAsync(user.Id, token.SessionId, default));

        Assert.Equal(ErrorKind.Unauthorized, (await Assert.ThrowsAsync<DomainException>(() => service.LoginAsync(user.Email, "wrong", default))).Kind);

        var inactive = new User("Inativo", "inactive@tests.local", hasher.Hash(Password), Roles.Developer, clock.UtcNow, false);

        store.Add(inactive);

        Assert.Equal(ErrorKind.Unauthorized, (await Assert.ThrowsAsync<DomainException>(() => service.LoginAsync(inactive.Email, Password, default))).Kind);

        var activeToken = await service.LoginAsync(user.Email, Password, default);

        clock.UtcNow = activeToken.ExpiresAt;

        Assert.Null(await service.ResolveAsync(user.Id, activeToken.SessionId, default));
    }

    [Fact]
    public void JwtValidatesSignatureIssuerAudienceAndExpiration()
    {
        var settings = Settings();

        var now = DateTimeOffset.UtcNow;

        var user = new User("Dev", "dev@tests.local", "hash", Roles.Developer, now);

        var signed = new JwtTokenIssuer(settings).Issue(user, now);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();

        handler.ValidateToken(signed.AccessToken, parameters, out _);

        parameters.ValidIssuer = "wrong";

        Assert.Throws<SecurityTokenInvalidIssuerException>(() => handler.ValidateToken(signed.AccessToken, parameters, out _));

        parameters.ValidIssuer = settings.Issuer;

        parameters.ValidAudience = "wrong";

        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(signed.AccessToken, parameters, out _));

        parameters.ValidAudience = settings.Audience;

        parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("different-test-key-at-least-thirty-two-characters"));

        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(signed.AccessToken, parameters, out _));

        parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));

        var expired = new JwtTokenIssuer(settings).Issue(user, now.AddHours(-1));

        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(expired.AccessToken, parameters, out _));
    }

    [Fact]
    public async Task RegistrationRestrictsRolesAndDuplicateEmail()
    {
        var store = new MemoryStore();

        var service = new UserService(store, store, new SecurePasswordHasher(), new FixedClock());

        var manager = new Actor(Guid.NewGuid(), Roles.Manager);

        foreach (var role in Roles.All)
        {
            var user = await service.RegisterAsync(manager, role, role + "@tests.local", Password, role, true, default);

            Assert.Equal(role, user.Role);

            Assert.NotEqual(Password, user.PasswordHash);
        }

        Assert.Equal(ErrorKind.Conflict, (await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(manager, "Duplicado", "GESTOR@tests.local", Password, Roles.Manager, true, default))).Kind);

        foreach (var role in new[] { Roles.Treasury, Roles.Developer })
        {
            Assert.Equal(ErrorKind.Forbidden, (await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(new Actor(Guid.NewGuid(), role), "Nome", "new@tests.local", Password, role, true, default))).Kind);
        }
    }

    [Fact]
    public async Task CreationAndTransitionUseRepositoriesAndRouteMaterials()
    {
        var store = new MemoryStore();

        var clock = new FixedClock();

        var owner = new User("Tesouraria", "owner@tests.local", "hash", Roles.Treasury, clock.UtcNow);

        var dev = new User("Dev", "dev@tests.local", "hash", Roles.Developer, clock.UtcNow);

        store.Add(owner);

        store.Add(dev);

        var actor = new Actor(owner.Id, owner.Role);

        var developer = new Actor(dev.Id, dev.Role);

        var create = new CreateTaskService(store, store, store, clock);

        var task = await create.CreateAsync(actor, "Título", "Descrição", dev.Id, new("backlog", []), default);

        Assert.Single(task.CurrentStage.Notes);

        Assert.Equal(owner.Id, task.CreatorId);

        var executor = new TransitionExecutor(store, store, clock);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await new StartDevelopmentService(executor).ExecuteAsync(task.Id, developer, task.Version, new("início", []), default);

        Assert.True(store.Locked);

        Assert.Equal("início", Assert.Single(task.CurrentStage.Notes).Text);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await new SendToReviewService(executor).ExecuteAsync(task.Id, developer, task.Version, new("entrega", []), default);

        Assert.Equal(2, task.Stages[1].Notes.Count);

        Assert.Empty(task.CurrentStage.Notes);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await new ReviewTaskService(executor).ExecuteAsync(task.Id, actor, task.Version, "send-to-deployment", new("aprovado", []), default);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await new FinishTaskService(executor).ExecuteAsync(task.Id, actor, task.Version, new("concluído", []), default);

        Assert.Equal("concluído", Assert.Single(task.CurrentStage.Notes).Text);

        Assert.Equal(ErrorKind.Conflict, (await Assert.ThrowsAsync<DomainException>(() => new StartDevelopmentService(executor).ExecuteAsync(task.Id, developer, task.Version, new(null, []), default))).Kind);

        Assert.Equal(ErrorKind.Forbidden, (await Assert.ThrowsAsync<DomainException>(() => create.CreateAsync(developer, "Título", "Descrição", dev.Id, new(null, []), default))).Kind);

        Assert.Equal(ErrorKind.Invalid, (await Assert.ThrowsAsync<DomainException>(() => create.CreateAsync(actor, "Título", "Descrição", owner.Id, new(null, []), default))).Kind);

        var read = new TaskReadService(store, store);

        Assert.Equal(dev.Id, (await read.GetAsync(task.Id, actor, default)).CurrentDeveloper!.Id);

        Assert.Empty((await read.ListAsync(new Actor(Guid.NewGuid(), Roles.Treasury), null, 1, 20, default)).Items);
    }

    [Fact]
    public void ValidatorsAndFilePolicyRejectInvalidInput()
    {
        Assert.False(new LoginValidator().Validate(new LoginRequest("invalid", "")).IsValid);

        Assert.False(new RegisterUserValidator().Validate(new RegisterUserRequest("", "invalid", "short", "unknown")).IsValid);

        Assert.False(new CreateTaskValidator().Validate(new CreateTaskRequest()).IsValid);

        Assert.False(new TransitionValidator().Validate(new TransitionRequest()).IsValid);

        Assert.False(new ReviewValidator().Validate(new ReviewRequest { ExpectedVersion = 1, Decision = "return-to-development" }).IsValid);

        Assert.True(new ReviewValidator().Validate(new ReviewRequest { ExpectedVersion = 1, Decision = "return-to-development", Note = "ajustar" }).IsValid);

        Assert.False(new NoteValidator().Validate(new NoteRequest(0, "")).IsValid);

        Assert.False(new TaskQueryValidator().Validate(new TaskQuery(PageSize: 101)).IsValid);

        Assert.Equal(ErrorKind.TooLarge, Assert.Throws<DomainException>(() => MaterialWriter.ValidateFile(new("large.txt", "text/plain", new byte[MaterialWriter.MaxFileBytes + 1]))).Kind);

        Assert.Equal(ErrorKind.Invalid, Assert.Throws<DomainException>(() => MaterialWriter.ValidateFile(new("../bad.txt", "text/plain", "texto"u8.ToArray()))).Kind);

        Assert.Equal(ErrorKind.Invalid, Assert.Throws<DomainException>(() => MaterialWriter.ValidateFile(new("fake.pdf", "application/pdf", "texto"u8.ToArray()))).Kind);

        MaterialWriter.ValidateFile(new("ok.pdf", "application/pdf", "%PDF-1.7"u8.ToArray()));
    }
}
