using Baseera.Application.Escalations;
using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Escalations;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FluentValidation.TestHelper;

namespace Baseera.UnitTests;

public sealed class EscalationRuleLogicTests
{
    [Fact]
    public void DueSoon_matches_due_between_now_and_threshold()
    {
        var now = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
        Assert.True(EscalationRuleLogic.IsDueSoon(now.AddDays(2), now, 3));
        Assert.False(EscalationRuleLogic.IsDueSoon(now.AddDays(4), now, 3));
        Assert.False(EscalationRuleLogic.IsDueSoon(now.AddMinutes(-1), now, 3));
    }

    [Fact]
    public void DueSoon_zero_threshold_matches_the_current_riyadh_day()
    {
        var now = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        var earlierSameSaudiDay = new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero);
        var nextSaudiDay = new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero);

        Assert.True(EscalationRuleLogic.IsDueSoon(earlierSameSaudiDay, now, 0));
        Assert.False(EscalationRuleLogic.IsDueSoon(nextSaudiDay, now, 0));
    }

    [Fact]
    public void Due_today_in_riyadh_is_not_overdue()
    {
        var now = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        var dueEarlierSameSaudiDay = new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero);
        var dueYesterdaySaudi = new DateTimeOffset(2026, 7, 19, 20, 0, 0, TimeSpan.Zero);

        Assert.False(EscalationRuleLogic.IsOverdue(dueEarlierSameSaudiDay, now));
        Assert.True(EscalationRuleLogic.IsOverdue(dueYesterdaySaudi, now));
    }

    [Fact]
    public void Keys_include_target_cycle_to_separate_reopened_work()
    {
        var targetId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cycle1 = EscalationRuleLogic.TargetCycleKey(EscalationTargetType.OperationalNote, targetId, DateTimeOffset.Parse("2026-07-20T00:00:00Z"));
        var cycle2 = EscalationRuleLogic.TargetCycleKey(EscalationTargetType.OperationalNote, targetId, DateTimeOffset.Parse("2026-07-21T00:00:00Z"));

        var occurrence1 = EscalationRuleLogic.OccurrenceKey(ruleId, EscalationTargetType.OperationalNote, targetId, cycle1, 1);
        var occurrence2 = EscalationRuleLogic.OccurrenceKey(ruleId, EscalationTargetType.OperationalNote, targetId, cycle2, 1);

        Assert.NotEqual(occurrence1, occurrence2);
        Assert.Contains(cycle1, occurrence1, StringComparison.Ordinal);
        Assert.Contains(userId.ToString("N"), EscalationRuleLogic.DeduplicationKey(occurrence1, userId), StringComparison.Ordinal);
    }
}

