using Backlog.Domain;

namespace Backlog.Application;

public sealed class UserService(IUserRepository users, IUnitOfWork unit, IPasswordHasher passwords, IClock clock)
{
    public async Task<User> RegisterAsync(Actor actor, string name, string email, string password, string role, bool isActive, CancellationToken ct)
    {
        if (actor.Role != Roles.Manager)
        {
            throw new DomainException(ErrorKind.Forbidden, "Apenas gestores cadastram usuários.");
        }

        if (password.Length is < 12 or > 128 || !Roles.All.Contains(role))
        {
            throw new DomainException(ErrorKind.Invalid, "Senha ou perfil inválido.");
        }

        if (await users.ByEmailAsync(email, ct) is not null)
        {
            throw new DomainException(ErrorKind.Conflict, "E-mail já cadastrado.");
        }

        var user = new User(name, email, passwords.Hash(password), role, clock.UtcNow, isActive);

        users.Add(user);

        await unit.SaveAsync(ct);

        return user;
    }

    public async Task<User> GetAsync(Guid id, CancellationToken ct)
    {
        return await users.ByIdAsync(id, ct)
            ?? throw new DomainException(ErrorKind.NotFound, "Usuário não encontrado.");
    }

    public Task<IReadOnlyList<User>> DevelopersAsync(Actor actor, CancellationToken ct)
    {
        if (actor.Role != Roles.Treasury)
        {
            throw new DomainException(ErrorKind.Forbidden, "Acesso restrito à Tesouraria.");
        }

        return users.DevelopersAsync(ct);
    }
}
