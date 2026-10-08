using Backlog.Application;
using Microsoft.AspNetCore.Mvc;

namespace Backlog.Api;

public static class TransitionEndpoints
{
    public static void MapTransitionEndpoints(this WebApplication app)
    {
        app.MapPost("/tasks/{id:guid}/start", async (Guid id, [FromForm] TransitionRequest request, HttpContext context, StartDevelopmentService service, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            await service.ExecuteAsync(id, actor, request.ExpectedVersion, await UploadReader.ReadAsync(request, ct), ct);

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        }).Produces<TaskResponse>().DisableAntiforgery().AddEndpointFilter<ValidationFilter<TransitionRequest>>();

        app.MapPost("/tasks/{id:guid}/send-to-review", async (Guid id, [FromForm] TransitionRequest request, HttpContext context, SendToReviewService service, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            await service.ExecuteAsync(id, actor, request.ExpectedVersion, await UploadReader.ReadAsync(request, ct), ct);

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        }).Produces<TaskResponse>().DisableAntiforgery().AddEndpointFilter<ValidationFilter<TransitionRequest>>();

        app.MapPost("/tasks/{id:guid}/review", async (Guid id, [FromForm] ReviewRequest request, HttpContext context, ReviewTaskService service, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            await service.ExecuteAsync(id, actor, request.ExpectedVersion, request.Decision, await UploadReader.ReadAsync(request, ct), ct);

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        }).Produces<TaskResponse>().DisableAntiforgery().AddEndpointFilter<ValidationFilter<ReviewRequest>>();

        app.MapPost("/tasks/{id:guid}/finish", async (Guid id, [FromForm] TransitionRequest request, HttpContext context, FinishTaskService service, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            await service.ExecuteAsync(id, actor, request.ExpectedVersion, await UploadReader.ReadAsync(request, ct), ct);

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        }).Produces<TaskResponse>().DisableAntiforgery().AddEndpointFilter<ValidationFilter<TransitionRequest>>();
    }
}
