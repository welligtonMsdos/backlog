using Backlog.Application;

namespace Backlog.Api;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (LoginRequest request, AuthenticationService auth, CancellationToken ct) =>
        {
            var token = await auth.LoginAsync(request.Email, request.Password, ct);

            return Results.Ok(new LoginResponse(token.AccessToken, token.ExpiresAt, token.Role));
        }).AllowAnonymous().Produces<LoginResponse>().AddEndpointFilter<ValidationFilter<LoginRequest>>();

        app.MapPost("/auth/logout", async (HttpContext context, AuthenticationService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(context.User.SessionId(), ct);

            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        app.MapGet("/users/me", async (HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok(UserResponse.From(await users.GetAsync(context.User.Actor().Id, ct)))).Produces<UserResponse>();

        app.MapPost("/users", async (RegisterUserRequest request, HttpContext context, UserService users, CancellationToken ct) =>
        {
            var user = await users.RegisterAsync(context.User.Actor(), request.Name, request.Email, request.Password, request.Role, request.IsActive, ct);

            return Results.Json(UserResponse.From(user), statusCode: StatusCodes.Status201Created);
        }).Produces<UserResponse>(StatusCodes.Status201Created).AddEndpointFilter<ValidationFilter<RegisterUserRequest>>();

        app.MapGet("/users/developers", async (HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok((await users.DevelopersAsync(context.User.Actor(), ct)).Select(UserResponse.From).ToArray())).Produces<UserResponse[]>();
    }
}
