using System.Security.Claims;
using Backlog.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Backlog.Api;

public static class Identity
{
    public static Actor Actor(this ClaimsPrincipal principal)
    {
        return new Actor(Guid.Parse(principal.FindFirstValue("sub")!), principal.FindFirstValue("role")!);
    }

    public static Guid SessionId(this ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue("jti")!);
    }
}

public sealed class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DomainException domain => domain.Kind switch
            {
                ErrorKind.Invalid => 400,
                ErrorKind.Unauthorized => 401,
                ErrorKind.Forbidden => 403,
                ErrorKind.NotFound => 404,
                ErrorKind.Conflict => 409,
                ErrorKind.TooLarge => 413,
                _ => 503
            },
            BadHttpRequestException bad => bad.StatusCode,
            Npgsql.NpgsqlException => 503,
            _ => 500
        };

        context.Response.StatusCode = status;

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status >= 500 ? "Serviço indisponível." : "Requisição recusada.",
                Detail = exception is DomainException ? exception.Message : null
            }
        });
    }
}
