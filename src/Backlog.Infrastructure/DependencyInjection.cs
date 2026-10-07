using Backlog.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backlog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new();

        jwt.Validate();

        services.AddSingleton(jwt);

        services.AddSingleton(configuration.GetSection("Bootstrap").Get<BootstrapSettings>() ?? new());

        services.AddDbContext<BacklogDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<ISessionRepository, SessionRepository>();

        services.AddScoped<ITaskRepository, TaskRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPasswordHasher, SecurePasswordHasher>();

        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<AuthenticationService>();

        services.AddScoped<UserService>();

        return services;
    }
}
