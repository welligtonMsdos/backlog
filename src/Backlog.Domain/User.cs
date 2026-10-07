namespace Backlog.Domain;

public sealed class User
{
    private User() { }

    public User(string name, string email, string passwordHash, string role, DateTimeOffset now, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name) || !Roles.All.Contains(role))
        {
            throw new DomainException(ErrorKind.Invalid, "Nome ou perfil inválido.");
        }

        Id = Guid.NewGuid();

        Name = name.Trim();

        Email = email.Trim().ToLowerInvariant();

        PasswordHash = passwordHash;

        Role = role;

        IsActive = isActive;

        CreatedAt = now;

        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Email { get; private set; } = "";

    public string PasswordHash { get; private set; } = "";

    public string Role { get; private set; } = "";

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}

public sealed class AuthSession
{
    private AuthSession() { }

    public AuthSession(Guid id, Guid userId, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        Id = id;

        UserId = userId;

        CreatedAt = now;

        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
