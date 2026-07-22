using Baseera.Domain.Forms;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests.Forms.Schema;

public sealed class FormSchemaSnapshotImmutabilityTests
{
    [Fact]
    public async Task DbContext_rejects_snapshot_update_and_delete()
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

        var snapshot = new FormSchemaSnapshot
        {
            FormVersionId = version.Id,
            SchemaFormatVersion = 1,
            CanonicalSchemaJson = "{\"schemaFormatVersion\":1,\"pages\":[]}",
            SchemaHash = "abc",
            SchemaSizeBytes = 10,
            CreatedByUserId = user.Id
        };
        db.FormSchemaSnapshots.Add(snapshot);
        version.SnapshotId = snapshot.Id;
        await db.SaveChangesAsync();

        snapshot.SchemaHash = "mutated";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var tracked = await db.FormSchemaSnapshots.FirstAsync(s => s.Id == snapshot.Id);
        db.FormSchemaSnapshots.Remove(tracked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
