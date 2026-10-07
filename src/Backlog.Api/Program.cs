using System.Security.Claims;
using System.Text;
using Backlog.Api;
using Backlog.Application;
using Backlog.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<FluentValidation.IValidator<LoginRequest>, LoginValidator>();

builder.Services.AddScoped<FluentValidation.IValidator<RegisterUserRequest>, RegisterUserValidator>();

builder.Services.AddScoped<CreateTaskService>();

builder.Services.AddScoped<TaskReadService>();

builder.Services.AddScoped<TransitionExecutor>();

builder.Services.AddScoped<StartDevelopmentService>();

builder.Services.AddScoped<SendToReviewService>();

builder.Services.AddScoped<ReviewTaskService>();

builder.Services.AddScoped<FinishTaskService>();

builder.Services.AddScoped<StageMaterialsService>();

builder.Services.AddScoped<FluentValidation.IValidator<CreateTaskRequest>, CreateTaskValidator>();

builder.Services.AddScoped<FluentValidation.IValidator<TransitionRequest>, TransitionValidator>();

builder.Services.AddScoped<FluentValidation.IValidator<ReviewRequest>, ReviewValidator>();

builder.Services.AddScoped<FluentValidation.IValidator<NoteRequest>, NoteValidator>();

builder.Services.AddScoped<FluentValidation.IValidator<TaskQuery>, TaskQueryValidator>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ApiDocumentTransformer>();

    options.AddOperationTransformer<ApiOperationTransformer>();
});

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 30 * 1024 * 1024);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new();

jwt.Validate();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role"
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var userId) ||
                !Guid.TryParse(context.Principal?.FindFirstValue("jti"), out var sessionId))
            {
                context.Fail("Sessão inválida.");

                return;
            }

            var service = context.HttpContext.RequestServices.GetRequiredService<AuthenticationService>();

            var actor = await service.ResolveAsync(userId, sessionId, context.HttpContext.RequestAborted);

            if (actor is null)
            {
                context.Fail("Sessão inválida.");

                return;
            }

            var identity = (ClaimsIdentity)context.Principal!.Identity!;

            foreach (var claim in identity.FindAll("role").ToArray())
            {
                identity.RemoveClaim(claim);
            }

            identity.AddClaim(new Claim("role", actor.Role));
        }
    };
});

builder.Services.AddAuthorization(options => options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
}

app.UseExceptionHandler();

app.UseAuthentication();

app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();

app.MapGet("/health/ready", async (IUserRepository users, CancellationToken ct) =>
{
    await users.AnyAsync(ct);

    return Results.Ok(new { status = "ready" });
}).AllowAnonymous();

app.MapUserEndpoints();

app.MapTaskEndpoints();

app.MapTransitionEndpoints();

app.MapMaterialsEndpoints();

app.MapDocumentation();

app.Run();

public partial class Program
{
}
