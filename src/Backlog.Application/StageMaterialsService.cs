using Backlog.Domain;

namespace Backlog.Application;

public sealed class StageMaterialsService(ITaskRepository tasks, IUnitOfWork unit, IClock clock)
{
    public Task<TaskItem> AddAsync(Guid id, Guid stageId, Actor actor, long version, TaskMaterials materials, CancellationToken ct)
    {
        return unit.ExecuteAsync(async () =>
        {
            var task = await tasks.FindAsync(id, actor, true, ct)
                ?? throw new DomainException(ErrorKind.NotFound, "Tarefa não encontrada.");

            task.CheckVersion(version);

            if (task.CurrentStage.Id != stageId || task.CurrentStage.EndedAt.HasValue)
            {
                throw new DomainException(ErrorKind.Conflict, "A passagem não está vigente.");
            }

            if (!task.CanWriteCurrentStage(actor))
            {
                throw new DomainException(ErrorKind.Forbidden, "Usuário não pode escrever nesta passagem.");
            }

            MaterialWriter.Add(task.CurrentStage, materials, actor, clock.UtcNow);

            task.Touch(clock.UtcNow);

            await tasks.SaveAsync(task, ct);

            return task;
        }, ct);
    }

    public async Task<StageAttachment> DownloadAsync(Guid id, Guid attachmentId, Actor actor, CancellationToken ct)
    {
        var task = await tasks.FindAsync(id, actor, false, ct)
            ?? throw new DomainException(ErrorKind.NotFound, "Tarefa não encontrada.");

        return task.Stages.SelectMany(stage => stage.Attachments).SingleOrDefault(file => file.Id == attachmentId)
            ?? throw new DomainException(ErrorKind.NotFound, "Anexo não encontrado nesta tarefa.");
    }
}
