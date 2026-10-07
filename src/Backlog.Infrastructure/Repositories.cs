using Backlog.Application;
using Backlog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backlog.Infrastructure;

public sealed class UserRepository(BacklogDbContext db) : IUserRepository
{
    public Task<User?> ByIdAsync(Guid id, CancellationToken ct)
    {
        return db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<User?> ByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();

        return db.Users.SingleOrDefaultAsync(x => x.Email == normalized, ct);
    }

    public Task<bool> AnyAsync(CancellationToken ct)
    {
        return db.Users.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<User>> DevelopersAsync(CancellationToken ct)
    {
        return await db.Users.Where(x => x.IsActive && x.Role == Roles.Developer).OrderBy(x => x.Name).ToListAsync(ct);
    }

    public void Add(User user)
    {
        db.Users.Add(user);
    }
}

public sealed class SessionRepository(BacklogDbContext db) : ISessionRepository
{
    public Task<AuthSession?> ByIdAsync(Guid id, CancellationToken ct)
    {
        return db.Sessions.SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public void Add(AuthSession session)
    {
        db.Sessions.Add(session);
    }
}

public sealed class TaskRepository(BacklogDbContext db) : ITaskRepository
{
    private IQueryable<TaskItem> Visible(Actor actor)
    {
        return actor.Role switch
        {
            Roles.Manager => db.Tasks,
            Roles.Treasury => db.Tasks.Where(x => x.CreatorId == actor.Id),
            Roles.Developer => db.Tasks.Where(x => x.TargetDeveloperId == actor.Id || x.CurrentDeveloperId == actor.Id),
            _ => db.Tasks.Where(x => false)
        };
    }

    public async Task<TaskItem?> FindAsync(Guid id, Actor actor, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM tasks WHERE id = {id} FOR UPDATE", ct);
        }

        var task = await Visible(actor).SingleOrDefaultAsync(x => x.Id == id, ct);

        if (task is null)
        {
            return null;
        }

        var stages = await db.Stages.Where(x => x.TaskId == id)
            .Include(x => x.Notes)
            .Include(x => x.Attachments)
            .AsSplitQuery()
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        task.RestoreHistory(stages);

        return task;
    }

    public async Task<TaskPage> ListAsync(Actor actor, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = Visible(actor).AsNoTracking();

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var count = await query.CountAsync(ct);

        var items = await query.OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new TaskPage(items, count, page, pageSize);
    }

    public void Add(TaskItem task)
    {
        db.Tasks.Add(task);

        db.Stages.AddRange(task.Stages);
    }

    public async Task SaveAsync(TaskItem task, CancellationToken ct)
    {
        StageEntry[] pending;

        var detection = db.ChangeTracker.AutoDetectChangesEnabled;

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            pending = task.Stages.Where(stage => db.Entry(stage).State == EntityState.Detached).ToArray();

            foreach (var stage in task.Stages.Except(pending))
            {
                db.Notes.AddRange(stage.Notes.Where(note => db.Entry(note).State == EntityState.Detached).ToArray());

                db.Attachments.AddRange(stage.Attachments.Where(file => db.Entry(file).State == EntityState.Detached).ToArray());
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = detection;
        }

        // Fecha a etapa existente antes de inserir outra; ambos os flushes
        // e a inserção explícita dos materiais ficam na mesma transação.
        await db.SaveChangesAsync(ct);

        if (pending.Length > 0)
        {
            db.Stages.AddRange(pending);

            await db.SaveChangesAsync(ct);
        }
    }
}
