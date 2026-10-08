using System.Net;
using System.Net.Http.Json;
using Backlog.Api;
using Backlog.Domain;
using Npgsql;

namespace Backlog.IntegrationTests;

public sealed class WorkflowTests : ApiTestBase
{
    [Fact]
    public async Task CompleteFlowWithReturnPreservesMaterialsAndTerminalDates()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId, true);

        task = await TaskHelpers.TransitionAsync(users.Developer, task, "start");

        task = await TaskHelpers.TransitionAsync(users.Developer, task, "send-to-review");

        task = await TaskHelpers.TransitionAsync(users.Owner, task, "review", "return-to-development");

        task = await TaskHelpers.TransitionAsync(users.Developer, task, "send-to-review");

        task = await TaskHelpers.TransitionAsync(users.Owner, task, "review", "send-to-deployment");

        task = await TaskHelpers.TransitionAsync(users.Owner, task, "finish");

        Assert.Equal(TaskStatuses.Finished, task.Status);

        Assert.Equal(7, task.Stages.Count);

        Assert.Equal(new[] { TaskStatuses.Backlog, TaskStatuses.Development, TaskStatuses.Review, TaskStatuses.Development, TaskStatuses.Review, TaskStatuses.Deployment, TaskStatuses.Finished }, task.Stages.Select(stage => stage.Status));

        Assert.Equal("criação", Assert.Single(task.Stages[0].Notes).Text);

        Assert.Equal(new[] { "start", "send-to-review" }, task.Stages[1].Notes.Select(note => note.Text));

        Assert.Equal("review:return-to-development", Assert.Single(task.Stages[2].Notes).Text);

        Assert.Equal("send-to-review", Assert.Single(task.Stages[3].Notes).Text);

        Assert.Equal("review:send-to-deployment", Assert.Single(task.Stages[4].Notes).Text);

        Assert.Empty(task.Stages[5].Notes);

        Assert.Equal("finish", Assert.Single(task.Stages[6].Notes).Text);

        Assert.Equal(new[] { 1, 2, 1, 1, 1, 0, 1 }, task.Stages.Select(stage => stage.Attachments.Count));

        Assert.All(task.Stages, stage => Assert.NotNull(stage.EndedAt));

        Assert.Equal(task.Stages[^1].StartedAt, task.Stages[^1].EndedAt);

        Assert.Empty(task.AvailableActions);

        for (var i = 1; i < task.Stages.Count; i++)
        {
            Assert.Equal(task.Stages[i - 1].EndedAt, task.Stages[i].StartedAt);
        }

        foreach (var action in new[] { "start", "send-to-review", "review", "finish" })
        {
            using var form = TaskHelpers.Form(new()
            {
                ["ExpectedVersion"] = task.Version.ToString(),
                ["Decision"] = "send-to-deployment"
            });

            Assert.Equal(HttpStatusCode.Conflict, (await users.Owner.PostAsync($"/tasks/{task.Id}/{action}", form)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await users.Owner.PostAsJsonAsync($"/tasks/{task.Id}/stages/{task.Stages[^1].Id}/notes", new NoteRequest(task.Version, "negado"))).StatusCode);

        var fromDatabase = await users.Manager.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}");

        Assert.Equal(task.Stages.Select(stage => stage.Id), fromDatabase!.Stages.Select(stage => stage.Id));
    }

    [Fact]
    public async Task WrongStatesRolesAndMissingReturnReasonAreRejected()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId);

        using var wrongState = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = "1"
        });

        Assert.Equal(HttpStatusCode.Conflict, (await users.Owner.PostAsync($"/tasks/{task.Id}/finish", wrongState)).StatusCode);

        using var wrongRole = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = "1"
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await users.Manager.PostAsync($"/tasks/{task.Id}/start", wrongRole)).StatusCode);

        using var invisible = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = "1"
        });

        Assert.Equal(HttpStatusCode.NotFound, (await users.OtherDeveloper.PostAsync($"/tasks/{task.Id}/start", invisible)).StatusCode);

        task = await TaskHelpers.TransitionAsync(users.Developer, task, "start", withFile: false);

        using var treasuryCannotSend = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await users.Owner.PostAsync($"/tasks/{task.Id}/send-to-review", treasuryCannotSend)).StatusCode);

        task = await TaskHelpers.TransitionAsync(users.Developer, task, "send-to-review", withFile: false);

        using var missingReason = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString(),
            ["Decision"] = "return-to-development"
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.PostAsync($"/tasks/{task.Id}/review", missingReason)).StatusCode);

        using var invalidDecision = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString(),
            ["Decision"] = "finalizado"
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Owner.PostAsync($"/tasks/{task.Id}/review", invalidDecision)).StatusCode);

        using var cannotSkip = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = task.Version.ToString()
        });

        Assert.Equal(HttpStatusCode.Conflict, (await users.Owner.PostAsync($"/tasks/{task.Id}/finish", cannotSkip)).StatusCode);

        Assert.Equal(task.Version, (await users.Owner.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}"))!.Version);
    }

    [Fact]
    public async Task ConcurrentStartAndNotesHaveExactlyOneWinner()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId);

        using var first = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = "1"
        });

        using var second = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = "1"
        });

        var responses = await Task.WhenAll(users.Developer.PostAsync($"/tasks/{task.Id}/start", first), users.Developer.PostAsync($"/tasks/{task.Id}/start", second));

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(response => response.StatusCode).OrderBy(status => (int)status));

        task = (await users.Developer.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}"))!;

        Assert.Equal(2, task.Version);

        Assert.Equal(2, task.Stages.Count);

        var noteRoute = $"/tasks/{task.Id}/stages/{task.Stages[^1].Id}/notes";

        var notes = await Task.WhenAll(users.Developer.PostAsJsonAsync(noteRoute, new NoteRequest(task.Version, "primeiro")), users.Developer.PostAsJsonAsync(noteRoute, new NoteRequest(task.Version, "segundo")));

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, notes.Select(response => response.StatusCode).OrderBy(status => (int)status));

        var updated = await users.Manager.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}");

        Assert.Single(updated!.Stages[^1].Notes);

        Assert.Equal(3, updated.Version);

        await using var connection = new NpgsqlConnection(Api.ConnectionString);

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT count(*) FROM task_stage_entries WHERE task_id = @id AND ended_at IS NULL", connection);

        command.Parameters.AddWithValue("id", task.Id);

        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task InvalidTransitionFileRollsBackStatusVersionAndHistory()
    {
        using var users = await TaskHelpers.ParticipantsAsync(Api);

        var task = await TaskHelpers.CreateAsync(users.Owner, users.DeveloperId);

        using var invalid = TaskHelpers.Form(new()
        {
            ["ExpectedVersion"] = "1",
            ["Note"] = "não deve persistir"
        }, "bad.pdf", "application/pdf", "not a pdf"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, (await users.Developer.PostAsync($"/tasks/{task.Id}/start", invalid)).StatusCode);

        var unchanged = await users.Owner.GetFromJsonAsync<TaskResponse>($"/tasks/{task.Id}");

        Assert.Equal(TaskStatuses.Backlog, unchanged!.Status);

        Assert.Equal(1, unchanged.Version);

        Assert.Null(unchanged.CurrentDeveloper);

        Assert.Single(unchanged.Stages);

        Assert.Null(unchanged.Stages[0].EndedAt);

        Assert.Single(unchanged.Stages[0].Notes);

        Assert.Empty(unchanged.Stages[0].Attachments);
    }

    [Fact]
    public async Task DocumentationHasBearerAndLocalResources()
    {
        var response = await Api.Client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("Bearer", json);

        Assert.Contains("/tasks/{id}/review", json);

        Assert.Contains("413", json);

        Assert.Contains("TaskResponse", json);

        Assert.Contains("LoginResponse", json);

        Assert.Contains("application/problem+json", json);

        Assert.Equal("application/problem+json", (await Api.Client.GetAsync("/users/me")).Content.Headers.ContentType!.MediaType);

        var html = await Api.Client.GetStringAsync("/scalar");

        Assert.Contains("\"withDefaultFonts\":false", html);

        Assert.Contains("\"agent\":{\"disabled\":true}", html);

        Assert.DoesNotContain("cdn.jsdelivr", html);

        Assert.DoesNotContain("fonts.googleapis", html);

        var resources = System.Text.RegularExpressions.Regex.Matches(html, "src=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();

        Assert.NotEmpty(resources);

        foreach (var resource in resources)
        {
            var uri = new Uri(new Uri(Api.Client.BaseAddress!, "/scalar/"), resource);

            Assert.Equal(Api.Client.BaseAddress!.Host, uri.Host);

            Assert.Equal(HttpStatusCode.OK, (await Api.Client.GetAsync(uri.PathAndQuery)).StatusCode);
        }
    }
}
