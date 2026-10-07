using Backlog.Domain;

namespace Backlog.UnitTests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var source in TaskStatuses.All)
        {
            foreach (var target in TaskStatuses.All)
            {
                foreach (var actor in new[] { "owner", "other-owner", "developer", "other-developer", "manager" })
                {
                    yield return [source, target, actor];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void TransitionMatrixEnforcesStateAndIdentity(string source, string target, string actorKind)
    {
        var owner = new Actor(Guid.NewGuid(), Roles.Treasury);

        var developer = new Actor(Guid.NewGuid(), Roles.Developer);

        var task = At(source, owner, developer);

        var actor = actorKind switch
        {
            "owner" => owner,
            "developer" => developer,
            "other-owner" => new Actor(Guid.NewGuid(), Roles.Treasury),
            "other-developer" => new Actor(Guid.NewGuid(), Roles.Developer),
            _ => new Actor(Guid.NewGuid(), Roles.Manager)
        };

        var legalPair = (source, target) is (TaskStatuses.Backlog, TaskStatuses.Development)
            or (TaskStatuses.Development, TaskStatuses.Review)
            or (TaskStatuses.Review, TaskStatuses.Development)
            or (TaskStatuses.Review, TaskStatuses.Deployment)
            or (TaskStatuses.Deployment, TaskStatuses.Finished);

        var legalActor = source is TaskStatuses.Backlog or TaskStatuses.Development ? actorKind == "developer" : actorKind == "owner";

        var version = task.Version;

        var count = task.Stages.Count;

        if (legalPair && legalActor)
        {
            task.Transition(target, actor, version, Now.AddHours(1), "motivo");

            Assert.Equal(target, task.Status);

            Assert.Equal(version + 1, task.Version);

            Assert.Equal(count + 1, task.Stages.Count);
        }
        else
        {
            var error = Assert.Throws<DomainException>(() => task.Transition(target, actor, version, Now.AddHours(1), "motivo"));

            Assert.Equal(legalPair ? ErrorKind.Forbidden : ErrorKind.Conflict, error.Kind);

            Assert.Equal(source, task.Status);

            Assert.Equal(version, task.Version);

            Assert.Equal(count, task.Stages.Count);
        }
    }

    [Fact]
    public void ReturnsPreserveHistoryAndFinishedIsTerminal()
    {
        var owner = new Actor(Guid.NewGuid(), Roles.Treasury);

        var developer = new Actor(Guid.NewGuid(), Roles.Developer);

        var task = At(TaskStatuses.Review, owner, developer);

        var originalDevelopment = task.Stages[1].Id;

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var outgoing = task.Transition(TaskStatuses.Development, owner, task.Version, Now.AddMinutes(10 + cycle * 2), "ajustar");

            Assert.Equal(TaskStatuses.Review, outgoing.Status);

            task.Transition(TaskStatuses.Review, developer, task.Version, Now.AddMinutes(11 + cycle * 2));
        }

        task.Transition(TaskStatuses.Deployment, owner, task.Version, Now.AddMinutes(20));

        var final = task.Transition(TaskStatuses.Finished, owner, task.Version, Now.AddMinutes(21));

        Assert.Equal(TaskStatuses.Finished, final.Status);

        Assert.Equal(final.StartedAt, final.EndedAt);

        Assert.All(task.Stages, stage => Assert.NotNull(stage.EndedAt));

        Assert.Equal(originalDevelopment, task.Stages[1].Id);

        Assert.Empty(task.AvailableActions(owner));

        Assert.False(task.CanWriteCurrentStage(owner));

        Assert.Equal(developer.Id, task.CurrentDeveloperId);

        for (var i = 1; i < task.Stages.Count; i++)
        {
            Assert.Equal(task.Stages[i - 1].EndedAt, task.Stages[i].StartedAt);
        }
    }

    [Fact]
    public void StaleVersionAndReturnWithoutReasonDoNotMutate()
    {
        var owner = new Actor(Guid.NewGuid(), Roles.Treasury);

        var developer = new Actor(Guid.NewGuid(), Roles.Developer);

        var task = At(TaskStatuses.Review, owner, developer);

        Assert.Equal(ErrorKind.Conflict, Assert.Throws<DomainException>(() => task.Transition(TaskStatuses.Development, owner, 1, Now.AddMinutes(20), "motivo")).Kind);

        Assert.Equal(ErrorKind.Invalid, Assert.Throws<DomainException>(() => task.Transition(TaskStatuses.Development, owner, task.Version, Now.AddMinutes(20))).Kind);

        Assert.Null(task.CurrentStage.EndedAt);

        Assert.Equal(3, task.Stages.Count);
    }

    [Fact]
    public void VisibilityAndWritingRespectRoles()
    {
        var owner = new Actor(Guid.NewGuid(), Roles.Treasury);

        var developer = new Actor(Guid.NewGuid(), Roles.Developer);

        var manager = new Actor(Guid.NewGuid(), Roles.Manager);

        var task = At(TaskStatuses.Backlog, owner, developer);

        Assert.True(task.CanRead(owner));

        Assert.True(task.CanRead(developer));

        Assert.True(task.CanRead(manager));

        Assert.False(task.CanRead(new Actor(Guid.NewGuid(), Roles.Treasury)));

        Assert.False(task.CanRead(new Actor(Guid.NewGuid(), Roles.Developer)));

        Assert.True(task.CanWriteCurrentStage(owner));

        Assert.False(task.CanWriteCurrentStage(developer));

        Assert.False(task.CanWriteCurrentStage(manager));

        task.Transition(TaskStatuses.Development, developer, task.Version, Now.AddMinutes(1));

        Assert.True(task.CanWriteCurrentStage(developer));

        Assert.False(task.CanWriteCurrentStage(owner));
    }

    private static TaskItem At(string status, Actor owner, Actor developer)
    {
        var task = new TaskItem("Título", "Descrição", owner.Id, developer.Id, Now);

        if (status == TaskStatuses.Backlog)
        {
            return task;
        }

        task.Transition(TaskStatuses.Development, developer, task.Version, Now.AddMinutes(1));

        if (status == TaskStatuses.Development)
        {
            return task;
        }

        task.Transition(TaskStatuses.Review, developer, task.Version, Now.AddMinutes(2));

        if (status == TaskStatuses.Review)
        {
            return task;
        }

        task.Transition(TaskStatuses.Deployment, owner, task.Version, Now.AddMinutes(3));

        if (status == TaskStatuses.Finished)
        {
            task.Transition(TaskStatuses.Finished, owner, task.Version, Now.AddMinutes(4));
        }

        return task;
    }
}
