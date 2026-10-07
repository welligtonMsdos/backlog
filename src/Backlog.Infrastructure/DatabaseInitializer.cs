using System.Net.Mail;
using Backlog.Application;
using Backlog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backlog.Infrastructure;

public sealed class BootstrapSettings
{
    public string Name { get; init; } = "";

    public string Email { get; init; } = "";

    public string Password { get; init; } = "";
}

public sealed class DatabaseInitializer(BacklogDbContext db, IPasswordHasher hasher, IClock clock, BootstrapSettings settings)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3098181)", ct);

        if (!await db.Users.AnyAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(settings.Name) || settings.Name.Length > 200
                || !MailAddress.TryCreate(settings.Email, out _) || settings.Email.Length > 254
                || settings.Password.Length < 12 || settings.Password.Length > 128)
            {
                throw new InvalidOperationException("Configuração do gestor inicial ausente ou inválida.");
            }

            db.Users.Add(new User(settings.Name, settings.Email, hasher.Hash(settings.Password), Roles.Manager, clock.UtcNow));

            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
