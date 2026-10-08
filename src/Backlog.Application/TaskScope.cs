using Backlog.Domain;

namespace Backlog.Application;

public sealed record TaskScope(bool All, Guid? CreatorId, Guid? DeveloperId)
{
    public static TaskScope For(Actor actor)
    {
        return actor.Role switch
        {
            Roles.Manager => new(true, null, null),
            Roles.Treasury => new(false, actor.Id, null),
            Roles.Developer => new(false, null, actor.Id),
            _ => throw new DomainException(ErrorKind.Forbidden, "Perfil sem acesso a tarefas.")
        };
    }

    public bool Contains(TaskItem task)
    {
        return All || CreatorId == task.CreatorId || DeveloperId.HasValue &&
            (DeveloperId == task.TargetDeveloperId || DeveloperId == task.CurrentDeveloperId);
    }
}
