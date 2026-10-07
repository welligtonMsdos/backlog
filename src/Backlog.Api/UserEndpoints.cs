using Backlog.Application;

namespace Backlog.Api;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (LoginRequest request, AuthenticationService auth, UserService users, CancellationToken ct) =>
        {
            var token = await auth.LoginAsync(request.Email, request.Password, ct);

            var user = await users.GetAsync(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken).Claims.Where(c => c.Type == "sub").Select(c => Guid.Parse(c.Value)).Single(), ct);

            return Results.Ok(new { token.AccessToken, token.ExpiresAt, user.Role });
        }).AllowAnonymous().AddEndpointFilter<ValidationFilter<LoginRequest>>();

        app.MapPost("/auth/logout", async (HttpContext context, AuthenticationService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(context.User.SessionId(), ct);

            return Results.NoContent();
        });

        app.MapGet("/users/me", async (HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok(UserResponse.From(await users.GetAsync(context.User.Actor().Id, ct))));

        app.MapPost("/users", async (RegisterUserRequest request, HttpContext context, UserService users, CancellationToken ct) =>
        {
            var user = await users.RegisterAsync(context.User.Actor(), request.Name, request.Email, request.Password, request.Role, request.IsActive, ct);

            return Results.Created("/users/me", UserResponse.From(user));
        }).AddEndpointFilter<ValidationFilter<RegisterUserRequest>>();

        app.MapGet("/users/developers", async (HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok((await users.DevelopersAsync(context.User.Actor(), ct)).Select(UserResponse.From)));
    }
}
