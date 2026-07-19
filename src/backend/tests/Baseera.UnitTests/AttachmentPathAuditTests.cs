using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Audit;
using Baseera.Application.Common;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Attachments;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baseera.UnitTests;

public sealed class StoragePathGuardTests
{
    [Fact]
    public void Accepts_paths_under_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "baseera-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var child = Path.Combine(root, "a", "b.txt");
        Assert.True(StoragePathGuard.IsPathInsideRoot(root, child));
    }

    [Fact]
    public void Rejects_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "baseera-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var outside = Path.Combine(root, "..", "sibling.txt");
        Assert.False(StoragePathGuard.IsPathInsideRoot(root, outside));
    }

    [Fact]
    public void Rejects_sibling_prefix_collision()
    {
        var baseTemp = Path.GetTempPath();
        var root = Path.Combine(baseTemp, "baseera-store");
        var sibling = Path.Combine(baseTemp, "baseera-store-evil", "x.bin");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);
        Assert.False(StoragePathGuard.IsPathInsideRoot(root, sibling));
    }
}

public sealed class AttachmentEntityScopeTests
{
    [Theory]
    [InlineData("Organization")]
    [InlineData("Region")]
    [InlineData("Facility")]
    [InlineData("FacilityUnit")]
    [InlineData("Building")]
    [InlineData("Department")]
    [InlineData("User")]
    public async Task Missing_entity_is_not_in_scope(string entityType)
    {
        await using var db = CreateDb();
        var service = CreateService(db, FakeUser(Guid.NewGuid(), true, true, []));
        var access = await service.ResolveEntityAccessAsync(entityType, Guid.NewGuid(), CancellationToken.None);
        Assert.False(access.Exists);
        Assert.False(access.InScope);
    }

    [Fact]
    public async Task Global_user_cannot_attach_orphan_facility()
    {
        await using var db = CreateDb();
        var service = CreateService(db, FakeUser(Guid.NewGuid(), true, true, []));
        var access = await service.ResolveEntityAccessAsync("Facility", Guid.NewGuid(), CancellationToken.None);
        Assert.False(access.Exists);
    }

    [Fact]
    public async Task Headquarters_user_cannot_attach_orphan_organization()
    {
        await using var db = CreateDb();
        var service = CreateService(
            db,
            FakeUser(Guid.NewGuid(), false, true, [new UserScopeSnapshot(ScopeType.Headquarters, null, null, null)]));
        var access = await service.ResolveEntityAccessAsync("Organization", Guid.NewGuid(), CancellationToken.None);
        Assert.False(access.Exists);
    }

    [Fact]
    public async Task Facility_user_out_of_scope_facility_rejected()
    {
        await using var db = CreateDb();
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        await db.SaveChangesAsync();

        var service = CreateService(
            db,
            FakeUser(Guid.NewGuid(), false, false,
                [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]));
        var access = await service.ResolveEntityAccessAsync("Facility", SeedIds.FacilityB1, CancellationToken.None);
        Assert.True(access.Exists);
        Assert.False(access.InScope);
    }

    [Fact]
    public async Task Facility_user_in_scope_facility_allowed()
    {
        await using var db = CreateDb();
        db.Facilities.Add(new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" });
        await db.SaveChangesAsync();
        var service = CreateService(
            db,
            FakeUser(Guid.NewGuid(), false, false,
                [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]));
        var access = await service.ResolveEntityAccessAsync("Facility", SeedIds.FacilityA1, CancellationToken.None);
        Assert.True(access.Exists);
        Assert.True(access.InScope);
    }

    private static AttachmentService CreateService(BaseeraDbContext db, ICurrentUser user) =>
        new(db, new NoopStorage(), user, new OrganizationalScopeService(user, db), new NoopAudit());

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }

    private static ICurrentUser FakeUser(
        Guid id,
        bool global,
        bool hq,
        IReadOnlyCollection<UserScopeSnapshot> scopes)
    {
        var list = scopes.ToList();
        if (global) list.Add(new UserScopeSnapshot(ScopeType.Global, null, null, null));
        else if (hq && list.All(s => s.ScopeType != ScopeType.Headquarters))
            list.Add(new UserScopeSnapshot(ScopeType.Headquarters, null, null, null));
        return new FakeCurrentUser(true, id, "u", "u", Array.Empty<string>(), list);
    }

    private sealed class NoopStorage : IFileStorage
    {
        public Task<StoredFileResult> SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoredFileResult("/tmp/" + storedFileName));
        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public sealed class AuditQueryScopeTests
{
    [Fact]
    public async Task Regional_auditor_cannot_list_national_audit()
    {
        await using var db = CreateDb();
        var user = new FakeCurrentUser(
            true, Guid.NewGuid(), "r", "r",
            [PermissionCodes.AuditView],
            [new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null)]);
        var svc = new AuditQueryService(db, user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.ListAsync(new PagedQuery { Page = 1, PageSize = 10 }, null));
    }

    [Fact]
    public async Task Global_auditor_can_list()
    {
        await using var db = CreateDb();
        var user = new FakeCurrentUser(
            true, Guid.NewGuid(), "g", "g",
            [PermissionCodes.AuditView],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var svc = new AuditQueryService(db, user);
        var page = await svc.ListAsync(new PagedQuery { Page = 1, PageSize = 10 }, null);
        Assert.NotNull(page);
    }

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }
}
