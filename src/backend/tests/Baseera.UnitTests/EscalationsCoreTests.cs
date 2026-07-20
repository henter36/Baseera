using Baseera.Application.Escalations;
using Baseera.Domain.Common;
using Baseera.Domain.Escalations;
using Baseera.Infrastructure.Persistence;
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
