using Baseera.Domain.Forms;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormSchemaSnapshotImmutabilityTests
{
    [Fact]
    public async Task DbContext_rejects_snapshot_update_and_delete_on_SaveChangesAsync()
    {
        await using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var user = FormTestFixtures.AddUser(db);
        var form = FormTestFixtures.NewForm(user.Id);
        db.FormDefinitions.Add(form);
        var version = new FormVersion
        {
            FormDefinitionId = form.Id,
            VersionNumber = 1,
            CreatedByUserId = user.Id,
            DraftSchemaJson = "{}",
            Status = FormVersionStatus.Locked
        };
        db.FormVersions.Add(version);
        await db.SaveChangesAsync();

        var snapshot = CreateSnapshot(version.Id, user.Id);
        db.FormSchemaSnapshots.Add(snapshot);
        version.SnapshotId = snapshot.Id;
        await db.SaveChangesAsync();

        db.Entry(snapshot).Property(s => s.SchemaHash).CurrentValue = "mutated";
        db.Entry(snapshot).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var tracked = await db.FormSchemaSnapshots.FirstAsync(s => s.Id == snapshot.Id);
        db.FormSchemaSnapshots.Remove(tracked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void DbContext_rejects_snapshot_update_on_sync_SaveChanges()
    {
        using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var user = FormTestFixtures.AddUser(db);
        var form = FormTestFixtures.NewForm(user.Id);
        db.FormDefinitions.Add(form);
        var version = new FormVersion
        {
            FormDefinitionId = form.Id,
            VersionNumber = 1,
            CreatedByUserId = user.Id,
            DraftSchemaJson = "{}",
            Status = FormVersionStatus.Locked
        };
        db.FormVersions.Add(version);
        db.SaveChanges();

        var snapshot = CreateSnapshot(version.Id, user.Id);
        db.FormSchemaSnapshots.Add(snapshot);
        version.SnapshotId = snapshot.Id;
        db.SaveChanges();

        db.Entry(snapshot).Property(s => s.SchemaHash).CurrentValue = "mutated";
        db.Entry(snapshot).State = EntityState.Modified;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void FormSchemaSnapshot_factory_rejects_invalid_input()
    {
        Assert.Throws<ArgumentException>(() => FormSchemaSnapshot.Create(new FormSchemaSnapshotData
        {
            FormVersionId = Guid.Empty,
            SchemaFormatVersion = 1,
            CanonicalSchemaJson = "{}",
            SchemaHash = "abc",
            SchemaSizeBytes = 1,
            PageCount = 0,
            SectionCount = 0,
            FieldCount = 0,
            CalculatedFieldCount = 0,
            ConditionCount = 0,
            CreatedByUserId = Guid.NewGuid()
        }));
        Assert.Throws<ArgumentException>(() => FormSchemaSnapshot.Create(new FormSchemaSnapshotData
        {
            FormVersionId = Guid.NewGuid(),
            SchemaFormatVersion = 1,
            CanonicalSchemaJson = "",
            SchemaHash = "abc",
            SchemaSizeBytes = 1,
            PageCount = 0,
            SectionCount = 0,
            FieldCount = 0,
            CalculatedFieldCount = 0,
            ConditionCount = 0,
            CreatedByUserId = Guid.NewGuid()
        }));
        Assert.Throws<ArgumentException>(() => FormSchemaSnapshot.Create(new FormSchemaSnapshotData
        {
            FormVersionId = Guid.NewGuid(),
            SchemaFormatVersion = 1,
            CanonicalSchemaJson = "{}",
            SchemaHash = "",
            SchemaSizeBytes = 1,
            PageCount = 0,
            SectionCount = 0,
            FieldCount = 0,
            CalculatedFieldCount = 0,
            ConditionCount = 0,
            CreatedByUserId = Guid.NewGuid()
        }));
    }

    private static FormSchemaSnapshot CreateSnapshot(Guid versionId, Guid userId) =>
        FormSchemaSnapshot.Create(new FormSchemaSnapshotData
        {
            FormVersionId = versionId,
            SchemaFormatVersion = 1,
            CanonicalSchemaJson = "{\"schemaFormatVersion\":1,\"pages\":[]}",
            SchemaHash = "abc",
            SchemaSizeBytes = 10,
            PageCount = 0,
            SectionCount = 0,
            FieldCount = 0,
            CalculatedFieldCount = 0,
            ConditionCount = 0,
            CreatedByUserId = userId
        });
}
