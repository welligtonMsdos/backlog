using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backlog.Api;
using Backlog.Domain;

namespace Backlog.IntegrationTests;

internal sealed record Participants(HttpClient Manager, HttpClient Owner, HttpClient Developer, HttpClient OtherOwner, HttpClient OtherDeveloper, Guid DeveloperId, Guid OtherDeveloperId) : IDisposable
{
    public void Dispose()
    {
        Manager.Dispose();

        Owner.Dispose();

        Developer.Dispose();

        OtherOwner.Dispose();

        OtherDeveloper.Dispose();
    }
}

internal static class TaskHelpers
{
    public static async Task<Participants> ParticipantsAsync(ApiHarness api)
    {
        var manager = await api.LoginAsync();

        var owner = await api.RegisterAsync(manager, Roles.Treasury);

        var developer = await api.RegisterAsync(manager, Roles.Developer);

        var otherOwner = await api.RegisterAsync(manager, Roles.Treasury);

        var otherDeveloper = await api.RegisterAsync(manager, Roles.Developer);

        return new Participants(manager, await api.LoginAsync(owner.Email), await api.LoginAsync(developer.Email), await api.LoginAsync(otherOwner.Email), await api.LoginAsync(otherDeveloper.Email), developer.Id, otherDeveloper.Id);
    }

    public static MultipartFormDataContent Form(Dictionary<string, string> fields, string? filename = null, string contentType = "text/plain", byte[]? content = null)
    {
        var form = new MultipartFormDataContent();

        foreach (var (name, value) in fields)
        {
            form.Add(new StringContent(value), name);
        }

        if (filename is not null)
        {
            var file = new ByteArrayContent(content ?? "arquivo de teste"u8.ToArray());

            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            form.Add(file, "Files", filename);
        }

        return form;
    }

    public static async Task<TaskResponse> CreateAsync(HttpClient owner, Guid developerId, bool withFile = false)
    {
        using var form = Form(new()
        {
            ["Title"] = "Tarefa de integração",
            ["Description"] = "Descrição detalhada",
            ["TargetDeveloperId"] = developerId.ToString(),
            ["Note"] = "criação"
        }, withFile ? "backlog.txt" : null);

        var response = await owner.PostAsync("/tasks", form);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }

    public static async Task<TaskResponse> TransitionAsync(HttpClient actor, TaskResponse task, string action, string? decision = null, bool withFile = true)
    {
        var fields = new Dictionary<string, string>
        {
            ["ExpectedVersion"] = task.Version.ToString(),
            ["Note"] = action + (decision is null ? "" : ":" + decision)
        };

        if (decision is not null)
        {
            fields["Decision"] = decision;
        }

        using var form = Form(fields, withFile ? action + ".txt" : null);

        var response = await actor.PostAsync($"/tasks/{task.Id}/{action}", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
    }

    public sealed record TaskPageResponse(IReadOnlyList<TaskResponse> Items, int Total, int Page, int PageSize);
}
