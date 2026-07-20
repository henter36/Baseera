using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class CorrectiveActionsCoreIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public CorrectiveActionsCoreIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Create_list_and_detail_are_scoped_to_parent_note()
    {
        await _factory.SeedUserAsync("ca-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-region-b", "منطقة ب", [RoleCodes.RegionalDirector], (ScopeType.Region, SeedIds.RegionB, null));

        var admin = _factory.CreateAuthenticatedClient("ca-admin");
        var note = await CreateOpenNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "ملاحظة أ");

        var created = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title = "إصلاح إجراء",
            description = "وصف إجراء تصحيحي",
            priority = CorrectiveActionPriority.High,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var action = await ReadObjectAsync(created);
        Assert.StartsWith("CA-", action.GetProperty("referenceNumber").GetString());

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{note.Id}/corrective-actions");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());

        var outOfScope = _factory.CreateAuthenticatedClient("ca-region-b");
        var detail = await outOfScope.GetAsync($"/api/v1/corrective-actions/{action.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Create_under_draft_note_is_rejected()
    {
        await _factory.SeedUserAsync("ca-draft-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-draft-admin");
        var note = await CreateDraftNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "مسودة");

        var response = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title = "إجراء",
            description = "وصف",
            priority = CorrectiveActionPriority.Medium
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Note_closure_is_blocked_until_corrective_actions_are_completed_or_cancelled()
    {
        await _factory.SeedUserAsync("ca-guard-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-guard-admin");
        var note = await CreateOpenNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "ملاحظة حارس");

        var created = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title = "إجراء مانع",
            description = "وصف",
            priority = CorrectiveActionPriority.Medium
        });
        var action = await ReadObjectAsync(created);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var guardedNote = await db.OperationalNotes.SingleAsync(n => n.Id == note.Id);
            guardedNote.Status = NoteStatus.PendingVerification;
            var entity = await db.CorrectiveActions.SingleAsync(a => a.Id == action.GetProperty("id").GetGuid());
            entity.Status = CorrectiveActionStatus.Open;
            await db.SaveChangesAsync();
        }

        var pending = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{note.Id}");
        var blocked = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد",
            closureSummary = "ملخص",
            rowVersion = pending.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var problem = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("إجراء تصحيحي", problem.GetProperty("detail").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var entity = await db.CorrectiveActions.SingleAsync(a => a.Id == action.GetProperty("id").GetGuid());
            entity.Status = CorrectiveActionStatus.Completed;
            await db.SaveChangesAsync();
        }

        var refreshed = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{note.Id}");
        var closed = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد",
            closureSummary = "ملخص",
            rowVersion = refreshed.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
    }

    private static async Task<(Guid Id, string RowVersion)> CreateOpenNoteAsync(HttpClient client, ScopeType scopeType, Guid? regionId, Guid? facilityId, string title)
    {
        var note = await CreateDraftNoteAsync(client, scopeType, regionId, facilityId, title);
        var opened = await client.PostAsJsonAsync($"/api/v1/notes/{note.Id}/submit", new { reason = "تقديم", rowVersion = note.RowVersion });
        var body = await ReadObjectAsync(opened);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("rowVersion").GetString()!);
    }

    private static async Task<(Guid Id, string RowVersion)> CreateDraftNoteAsync(HttpClient client, ScopeType scopeType, Guid? regionId, Guid? facilityId, string title)
    {
        var created = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف تشغيلي كاف",
            category = NoteCategory.Operational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            classification = 1,
            scopeType,
            regionId,
            facilityId,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(5)
        });
        var body = await ReadObjectAsync(created);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("rowVersion").GetString()!);
    }

    private static async Task<JsonElement> ReadObjectAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
