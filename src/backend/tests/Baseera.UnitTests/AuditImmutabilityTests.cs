using Baseera.Domain.Audit;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class AuditImmutabilityTests
{
    [Fact]
    public void Modified_audit_log_is_rejected()
    {
        using var db = CreateDb();
        var log = new AuditLog { Action = "Create", Module = "Test", EntityType = "X", Outcome = "Success" };
        db.AuditLogs.Add(log);
        db.SaveChanges();

        log.Action = "Hacked";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Deleted_audit_log_is_rejected()
    {
        using var db = CreateDb();
        var log = new AuditLog { Action = "Create", Module = "Test", EntityType = "X", Outcome = "Success" };
        db.AuditLogs.Add(log);
        db.SaveChanges();

        db.AuditLogs.Remove(log);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }
}
