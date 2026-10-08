namespace Backlog.Domain;

public sealed partial class TaskItem
{
    private TaskItem()
    {
    }

    public TaskItem(string title, string description, Guid creatorId, Guid targetDeveloperId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException(ErrorKind.Invalid, "Título e descrição são obrigatórios.");
        }

        Id = Guid.NewGuid();

        Title = title.Trim();

        Description = description.Trim();

        CreatorId = creatorId;

        TargetDeveloperId = targetDeveloperId;

        Status = TaskStatuses.Backlog;

        Version = 1;

        CreatedAt = now;

        UpdatedAt = now;

        Stages.Add(new StageEntry(Id, 1, Status, creatorId, now));
    }

    public Guid Id
    {
        get; private set;
    }

    public string Title { get; private set; } = "";

    public string Description { get; private set; } = "";

    public Guid CreatorId
    {
        get; private set;
    }

    public Guid TargetDeveloperId
    {
        get; private set;
    }

    public Guid? CurrentDeveloperId
    {
        get; private set;
    }

    public string Status { get; private set; } = "";

    public long Version
    {
        get; private set;
    }

    public DateTimeOffset CreatedAt
    {
        get; private set;
    }

    public DateTimeOffset UpdatedAt
    {
        get; private set;
    }

    public List<StageEntry> Stages { get; } = [];

    public StageEntry CurrentStage => Stages.MaxBy(x => x.Sequence)!;

    public void RestoreHistory(IEnumerable<StageEntry> stages)
    {
        Stages.Clear();

        Stages.AddRange(stages.OrderBy(x => x.Sequence));
    }
}
