using Backlog.Application;
using Backlog.Domain;

namespace Backlog.UnitTests;

internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-10-07T12:00:00Z");
}

internal sealed class MemoryStore : IUserRepository, ISessionRepository, ITaskRepository, IUnitOfWork
{
    public List<User> Users { get; } = [];

    public List<AuthSession> Sessions { get; } = [];

    public List<TaskItem> Tasks { get; } = [];

    public int Saves { get; private set; }

    public bool Locked { get; private set; }

    public Task<User?> ByIdAsync(Guid id, CancellationToken ct)
    {
        return Task.FromResult(Users.SingleOrDefault(user => user.Id == id));
    }

    public Task<User?> ByEmailAsync(string email, CancellationToken ct)
    {
        return Task.FromResult(Users.SingleOrDefault(user => user.Email == email.Trim().ToLowerInvariant()));
    }

    public Task<bool> AnyAsync(CancellationToken ct)
    {
        return Task.FromResult(Users.Count != 0);
    }

    public Task<IReadOnlyList<User>> DevelopersAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<User>>(Users.Where(user => user.IsActive && user.Role == Roles.Developer).ToArray());
    }

    public void Add(User user)
    {
        Users.Add(user);
    }

    Task<AuthSession?> ISessionRepository.ByIdAsync(Guid id, CancellationToken ct)
    {
        return Task.FromResult(Sessions.SingleOrDefault(session => session.Id == id));
    }

    public void Add(AuthSession session)
    {
        Sessions.Add(session);
    }

    public Task<TaskItem?> FindAsync(Guid id, Actor actor, bool forUpdate, CancellationToken ct)
    {
        Locked = forUpdate;

        return Task.FromResult(Tasks.SingleOrDefault(task => task.Id == id && task.CanRead(actor)));
    }

    public Task<TaskPage> ListAsync(Actor actor, string? status, int page, int pageSize, CancellationToken ct)
    {
        var visible = Tasks.Where(task => task.CanRead(actor) && (status is null || task.Status == status)).ToArray();

        return Task.FromResult(new TaskPage(visible.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), visible.Length, page, pageSize));
    }

    public void Add(TaskItem task)
    {
        Tasks.Add(task);
    }

    public Task SaveAsync(TaskItem task, CancellationToken ct)
    {
        Saves++;

        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;

        return Task.CompletedTask;
    }

    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        return operation();
    }
}
