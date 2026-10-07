using Backlog.Domain;

namespace Backlog.Application;

public sealed class CreateTaskService(IUserRepository users, ITaskRepository tasks, IUnitOfWork unit, IClock clock)
{
    public async Task<TaskItem> CreateAsync(Actor actor, string title, string description, Guid developerId, TaskMaterials materials, CancellationToken ct)
    {
        if (actor.Role != Roles.Treasury)
        {
            throw new DomainException(ErrorKind.Forbidden, "Apenas Tesouraria cria tarefas.");
        }

        var developer = await users.ByIdAsync(developerId, ct);

        if (developer is null || !developer.IsActive || developer.Role != Roles.Developer)
        {
            throw new DomainException(ErrorKind.Invalid, "Destinatário deve ser desenvolvedor ativo.");
        }

        return await unit.ExecuteAsync(async () =>
        {
            var task = new TaskItem(title, description, actor.Id, developerId, clock.UtcNow);

            MaterialWriter.Add(task.CurrentStage, materials, actor, clock.UtcNow);

            tasks.Add(task);

            await unit.SaveAsync(ct);

            return task;
        }, ct);
    }
}
