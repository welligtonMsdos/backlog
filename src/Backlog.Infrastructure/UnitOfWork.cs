using Backlog.Application;
using Backlog.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backlog.Infrastructure;

public sealed class UnitOfWork(BacklogDbContext db) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var result = await operation();

            await transaction.CommitAsync(ct);

            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(ErrorKind.Conflict, "A tarefa foi atualizada por outra requisição.");
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException(ErrorKind.Conflict, "Registro duplicado ou atualização concorrente.");
        }
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException(ErrorKind.Conflict, "Registro já cadastrado.");
        }
    }
}
