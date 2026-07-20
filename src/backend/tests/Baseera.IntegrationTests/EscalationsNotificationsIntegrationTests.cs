using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text.Json;
using Baseera.Application.Escalations;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    [IntegrationConnectionFact]
    public async Task Recipient_resolution_batches_user_scopes_and_preserves_scope_intersections()
    {
        var counter = new UserScopeCommandCounter();
        await using var scopedFactory = BaseeraApiFactory.WithInterceptor(counter);
        await scopedFactory.SeedUserAsync("batch-admin", "مسؤول التصعيد", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await scopedFactory.SeedUserAsync("batch-global", "وطني", [RoleCodes.FacilityDirector], (ScopeType.Global, null, null));
        await scopedFactory.SeedUserAsync("batch-region-a", "منطقة أ", [RoleCodes.FacilityDirector], (ScopeType.Region, SeedIds.RegionA, null));
        await scopedFactory.SeedUserAsync("batch-region-b", "منطقة ب", [RoleCodes.FacilityDirector], (ScopeType.Region, SeedIds.RegionB, null));
        await scopedFactory.SeedUserAsync("batch-fac-a", "سجن أ", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await scopedFactory.SeedUserAsync("batch-fac-b", "سجن ب", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));
        var unitId = await SeedFacilityUnitScopeAsync(scopedFactory, "batch-unit-a");

        var admin = scopedFactory.CreateAuthenticatedClient("batch-admin");
        await CreatePolicyWithRuleAsync(admin, EscalationTargetType.OperationalNote, EscalationTriggerType.DueSoon, 3, RoleCodes.FacilityDirector);
        await SeedDueNoteAsync(scopedFactory, "OBS-ESC-BATCH", DateTimeOffset.UtcNow.AddDays(1), NoteStatus.Open, ScopeType.FacilityUnit, SeedIds.RegionA, SeedIds.FacilityA1, unitId);

        counter.Reset();
        var response = await admin.PostAsync("/api/v1/escalations/run", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = scopedFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var recipients = await db.Notifications
            .Where(n => n.TargetReferenceNumber == "OBS-ESC-BATCH")
            .Join(db.Users, notification => notification.RecipientUserId, user => user.Id, (_, user) => user.Email)
            .OrderBy(email => email)
            .ToListAsync();

        Assert.Equal(
            [
                "batch-fac-a@test.local",
                "batch-global@test.local",
                "batch-region-a@test.local",
                "batch-unit-a@test.local"
            ],
            recipients);
        Assert.Equal(1, counter.UserScopeQueryCount);
    }

    [IntegrationConnectionFact]
    public async Task Mark_all_read_bulk_updates_unread_only_and_writes_single_audit()
    {
        await factory.SeedUserAsync("bulk-owner", "مالك الإشعارات", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await factory.SeedUserAsync("bulk-other", "مستخدم آخر", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await SeedNotificationsAsync("bulk-owner", "bulk-other", unreadCount: 25);

        var response = await factory.CreateAuthenticatedClient("bulk-owner").PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, body.GetProperty("count").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var ownerId = await db.Users.Where(u => u.ExternalSubject == "bulk-owner").Select(u => u.Id).FirstAsync();
        var otherId = await db.Users.Where(u => u.ExternalSubject == "bulk-other").Select(u => u.Id).FirstAsync();

        Assert.Equal(0, await db.Notifications.CountAsync(n => n.RecipientUserId == ownerId && n.Status == NotificationStatus.Unread));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.RecipientUserId == ownerId && n.Status == NotificationStatus.Read && n.ReadAtUtc == null));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.RecipientUserId == ownerId && n.Status == NotificationStatus.Archived && n.ReadAtUtc == null));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.RecipientUserId == otherId && n.Status == NotificationStatus.Unread));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "NotificationReadAll"));

        var second = await factory.CreateAuthenticatedClient("bulk-owner").PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, secondBody.GetProperty("count").GetInt32());
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "NotificationReadAll"));
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_lease_acquisition_allows_single_insert_and_existing_rules()
    {
        await using var scopedFactory = new BaseeraApiFactory();
        var jobName = $"lease-{Guid.NewGuid():N}";

        var firstResults = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => TryAcquireWithNewScopeAsync(scopedFactory, jobName, $"owner-{i}")));
        Assert.Equal(1, firstResults.Count(success => success));

        using (var scope = scopedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            Assert.Equal(1, await db.BackgroundJobLeases.CountAsync(l => l.JobName == jobName));
        }

        Assert.False(await TryAcquireWithNewScopeAsync(scopedFactory, jobName, "different-owner"));
        var winnerIndex = firstResults.Select((success, index) => (success, index)).First(item => item.success).index;
        Assert.True(await TryAcquireWithNewScopeAsync(scopedFactory, jobName, $"owner-{winnerIndex}"));

        using (var scope = scopedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            await db.BackgroundJobLeases
                .Where(l => l.JobName == jobName)
                .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.LeaseExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        Assert.True(await TryAcquireWithNewScopeAsync(scopedFactory, jobName, "takeover-owner"));
    }

    private static async Task<(Guid Id, string RowVersion)> CreatePolicyWithRuleAsync(
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

    private Task SeedDueNoteAsync(string reference, DateTimeOffset dueAtUtc, NoteStatus status) =>
        SeedDueNoteAsync(factory, reference, dueAtUtc, status, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null);

    private static async Task SeedDueNoteAsync(
        BaseeraApiFactory factory,
        string reference,
        DateTimeOffset dueAtUtc,
        NoteStatus status,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId)
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
            ScopeType = scopeType,
            RegionId = regionId,
            FacilityId = facilityId,
            FacilityUnitId = facilityUnitId,
            ReportedByUserId = reporter.Id,
            ReportedAtUtc = DateTimeOffset.UtcNow,
            DueAtUtc = dueAtUtc,
            SubmittedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedFacilityUnitScopeAsync(BaseeraApiFactory factory, string subject)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var unit = new FacilityUnit { FacilityId = SeedIds.FacilityA1, Code = $"UNIT-{Guid.NewGuid():N}"[..12], NameAr = "وحدة اختبار التصعيد" };
        db.FacilityUnits.Add(unit);
        await db.SaveChangesAsync();

        await factory.SeedUserAsync(subject, "وحدة أ", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var user = await db.Users.FirstAsync(u => u.ExternalSubject == subject);
        db.UserScopes.Add(new UserScope
        {
            UserId = user.Id,
            ScopeType = ScopeType.FacilityUnit,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = unit.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();
        return unit.Id;
    }

    private async Task SeedNotificationsAsync(string ownerSubject, string otherSubject, int unreadCount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var ownerId = await db.Users.Where(u => u.ExternalSubject == ownerSubject).Select(u => u.Id).FirstAsync();
        var otherId = await db.Users.Where(u => u.ExternalSubject == otherSubject).Select(u => u.Id).FirstAsync();
        for (var i = 0; i < unreadCount; i++)
        {
            db.Notifications.Add(NewNotification(ownerId, $"bulk-unread-{i}", NotificationStatus.Unread));
        }

        db.Notifications.Add(NewNotification(ownerId, "bulk-read", NotificationStatus.Read));
        db.Notifications.Add(NewNotification(ownerId, "bulk-archived", NotificationStatus.Archived));
        db.Notifications.Add(NewNotification(otherId, "bulk-other", NotificationStatus.Unread));
        await db.SaveChangesAsync();
    }

    private static Notification NewNotification(Guid recipientId, string key, NotificationStatus status) =>
        new()
        {
            RecipientUserId = recipientId,
            TargetType = EscalationTargetType.OperationalNote,
            TargetId = Guid.NewGuid(),
            TargetReferenceNumber = key,
            TitleAr = key,
            MessageAr = key,
            Priority = (int)CorrectiveActionPriority.Medium,
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            DeduplicationKey = key,
            Classification = Baseera.Domain.Attachments.ClassificationLevel.Internal
        };

    private static async Task<bool> TryAcquireWithNewScopeAsync(BaseeraApiFactory factory, string jobName, string owner)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBackgroundJobLeaseService>();
        return await service.TryAcquireAsync(jobName, owner, TimeSpan.FromMinutes(5));
    }

    private sealed class UserScopeCommandCounter : DbCommandInterceptor
    {
        public int UserScopeQueryCount { get; private set; }

        public void Reset() => UserScopeQueryCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CountUserScopeQuery(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CountUserScopeQuery(command);
            return ValueTask.FromResult(result);
        }

        private void CountUserScopeQuery(DbCommand command)
        {
            if (command.CommandText.Contains("[UserScopes]", StringComparison.OrdinalIgnoreCase) &&
                !command.CommandText.Contains("@user_Id", StringComparison.OrdinalIgnoreCase))
            {
                UserScopeQueryCount++;
            }
        }
    }
}
