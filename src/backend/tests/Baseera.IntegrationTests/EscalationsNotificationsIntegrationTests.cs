using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Escalations;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class EscalationsNotificationsIntegrationTests(BaseeraApiFactory factory) : IClassFixture<BaseeraApiFactory>
{
    [IntegrationConnectionFact]
    public async Task Due_soon_note_run_creates_in_app_notification_once()
    {
        await factory.SeedUserAsync("esc-admin", "مسؤول التصعيد", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await factory.SeedUserAsync("esc-facility-director", "مدير السجن", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var admin = factory.CreateAuthenticatedClient("esc-admin");

        var policy = await CreatePolicyWithRuleAsync(admin, EscalationTargetType.OperationalNote, EscalationTriggerType.DueSoon, 3, RoleCodes.FacilityDirector);
        await SeedDueNoteAsync("OBS-ESC-0001", DateTimeOffset.UtcNow.AddDays(2), NoteStatus.Open);

        var firstRun = await admin.PostAsync("/api/v1/escalations/run", null);
        Assert.Equal(HttpStatusCode.OK, firstRun.StatusCode);
        var secondRun = await admin.PostAsync("/api/v1/escalations/run", null);
        Assert.Equal(HttpStatusCode.OK, secondRun.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(1, await db.EscalationOccurrences.CountAsync(o => o.PolicyId == policy.Id));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.TargetReferenceNumber == "OBS-ESC-0001"));

        var inbox = await factory.CreateAuthenticatedClient("esc-facility-director").GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Equal(1, inbox.GetProperty("totalCount").GetInt32());
    }

    [IntegrationConnectionFact]
    public async Task Notification_owner_only_can_read_and_archive_with_rowversion()
    {
        await factory.SeedUserAsync("notif-admin", "مسؤول التصعيد", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await factory.SeedUserAsync("notif-owner", "مالك الإشعار", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("notif-other", "مستخدم آخر", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var admin = factory.CreateAuthenticatedClient("notif-admin");

        await CreatePolicyWithRuleAsync(admin, EscalationTargetType.OperationalNote, EscalationTriggerType.DueSoon, 3, RoleCodes.FacilityDirector);
        await SeedDueNoteAsync("OBS-ESC-0002", DateTimeOffset.UtcNow.AddDays(1), NoteStatus.Open);
        await admin.PostAsync("/api/v1/escalations/run", null);

        var owner = factory.CreateAuthenticatedClient("notif-owner");
        var inbox = await owner.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        var notification = inbox.GetProperty("items")[0];
        var id = notification.GetProperty("id").GetGuid();
        var rowVersion = notification.GetProperty("rowVersion").GetString();

        var otherResponse = await factory.CreateAuthenticatedClient("notif-other").GetAsync($"/api/v1/notifications/{id}");
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);

        var read = await owner.PostAsJsonAsync($"/api/v1/notifications/{id}/read", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var readBody = await read.Content.ReadFromJsonAsync<JsonElement>();
        var archive = await owner.PostAsJsonAsync($"/api/v1/notifications/{id}/archive", new { rowVersion = readBody.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Completed_cancelled_and_closed_targets_are_not_escalated()
    {
        await factory.SeedUserAsync("esc-terminal-admin", "مسؤول التصعيد", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await factory.SeedUserAsync("esc-terminal-director", "مدير السجن", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var admin = factory.CreateAuthenticatedClient("esc-terminal-admin");

        await CreatePolicyWithRuleAsync(admin, EscalationTargetType.OperationalNote, EscalationTriggerType.Overdue, 0, RoleCodes.FacilityDirector);
        await SeedDueNoteAsync("OBS-ESC-CLOSED", DateTimeOffset.UtcNow.AddDays(-2), NoteStatus.Closed);
        await SeedDueNoteAsync("OBS-ESC-CANCELLED", DateTimeOffset.UtcNow.AddDays(-2), NoteStatus.Cancelled);

        await admin.PostAsync("/api/v1/escalations/run", null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(0, await db.Notifications.CountAsync(n => n.TargetReferenceNumber.StartsWith("OBS-ESC-C")));
    }

    private async Task<(Guid Id, string RowVersion)> CreatePolicyWithRuleAsync(
        HttpClient admin,
        EscalationTargetType targetType,
        EscalationTriggerType triggerType,
        int thresholdDays,
        string roleCode)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var created = await admin.PostAsJsonAsync("/api/v1/escalation-policies", new
        {
            code = $"POL-{suffix}",
            nameAr = "سياسة اختبار",
            targetType,
            scopeType = ScopeType.Global
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var policy = await created.Content.ReadFromJsonAsync<JsonElement>();
        var policyId = policy.GetProperty("id").GetGuid();

        var rule = await admin.PostAsJsonAsync($"/api/v1/escalation-policies/{policyId}/rules", new
        {
            level = 1,
            priority = 2,
            triggerType,
            thresholdDays,
            repeatEveryDays = 1,
            maximumOccurrences = 2,
            recipientStrategy = EscalationRecipientStrategy.SpecificRoleInTargetScope,
            recipientRoleCode = roleCode,
            titleTemplateAr = "تصعيد {reference}",
            messageTemplateAr = "يوجد {targetType} يتطلب المتابعة"
        });
        Assert.Equal(HttpStatusCode.Created, rule.StatusCode);

        var activated = await admin.PostAsJsonAsync($"/api/v1/escalation-policies/{policyId}/activate", new
        {
            rowVersion = policy.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var activatedPolicy = await activated.Content.ReadFromJsonAsync<JsonElement>();
        return (policyId, activatedPolicy.GetProperty("rowVersion").GetString()!);
    }

    private async Task SeedDueNoteAsync(string reference, DateTimeOffset dueAtUtc, NoteStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var reporter = await db.Users.FirstAsync(u => u.ExternalSubject.EndsWith("admin"));
        db.OperationalNotes.Add(new OperationalNote
        {
            ReferenceNumber = reference,
            Title = reference,
            Description = "ملاحظة لاختبار التصعيد",
            Category = NoteCategory.Operational,
            Severity = NoteSeverity.High,
            Status = status,
            SourceType = NoteSourceType.Manual,
            Classification = Baseera.Domain.Attachments.ClassificationLevel.Internal,
            ScopeType = ScopeType.Facility,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            ReportedByUserId = reporter.Id,
            ReportedAtUtc = DateTimeOffset.UtcNow,
            DueAtUtc = dueAtUtc,
            SubmittedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
    }
}
