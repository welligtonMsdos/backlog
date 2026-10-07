using Backlog.Domain;

namespace Backlog.Application;

public sealed class ReviewTaskService(TransitionExecutor executor)
{
    public Task<TaskItem> ExecuteAsync(Guid id, Actor actor, long version, string decision, TaskMaterials materials, CancellationToken ct)
    {
        var target = decision switch
        {
            "return-to-development" => TaskStatuses.Development,
            "send-to-deployment" => TaskStatuses.Deployment,
            _ => throw new DomainException(ErrorKind.Invalid, "Decisão inválida.")
        };

        return executor.ExecuteAsync(id, actor, version, TaskStatuses.Review, target, materials, ct);
    }
}
