using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Backlog.Api;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Backlog da Tesouraria",
            Version = "v1",
            Description = "Backlog → desenvolvimento → homologação → em implantação → finalizado. Homologação pode retornar ao desenvolvimento com motivo. Operações com arquivos usam multipart/form-data e expectedVersion. Limites: cinco arquivos de 5 MiB por passagem; PDF, PNG, JPEG e texto UTF-8. Gestor consulta todas as tarefas e cadastra usuários."
        };

        document.Components ??= new OpenApiComponents();

        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT emitido por POST /auth/login; logout revoga a sessão."
            }
        };

        foreach (var (route, path) in document.Paths)
        {
            foreach (var operation in path.Operations!.Values)
            {
                if (route == "/auth/login" || route.StartsWith("/health/"))
                {
                    continue;
                }

                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class ApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        operation.Responses ??= new OpenApiResponses();

        foreach (var (code, description) in new[] { ("400", "Campos inválidos."), ("401", "Token ou sessão inválidos."), ("403", "Sem permissão."), ("404", "Recurso inexistente ou não visível."), ("409", "Versão ou status desatualizados."), ("413", "Arquivo ou requisição excede o limite."), ("503", "Banco indisponível; reinicie a API após recuperar PostgreSQL.") })
        {
            operation.Responses.TryAdd(code, new OpenApiResponse { Description = description });
        }

        return Task.CompletedTask;
    }
}

public static class Documentation
{
    public static void MapDocumentation(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Documentation:Enabled"))
        {
            return;
        }

        app.MapOpenApi().AllowAnonymous();

        app.MapScalarApiReference("/scalar", options => options.WithTitle("Backlog da Tesouraria").DisableDefaultFonts().DisableAgent().AddPreferredSecuritySchemes("Bearer")).AllowAnonymous();
    }
}
