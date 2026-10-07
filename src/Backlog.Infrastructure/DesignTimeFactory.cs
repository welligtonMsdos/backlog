using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backlog.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<BacklogDbContext>
{
    public BacklogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BacklogDbContext>()
            .UseNpgsql("Host=localhost;Database=backlog;Username=backlog")
            .Options;

        return new BacklogDbContext(options);
    }
}
