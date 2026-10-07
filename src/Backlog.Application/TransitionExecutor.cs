using Backlog.Domain;

namespace Backlog.Application;

public sealed class TransitionExecutor(ITaskRepository tasks, IUnitOfWork unit, IClock clock)
{
    public Task<TaskItem> ExecuteAsync(Guid id, Actor actor, long version, string source, string target, TaskMaterials materials, CancellationToken ct)
    {
        return unit.ExecuteAsync(async () =>
        {
            var task = await tasks.FindAsync(id, actor, true, ct)
                ?? throw new DomainException(ErrorKind.NotFound, "Tarefa não encontrada.");

            if (task.Status != source)
            {
                throw new DomainException(ErrorKind.Conflict, "Ação incompatível com o status atual.");
            }

            var now = clock.UtcNow;

            var stage = task.Transition(target, actor, version, now, materials.Note);

            MaterialWriter.Add(stage, materials, actor, now);

            await tasks.SaveAsync(task, ct);

            return task;
        }, ct);
    }
}

public sealed class StartDevelopmentService(TransitionExecutor executor)
{
    public Task<TaskItem> ExecuteAsync(Guid id, Actor actor, long version, TaskMaterials materials, CancellationToken ct)
    {
        return executor.ExecuteAsync(id, actor, version, TaskStatuses.Backlog, TaskStatuses.Development, materials, ct);
    }
}
