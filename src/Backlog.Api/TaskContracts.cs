using Backlog.Application;
using Backlog.Domain;
using FluentValidation;

namespace Backlog.Api;

public class MaterialsRequest
{
    public string? Note
    {
        get; set;
    }

    public IFormFileCollection Files { get; set; } = new FormFileCollection();
}

public sealed class CreateTaskRequest : MaterialsRequest
{
    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public Guid TargetDeveloperId
    {
        get; set;
    }
}

public class TransitionRequest : MaterialsRequest
{
    public long ExpectedVersion
    {
        get; set;
    }
}

public sealed class ReviewRequest : TransitionRequest
{
    public string Decision { get; set; } = "";
}

public sealed record NoteRequest(long ExpectedVersion, string Text);

public sealed record TaskQuery(string? Status = null, int Page = 1, int PageSize = 20);

public sealed class MaterialsValidator<T> : AbstractValidator<T> where T : MaterialsRequest
{
    public MaterialsValidator()
    {
        RuleFor(x => x.Note).MaximumLength(4000);

        RuleFor(x => x.Files).NotNull().Must(files => files is not null && files.Count <= MaterialWriter.MaxFiles).WithMessage("Máximo de cinco arquivos.");

        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(x => x.FileName).NotEmpty().MaximumLength(200).Must(name => name.IndexOfAny(['/', '\\', '\r', '\n', '\0']) < 0);

            file.RuleFor(x => x.ContentType).Must(type => MaterialWriter.AllowedTypes.Contains(type));

            file.RuleFor(x => x.Length).GreaterThan(0);
        });
    }
}

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskValidator()
    {
        Include(new MaterialsValidator<CreateTaskRequest>());

        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Description).NotEmpty().MaximumLength(10000);

        RuleFor(x => x.TargetDeveloperId).NotEmpty();
    }
}

public sealed class TransitionValidator : AbstractValidator<TransitionRequest>
{
    public TransitionValidator()
    {
        Include(new MaterialsValidator<TransitionRequest>());

        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}

public sealed class ReviewValidator : AbstractValidator<ReviewRequest>
{
    public ReviewValidator()
    {
        Include(new MaterialsValidator<ReviewRequest>());

        RuleFor(x => x.ExpectedVersion).GreaterThan(0);

        RuleFor(x => x.Decision).Must(decision => decision is "return-to-development" or "send-to-deployment");

        RuleFor(x => x.Note).NotEmpty().When(x => x.Decision == "return-to-development");
    }
}

public sealed class NoteValidator : AbstractValidator<NoteRequest>
{
    public NoteValidator()
    {
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);

        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public sealed class TaskQueryValidator : AbstractValidator<TaskQuery>
{
    public TaskQueryValidator()
    {
        RuleFor(x => x.Status).Must(status => status is null || TaskStatuses.All.Contains(status));

        RuleFor(x => x.Page).GreaterThan(0).LessThanOrEqualTo(1000000);

        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public static class UploadReader
{
    public static async Task<TaskMaterials> ReadAsync(MaterialsRequest request, CancellationToken ct)
    {
        var files = new List<UploadedFile>();

        foreach (var file in request.Files)
        {
            if (file.Length > MaterialWriter.MaxFileBytes)
            {
                throw new DomainException(ErrorKind.TooLarge, "Arquivo excede 5 MiB.");
            }

            using var stream = new MemoryStream();

            await file.CopyToAsync(stream, ct);

            files.Add(new UploadedFile(file.FileName, file.ContentType, stream.ToArray()));
        }

        return new TaskMaterials(request.Note, files);
    }
}
