using Backlog.Application;
using Microsoft.AspNetCore.Mvc;

namespace Backlog.Api;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        app.MapPost("/tasks", async ([FromForm] CreateTaskRequest request, HttpContext context, CreateTaskService create, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            var task = await create.CreateAsync(actor, request.Title, request.Description, request.TargetDeveloperId, await UploadReader.ReadAsync(request, ct), ct);

            return Results.Created($"/tasks/{task.Id}", TaskResponse.From(await read.GetAsync(task.Id, actor, ct), actor));
        }).DisableAntiforgery().AddEndpointFilter<ValidationFilter<CreateTaskRequest>>();

        app.MapGet("/tasks", async ([AsParameters] TaskQuery query, HttpContext context, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            var page = await read.ListAsync(actor, query.Status, query.Page, query.PageSize, ct);

            return Results.Ok(new { Items = page.Items.Select(view => TaskResponse.From(view, actor)), page.Total, page.Page, page.PageSize });
        }).AddEndpointFilter<ValidationFilter<TaskQuery>>();

        app.MapGet("/tasks/{id:guid}", async (Guid id, HttpContext context, TaskReadService read, CancellationToken ct) =>
        {
            var actor = context.User.Actor();

            return Results.Ok(TaskResponse.From(await read.GetAsync(id, actor, ct), actor));
        });
    }
}
