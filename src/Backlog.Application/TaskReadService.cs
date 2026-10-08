using Backlog.Domain;

namespace Backlog.Application;

public sealed record TaskView(TaskItem Task, User Creator, User TargetDeveloper, User? CurrentDeveloper);

public sealed record TaskViewPage(IReadOnlyList<TaskView> Items, int Total, int Page, int PageSize);

public sealed class TaskReadService(ITaskRepository tasks, IUserRepository users)
{
    public async Task<TaskView> GetAsync(Guid id, Actor actor, CancellationToken ct)
    {
        var task = await tasks.FindAsync(id, TaskScope.For(actor), false, ct)
            ?? throw new DomainException(ErrorKind.NotFound, "Tarefa não encontrada.");

        return await ViewAsync(task, ct);
    }

    public async Task<TaskViewPage> ListAsync(Actor actor, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || status is not null && !TaskStatuses.All.Contains(status))
        {
            throw new DomainException(ErrorKind.Invalid, "Paginação ou status inválido.");
        }

        var result = await tasks.ListAsync(TaskScope.For(actor), status, page, pageSize, ct);

        var items = new List<TaskView>();

        foreach (var task in result.Items)
        {
            items.Add(await ViewAsync(task, ct));
        }

        return new TaskViewPage(items, result.Total, result.Page, result.PageSize);
    }

    private async Task<TaskView> ViewAsync(TaskItem task, CancellationToken ct)
    {
        var creator = await users.ByIdAsync(task.CreatorId, ct);

        var target = await users.ByIdAsync(task.TargetDeveloperId, ct);

        var current = task.CurrentDeveloperId.HasValue ? await users.ByIdAsync(task.CurrentDeveloperId.Value, ct) : null;

        return new TaskView(task, creator!, target!, current);
    }
}
