namespace Backlog.Application;

public sealed class ReadinessService(IUserRepository users)
{
    public async Task CheckAsync(CancellationToken ct)
    {
        await users.AnyAsync(ct);
    }
}
