using Backlog.Domain;

namespace Backlog.Application;

public sealed class AuthenticationService(
    IUserRepository users,
    ISessionRepository sessions,
    IUnitOfWork unit,
    IPasswordHasher passwords,
    ITokenIssuer tokens,
    IClock clock)
{
    public async Task<SignedToken> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.ByEmailAsync(email, ct);

        if (user is null || !user.IsActive || !passwords.Verify(user.PasswordHash, password))
        {
            throw new DomainException(ErrorKind.Unauthorized, "Credenciais inválidas.");
        }

        var token = tokens.Issue(user, clock.UtcNow);

        sessions.Add(new AuthSession(token.SessionId, user.Id, clock.UtcNow, token.ExpiresAt));

        await unit.SaveAsync(ct);

        return token;
    }

    public async Task LogoutAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.ByIdAsync(sessionId, ct)
            ?? throw new DomainException(ErrorKind.Unauthorized, "Sessão inválida.");

        session.Revoke(clock.UtcNow);

        await unit.SaveAsync(ct);
    }

    public async Task<Actor?> ResolveAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.ByIdAsync(sessionId, ct);

        var user = await users.ByIdAsync(userId, ct);

        if (session is null || session.UserId != userId || session.RevokedAt.HasValue
            || session.ExpiresAt <= clock.UtcNow || user is null || !user.IsActive)
        {
            return null;
        }

        return new Actor(user.Id, user.Role);
    }
}
