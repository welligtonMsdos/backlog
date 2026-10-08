using System.Net;
using System.Net.Http.Json;
using Backlog.Api;
using Backlog.Application;
using Backlog.Domain;

namespace Backlog.IntegrationTests;

public sealed class TaskAccessTests : ApiTestBase
{
    [Fact]
    public async Task ListsDetailsAndDownloadsAreScopedByCurrentIdentity()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId, true);

        var other = await TaskHelpers.CreateAsync(users.OtherOwner, users.OtherDeveloperId);

        Assert.Equal(TaskStatuses.Backlog, task.Status);

        Assert.Null(task.CurrentDeveloper);

        Assert.Null(Assert.Single(task.Stages).EndedAt);

        var devTask = await users.Developer.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}");

        Assert.Contains("start", devTask!.AvailableActions);

        Assert.Empty(task.AvailableActions);

        foreach (var reader in new[] { users.Owner, users.Developer })
        {
            var page = await reader.GetFromJsonAsync<TaskHelpers.TaskPageResponse>("/tasks?page=1&pageSize=10");

            Assert.Equal(task.Id, Assert.Single(page!.Items).Id);

            Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/tasks/{other.Id}")).StatusCode);
        }

        var managerPage = await users.Manager.GetFromJsonAsync<TaskHelpers.TaskPageResponse>("/tasks?status=backlog&pageSize=1");

        Assert.Equal(2, managerPage!.Total);

        Assert.Single(managerPage.Items);

        var attachment = Assert.Single(task.Stages[0].Attachments);

        foreach (var reader in new[] { users.Owner, users.Developer, users.Manager })
        {
            var download = await reader.GetAsync($"/tasks/{task.Id}/attachments/{attachment.Id}");

            Assert.Equal(HttpStatusCode.OK, download.StatusCode);

            Assert.Equal("arquivo de teste"u8.ToArray(), await download.Content.ReadAsByteArrayAsync());
        }

        foreach (var reader in new[] { users.OtherOwner, users.OtherDeveloper })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/tasks/{task.Id}")).StatusCode);

            Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/tasks/{task.Id}/attachments/{attachment.Id}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await users.Manager.GetAsync($"/tasks/{other.Id}/attachments/{attachment.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.GetAsync("/tasks?page=0")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.GetAsync("/tasks?status=unknown")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await users.Owner.GetAsync("/users/developers")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await users.Developer.GetAsync("/users/developers")).StatusCode);

        using var forbidden = TaskHelpers.Form(new()
        {
            ["Title"] = "Título",
            ["Description"] = "Descrição",
            ["TargetDeveloperId"] = users.DeveloperId.ToString()
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await users.Manager.PostAsync("/tasks", forbidden)).StatusCode);
    }

    [Fact]
    public async Task NotesAndUploadsRequireCurrentStageOwnerAndVersion()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId);

        var stageId = task.Stages[0].Id;

        foreach (var actor in new[] { users.Developer, users.Manager })
        {
            var denied = await actor.PostAsJsonAsync($"/tasks/{task.Id}/stages/{stageId}/notes", new NoteRequest(task.Version, "negado"));

            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        var note = await users.Owner.PostAsJsonAsync($"/tasks/{task.Id}/stages/{stageId}/notes", new NoteRequest(task.Version, "observação extra"));

        Assert.Equal(HttpStatusCode.OK, note.StatusCode);

        task = (await note.Content.ReadFromJsonAsync<TaskResponse>())!;

        Assert.Equal(2, task.Stages[0].Notes.Count);

        using var upload = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        }, "upload.pdf", "application/pdf", "%PDF-1.7 arquivo"u8.ToArray());

        var uploaded = await users.Owner.PostAsync($"/tasks/{task.Id}/stages/{stageId}/attachments", upload);

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);

        task = (await uploaded.Content.ReadFromJsonAsync<TaskResponse>())!;

        Assert.Single(task.Stages[0].Attachments);

        task = await TaskHelpers.TransitionAsync(users.Developer, task, "start", withFile: false);

        Assert.Equal(users.DeveloperId, task.CurrentDeveloper!.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await users.Owner.PostAsJsonAsync($"/tasks/{task.Id}/stages/{stageId}/notes", new NoteRequest(task.Version, "etapa encerrada"))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await users.Owner.PostAsJsonAsync($"/tasks/{task.Id}/stages/{task.Stages[^1].Id}/notes", new NoteRequest(task.Version, "negado"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await users.Developer.PostAsJsonAsync($"/tasks/{task.Id}/stages/{task.Stages[^1].Id}/notes", new NoteRequest(task.Version, "progresso"))).StatusCode);
    }

    [Fact]
    public async Task InvalidFilesAreRejectedWithoutChangingTask()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId);

        var route = $"/tasks/{task.Id}/stages/{task.Stages[0].Id}/attachments";

        using var large = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        }, "large.txt", content: new byte[MaterialWriter.MaxFileBytes + 1]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await users.Owner.PostAsync(route, large)).StatusCode);

        using var falsePdf = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        }, "fake.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.PostAsync(route, falsePdf)).StatusCode);

        using var forbidden = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        }, "file.exe", "application/octet-stream");

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.PostAsync(route, forbidden)).StatusCode);

        var unchanged = await users.Owner.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}");

        Assert.Equal(task.Version, unchanged!.Version);

        Assert.Empty(unchanged.Stages[0].Attachments);

        for (var i = 0; i < 5; i++)
        {
            using var form = TaskHelpers.Form(new()
            {
                ["ExpectedVersion"] = task.Version.ToString()
            }, $"file-{i}.txt");

            var response = await users.Owner.PostAsync(route, form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            task = (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
        }

        using var excess = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        }, "excess.txt");

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.PostAsync(route, excess)).StatusCode);

        Assert.Equal(5, (await users.Owner.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}"))!.Stages[0].Attachments.Count);
    }
}
