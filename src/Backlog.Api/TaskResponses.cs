using Backlog.Application;
using Backlog.Domain;

namespace Backlog.Api;

public sealed record PersonResponse(Guid Id, string Name);

public sealed record NoteResponse(Guid Id, string Text, Guid AuthorUserId, DateTimeOffset CreatedAt);

public sealed record AttachmentResponse(Guid Id, string OriginalName, string ContentType, long SizeBytes, Guid UploadedByUserId, DateTimeOffset CreatedAt);

public sealed record StageResponse(Guid Id, int Sequence, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, Guid StartedByUserId, Guid? EndedByUserId, IReadOnlyList<NoteResponse> Notes, IReadOnlyList<AttachmentResponse> Attachments);

public sealed record TaskResponse(Guid Id, string Title, string Description, string Status, long Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, PersonResponse Creator, PersonResponse TargetDeveloper, PersonResponse? CurrentDeveloper, IReadOnlyList<string> AvailableActions, IReadOnlyList<StageResponse> Stages)
{
    public static TaskResponse From(TaskView view, Actor actor)
    {
        var task = view.Task;

        var stages = task.Stages.OrderBy(stage => stage.Sequence).Select(stage => new StageResponse(
            stage.Id, stage.Sequence, stage.Status, stage.StartedAt, stage.EndedAt, stage.StartedByUserId, stage.EndedByUserId,
            stage.Notes.OrderBy(note => note.CreatedAt).Select(note => new NoteResponse(note.Id, note.Text, note.AuthorUserId, note.CreatedAt)).ToArray(),
            stage.Attachments.OrderBy(file => file.CreatedAt).Select(file => new AttachmentResponse(file.Id, file.OriginalName, file.ContentType, file.SizeBytes, file.UploadedByUserId, file.CreatedAt)).ToArray())).ToArray();

        return new TaskResponse(task.Id, task.Title, task.Description, task.Status, task.Version, task.CreatedAt, task.UpdatedAt,
            new(view.Creator.Id, view.Creator.Name), new(view.TargetDeveloper.Id, view.TargetDeveloper.Name),
            view.CurrentDeveloper is null ? null : new(view.CurrentDeveloper.Id, view.CurrentDeveloper.Name), task.AvailableActions(actor), stages);
    }
}
