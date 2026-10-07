namespace Backlog.Domain;

public sealed class StageEntry
{
    private StageEntry() { }

    public StageEntry(Guid taskId, int sequence, string status, Guid author, DateTimeOffset now)
    {
        Id = Guid.NewGuid();

        TaskId = taskId;

        Sequence = sequence;

        Status = status;

        StartedAt = now;

        StartedByUserId = author;
    }

    public Guid Id { get; private set; }

    public Guid TaskId { get; private set; }

    public int Sequence { get; private set; }

    public string Status { get; private set; } = "";

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public Guid StartedByUserId { get; private set; }

    public Guid? EndedByUserId { get; private set; }

    public List<StageNote> Notes { get; private set; } = [];

    public List<StageAttachment> Attachments { get; private set; } = [];

    public void Close(Guid author, DateTimeOffset now)
    {
        if (EndedAt.HasValue || now < StartedAt)
        {
            throw new DomainException(ErrorKind.Conflict, "Etapa encerrada ou data inválida.");
        }

        EndedAt = now;

        EndedByUserId = author;
    }

    public void AddNote(string? text, Guid author, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Notes.Add(new StageNote(Id, text.Trim(), author, now));
        }
    }

    public void AddAttachment(StageAttachment attachment)
    {
        if (attachment.StageEntryId != Id)
        {
            throw new DomainException(ErrorKind.Invalid, "Anexo não pertence à etapa.");
        }

        Attachments.Add(attachment);
    }
}

public sealed class StageNote
{
    private StageNote() { }

    public StageNote(Guid stageEntryId, string text, Guid author, DateTimeOffset now)
    {
        Id = Guid.NewGuid();

        StageEntryId = stageEntryId;

        Text = text;

        AuthorUserId = author;

        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid StageEntryId { get; private set; }

    public string Text { get; private set; } = "";

    public Guid AuthorUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class StageAttachment
{
    private StageAttachment() { }

    public StageAttachment(Guid stageEntryId, Guid author, string name, string contentType, byte[] content, DateTimeOffset now)
    {
        Id = Guid.NewGuid();

        StageEntryId = stageEntryId;

        UploadedByUserId = author;

        OriginalName = name;

        ContentType = contentType;

        Content = content;

        SizeBytes = content.LongLength;

        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid StageEntryId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string OriginalName { get; private set; } = "";

    public string ContentType { get; private set; } = "";

    public long SizeBytes { get; private set; }

    public byte[] Content { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }
}
