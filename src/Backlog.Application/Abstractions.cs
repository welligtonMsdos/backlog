using Backlog.Domain;

namespace Backlog.Application;

public interface IUserRepository
{
    Task<User?> ByIdAsync(Guid id, CancellationToken ct);

    Task<User?> ByEmailAsync(string email, CancellationToken ct);

    Task<bool> AnyAsync(CancellationToken ct);

    Task<IReadOnlyList<User>> DevelopersAsync(CancellationToken ct);

    void Add(User user);
}

public interface ISessionRepository
{
    Task<AuthSession?> ByIdAsync(Guid id, CancellationToken ct);

    void Add(AuthSession session);
}

public interface ITaskRepository
{
    Task<TaskItem?> FindAsync(Guid id, Actor actor, bool forUpdate, CancellationToken ct);

    Task<TaskPage> ListAsync(Actor actor, string? status, int page, int pageSize, CancellationToken ct);

    void Add(TaskItem task);

    Task SaveAsync(TaskItem task, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}

public interface ITokenIssuer
{
    SignedToken Issue(User user, DateTimeOffset now);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed record SignedToken(string AccessToken, DateTimeOffset ExpiresAt, Guid SessionId);

public sealed record TaskPage(IReadOnlyList<TaskItem> Items, int Total, int Page, int PageSize);

public sealed record UploadedFile(string Name, string ContentType, byte[] Content);

public sealed record TaskMaterials(string? Note, IReadOnlyList<UploadedFile> Files);