public sealed class EscalationValidatorTests
{
    [Fact]
    public void Policy_validator_rejects_region_scope_without_region()
    {
        var validator = new CreateEscalationPolicyRequestValidator();
        var result = validator.TestValidate(new CreateEscalationPolicyRequest(
            "NOTE-DUE",
            "سياسة",
            null,
            EscalationTargetType.OperationalNote,
            ScopeType.Region,
            null,
            null,
            null));

        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Rule_validator_requires_role_for_role_strategy()
    {
        var validator = new CreateEscalationRuleRequestValidator();
        var result = validator.TestValidate(new CreateEscalationRuleRequest(
            1,
            2,
            EscalationTriggerType.DueSoon,
            3,
            1,
            2,
            EscalationRecipientStrategy.SpecificRoleInTargetScope,
            null,
            null,
            "تصعيد {reference}",
            "رسالة"));

        result.ShouldHaveValidationErrorFor(x => x.RecipientRoleCode);
    }
}

public sealed class EscalationPagingTests
{
    [Fact]
    public void Skip_calculation_clamps_large_pages_without_overflow()
    {
        Assert.Equal(int.MaxValue, new EscalationPolicyQuery { Page = int.MaxValue, PageSize = 200 }.Skip);
        Assert.Equal(int.MaxValue, new EscalationOccurrenceQuery { Page = int.MaxValue, PageSize = 200 }.Skip);
        Assert.Equal(int.MaxValue, new NotificationQuery { Page = int.MaxValue, PageSize = 200 }.Skip);
        Assert.Equal(0, new NotificationQuery { Page = int.MinValue, PageSize = 200 }.Skip);
    }
}

public sealed class BackgroundJobLeaseTests
{
    [Fact]
    public async Task Expired_lease_can_be_taken_over()
    {
        await using var db = NoteTestFixtures.CreateDb();
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-20T00:00:00Z"));
        var leases = new BackgroundJobLeaseService(db, time);

        Assert.True(await leases.TryAcquireAsync("EscalationProcessing", "worker-a", TimeSpan.FromMinutes(1)));
        Assert.False(await leases.TryAcquireAsync("EscalationProcessing", "worker-b", TimeSpan.FromMinutes(1)));

        time.UtcNow = time.UtcNow.AddMinutes(2);

        Assert.True(await leases.TryAcquireAsync("EscalationProcessing", "worker-b", TimeSpan.FromMinutes(1)));
        Assert.Equal("worker-b", db.BackgroundJobLeases.Single().LeaseOwner);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

public sealed class EscalationProcessorFailureTests
{
    [Fact]
    public async Task Candidate_failure_is_counted_and_next_candidate_continues()
    {
        await using var db = NoteTestFixtures.CreateDb();
        var now = DateTimeOffset.Parse("2026-07-20T00:00:00Z");
        var audit = new RecordingAudit(failFirstNotificationCreated: true);
        var processor = CreateProcessor(db, audit, now);
        SeedRunData(db, now, noteCount: 2);

        var result = await processor.RunAsync("worker-a");

        Assert.Equal(2, result.CandidatesEvaluated);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.OccurrencesCreated);
        Assert.Equal(1, result.NotificationsCreated);
        Assert.Equal(1, await db.EscalationOccurrences.CountAsync());
        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Contains(audit.Entries, entry => entry.Action == "EscalationCandidateFailed" && entry.Outcome == "Failed");
    }

    [Fact]
    public async Task Candidate_cancellation_is_rethrown()
    {
        await using var db = NoteTestFixtures.CreateDb();
        var now = DateTimeOffset.Parse("2026-07-20T00:00:00Z");
        var audit = new RecordingAudit(cancelOnNotificationCreated: true);
        var processor = CreateProcessor(db, audit, now);
        SeedRunData(db, now, noteCount: 1);

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.RunAsync("worker-a"));
    }

    private static EscalationProcessor CreateProcessor(BaseeraDbContext db, RecordingAudit audit, DateTimeOffset now)
    {
        var time = new MutableTimeProvider(now);
        return new EscalationProcessor(db, new BackgroundJobLeaseService(db, time), audit, time);
    }

    private static void SeedRunData(BaseeraDbContext db, DateTimeOffset now, int noteCount)
    {
        var user = NoteTestFixtures.AddUser(db, "مستلم التصعيد");
        NoteTestFixtures.GrantPermissions(db, user.Id, RoleCodes.FacilityDirector, PermissionCodes.NotificationsViewOwn);
        db.UserScopes.Add(new UserScope { UserId = user.Id, ScopeType = ScopeType.Global, IsActive = true });

        var policy = new EscalationPolicy
        {
            Code = $"POL-{Guid.NewGuid():N}",
            NameAr = "سياسة",
            TargetType = EscalationTargetType.OperationalNote,
            IsEnabled = true,
            ScopeType = ScopeType.Global,
            CreatedByUserId = user.Id,
            CreatedAtUtc = now
        };
        db.EscalationPolicies.Add(policy);
        db.EscalationRules.Add(new EscalationRule
        {
            EscalationPolicyId = policy.Id,
            Level = 1,
            Priority = 2,
            TriggerType = EscalationTriggerType.DueSoon,
            ThresholdDays = 3,
            RepeatEveryDays = 1,
            MaximumOccurrences = 2,
            RecipientStrategy = EscalationRecipientStrategy.FacilityDirector,
            TitleTemplateAr = "تصعيد {reference}",
            MessageTemplateAr = "رسالة {reference}",
            IsEnabled = true,
            CreatedAtUtc = now
        });

        for (var i = 0; i < noteCount; i++)
        {
            var note = NoteTestFixtures.NewNote(
                ScopeType.Global,
                user.Id,
                status: NoteStatus.Open,
                reference: $"OBS-FAIL-{i + 1:0000}");
            note.DueAtUtc = now.AddDays(1);
            db.OperationalNotes.Add(note);
        }

        db.SaveChanges();
    }

    private sealed class RecordingAudit(
        bool failFirstNotificationCreated = false,
        bool cancelOnNotificationCreated = false) : IAuditService
    {
        private bool _failed;
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry.Action == "NotificationCreated")
            {
                if (cancelOnNotificationCreated)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (failFirstNotificationCreated && !_failed)
                {
                    _failed = true;
                    throw new InvalidOperationException("Injected candidate failure.");
                }
            }

            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
