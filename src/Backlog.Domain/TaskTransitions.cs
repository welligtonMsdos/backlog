namespace Backlog.Domain;

public sealed partial class TaskItem
{
    public bool CanRead(Actor actor)
    {
        return actor.Role == Roles.Manager
            || actor.Role == Roles.Treasury && actor.Id == CreatorId
            || actor.Role == Roles.Developer && (actor.Id == TargetDeveloperId || actor.Id == CurrentDeveloperId);
    }

    public bool CanWriteCurrentStage(Actor actor)
    {
        return Status != TaskStatuses.Finished
            && (Status == TaskStatuses.Development
                ? actor.Role == Roles.Developer && actor.Id == CurrentDeveloperId
                : actor.Role == Roles.Treasury && actor.Id == CreatorId);
    }

    public string[] AvailableActions(Actor actor)
    {
        return Status switch
        {
            TaskStatuses.Backlog when actor.Role == Roles.Developer && actor.Id == TargetDeveloperId => ["start"],
            TaskStatuses.Development when actor.Role == Roles.Developer && actor.Id == CurrentDeveloperId => ["send-to-review"],
            TaskStatuses.Review when actor.Role == Roles.Treasury && actor.Id == CreatorId => ["return-to-development", "send-to-deployment"],
            TaskStatuses.Deployment when actor.Role == Roles.Treasury && actor.Id == CreatorId => ["finish"],
            _ => []
        };
    }

    public StageEntry Transition(string target, Actor actor, long expectedVersion, DateTimeOffset now, string? note = null)
    {
        CheckVersion(expectedVersion);

        var action = (Status, target) switch
        {
            (TaskStatuses.Backlog, TaskStatuses.Development) => "start",
            (TaskStatuses.Development, TaskStatuses.Review) => "send-to-review",
            (TaskStatuses.Review, TaskStatuses.Development) => "return-to-development",
            (TaskStatuses.Review, TaskStatuses.Deployment) => "send-to-deployment",
            (TaskStatuses.Deployment, TaskStatuses.Finished) => "finish",
            _ => throw new DomainException(ErrorKind.Conflict, "Transição de status não permitida.")
        };

        if (!AvailableActions(actor).Contains(action))
        {
            throw new DomainException(ErrorKind.Forbidden, "Usuário não pode executar esta ação.");
        }

        if (action == "return-to-development" && string.IsNullOrWhiteSpace(note))
        {
            throw new DomainException(ErrorKind.Invalid, "Informe o motivo da devolução.");
        }

        var outgoing = CurrentStage;

        outgoing.Close(actor.Id, now);

        if (action == "start")
        {
            CurrentDeveloperId = actor.Id;
        }

        Status = target;

        var incoming = new StageEntry(Id, outgoing.Sequence + 1, target, actor.Id, now);

        if (target == TaskStatuses.Finished)
        {
            incoming.Close(actor.Id, now);
        }

        Stages.Add(incoming);

        Touch(now);

        return action is "start" or "finish" ? incoming : outgoing;
    }

    public void CheckVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new DomainException(ErrorKind.Conflict, "A tarefa foi atualizada. Consulte a versão atual.");
        }
    }

    public void Touch(DateTimeOffset now)
    {
        Version++;

        UpdatedAt = now;
    }
}
