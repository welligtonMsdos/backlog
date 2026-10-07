using Backlog.Domain;

namespace Backlog.Application;

public sealed class SendToReviewService(TransitionExecutor executor)
{
    public Task<TaskItem> ExecuteAsync(Guid id, Actor actor, long version, TaskMaterials materials, CancellationToken ct)
    {
        return executor.ExecuteAsync(id, actor, version, TaskStatuses.Development, TaskStatuses.Review, materials, ct);
    }
}
