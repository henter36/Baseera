namespace Baseera.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class BaseeraDbContextFactory : IDesignTimeDbContextFactory<BaseeraDbContext>
{
    public BaseeraDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("BASEERA_CONNECTION")
            ?? Environment.GetEnvironmentVariable("BASEERA_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set BASEERA_CONNECTION (or BASEERA_TEST_CONNECTION) before running EF design-time commands.");

        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new BaseeraDbContext(options);
    }
}
