using Backlog.Application;
using Backlog.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Backlog.Api;

public static class MaterialsEndpoints
{
    public static void MapMaterialsEndpoints(this WebApplication app)
    {
        app.MapPost("/tasks/{id:guid}/stages/{stageId:guid}/notes", async (Guid id, Guid stageId, NoteRequest request, HttpContext context, StageMaterialsService service, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            await service.AddAsync(id, stageId, actor, request.ExpectedVersion, new TaskMaterials(request.Text, []), ct);

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        }).Produces<TaskResponse>().AddEndpointFilter<ValidationFilter<NoteRequest>>();

        app.MapPost("/tasks/{id:guid}/stages/{stageId:guid}/attachments", async (Guid id, Guid stageId, [FromForm] TransitionRequest request, HttpContext context, StageMaterialsService service, TaskReadService read, CancellationToken ct) =>
        {
            if (request.Files.Count == 0)
            {
                throw new DomainException(ErrorKind.Invalid, "Envie ao menos um arquivo.");
            }

            var actor = context.User.Actor();

            await service.AddAsync(id, stageId, actor, request.ExpectedVersion, await UploadReader.ReadAsync(request, ct), ct);

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        }).Produces<TaskResponse>().DisableAntiforgery().AddEndpointFilter<ValidationFilter<TransitionRequest>>();

        app.MapGet("/tasks/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, HttpContext context, StageMaterialsService service, CancellationToken ct) =>
        {
            var file = await service.DownloadAsync(id, attachmentId, context.User.Actor(), ct);

            context.Response.Headers.XContentTypeOptions = "nosniff";

            return Results.File(file.Content, file.ContentType, file.OriginalName);
        }).Produces(StatusCodes.Status200OK, contentType: "application/octet-stream");
    }
}
