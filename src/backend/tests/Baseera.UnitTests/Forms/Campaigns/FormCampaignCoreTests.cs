using Baseera.Application.Abstractions;
using Baseera.Application.Forms.Campaigns;
using Baseera.Application.Security;
using Baseera.BackgroundJobs;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Infrastructure.Persistence;
using Baseera.UnitTests.Forms;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Baseera.UnitTests.Forms.Campaigns;

public sealed class FormCampaignStateMachineTests
{
    [Theory]
    [InlineData(FormCampaignStatus.Draft, FormCampaignStatus.Scheduled, true)]
    [InlineData(FormCampaignStatus.Draft, FormCampaignStatus.Active, true)]
    [InlineData(FormCampaignStatus.Scheduled, FormCampaignStatus.Active, true)]
    [InlineData(FormCampaignStatus.Active, FormCampaignStatus.Paused, true)]
    [InlineData(FormCampaignStatus.Paused, FormCampaignStatus.Active, true)]
    [InlineData(FormCampaignStatus.Cancelled, FormCampaignStatus.Active, false)]
    [InlineData(FormCampaignStatus.Completed, FormCampaignStatus.Active, false)]
    public void Campaign_transitions(FormCampaignStatus from, FormCampaignStatus to, bool expected) =>
        Assert.Equal(expected, FormCampaignStateMachine.CanTransition(from, to));

    [Fact]
    public void Draft_is_mutable_only()
    {
        Assert.True(FormCampaignStateMachine.IsMutable(FormCampaignStatus.Draft));
        Assert.False(FormCampaignStateMachine.IsMutable(FormCampaignStatus.Active));
    }
}

public sealed class FormRecurrenceCalculatorTests
{
    private readonly FormRecurrenceCalculator _calculator = new(new FormTimeZoneResolver());

    [Fact]
    public void Once_returns_single_occurrence()
    {
        var schedule = BaseSchedule(FormRecurrenceKind.Once, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)));
        var upcoming = _calculator.EnumerateUpcoming(schedule, schedule.FirstOpenAtLocal, 5);
        Assert.Single(upcoming);
    }

    [Fact]
    public void Daily_interval_respects_count()
    {
        var schedule = BaseSchedule(FormRecurrenceKind.Daily, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3))) with
        {
            IntervalDays = 2,
            MaxOccurrences = 3
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, schedule.FirstOpenAtLocal, 10);
        Assert.Equal(3, upcoming.Count);
        Assert.Equal(2, (upcoming[1] - upcoming[0]).TotalDays);
    }

    [Fact]
    public void Weekly_multiple_weekdays()
    {
        var start = new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Weekly, start) with
        {
            IntervalWeeks = 1,
            WeekDays = [DayOfWeek.Monday, DayOfWeek.Wednesday],
            MaxOccurrences = 4
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 4);
        Assert.Equal(4, upcoming.Count);
        Assert.All(upcoming, d => Assert.True(d.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday));
    }

    [Fact]
    public void Monthly_clamp_jan31_non_leap()
    {
        var start = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Monthly, start) with
        {
            DayOfMonth = 31,
            MissingDayPolicy = MonthlyMissingDayPolicy.ClampToLastDay,
            MaxOccurrences = 3
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 3);
        Assert.Equal(3, upcoming.Count);
        Assert.Equal(31, upcoming[0].Day);
        Assert.Equal(2, upcoming[1].Month);
        Assert.Equal(28, upcoming[1].Day);
        Assert.Equal(3, upcoming[2].Month);
        Assert.Equal(31, upcoming[2].Day);
    }

    [Fact]
    public void Monthly_clamp_jan31_leap_preserves_day_of_month()
    {
        var start = new DateTimeOffset(2024, 1, 31, 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Monthly, start) with
        {
            DayOfMonth = 31,
            MissingDayPolicy = MonthlyMissingDayPolicy.ClampToLastDay,
            MaxOccurrences = 3
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 3);
        Assert.Equal(3, upcoming.Count);
        Assert.Equal([31, 29, 31], upcoming.Select(d => d.Day).ToArray());
        Assert.Equal([1, 2, 3], upcoming.Select(d => d.Month).ToArray());
    }

    [Fact]
    public void Monthly_skip_missing_day()
    {
        var start = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Monthly, start) with
        {
            DayOfMonth = 31,
            MissingDayPolicy = MonthlyMissingDayPolicy.SkipOccurrence,
            MaxOccurrences = 3
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 3);
        Assert.DoesNotContain(upcoming, d => d.Month == 2);
        Assert.All(upcoming, d => Assert.Equal(31, d.Day));
    }

    [Fact]
    public void Custom_dates_unique_sorted()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var d2 = start.AddDays(2);
        var d1 = start.AddDays(1);
        var schedule = BaseSchedule(FormRecurrenceKind.CustomDates, start) with
        {
            CustomDatesLocal = [d2, d1, d1, start]
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 10);
        Assert.Equal([start, d1, d2], upcoming);
    }

    [Fact]
    public void Custom_dates_100_distinct_succeed()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var dates = Enumerable.Range(0, 100).Select(i => start.AddDays(i)).ToList();
        var schedule = BaseSchedule(FormRecurrenceKind.CustomDates, start) with { CustomDatesLocal = dates };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 100);
        Assert.Equal(100, upcoming.Count);
    }

    [Fact]
    public void Custom_dates_101_distinct_throws_with_arabic_max_message()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var dates = Enumerable.Range(0, 101).Select(i => start.AddDays(i)).ToList();
        var schedule = BaseSchedule(FormRecurrenceKind.CustomDates, start) with { CustomDatesLocal = dates };
        var ex = Assert.Throws<InvalidOperationException>(() => _calculator.EnumerateUpcoming(schedule, start, 101));
        Assert.Contains(FormRecurrenceCalculator.MaxCustomDates.ToString(), ex.Message, StringComparison.Ordinal);
        Assert.Contains("عدد التواريخ المخصصة", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_dates_101_items_with_duplicate_succeeds()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var dates = Enumerable.Range(0, 100).Select(i => start.AddDays(i)).ToList();
        dates.Add(dates[0]);
        var schedule = BaseSchedule(FormRecurrenceKind.CustomDates, start) with { CustomDatesLocal = dates };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 100);
        Assert.Equal(100, upcoming.Count);
    }

    [Fact]
    public void OccurrenceKey_is_deterministic()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var local = new DateTimeOffset(2026, 7, 1, 9, 30, 0, TimeSpan.FromHours(3));
        var a = _calculator.BuildOccurrenceKey(id, local, "Asia/Riyadh");
        var b = _calculator.BuildOccurrenceKey(id, local, "Asia/Riyadh");
        Assert.Equal(a, b);
        Assert.Contains("Asia/Riyadh", a, StringComparison.Ordinal);
    }

    [Fact]
    public void Asia_Riyadh_conversion_is_stable()
    {
        var resolver = new FormTimeZoneResolver();
        var local = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var utc = resolver.ToUtc(local, "Asia/Riyadh");
        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(9, resolver.ToLocal(utc, "Asia/Riyadh").Hour);
    }

    [Fact]
    public void Dst_invalid_time_is_normalized_for_New_York()
    {
        var resolver = new FormTimeZoneResolver();
        var tzId = OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York";
        var invalid = new DateTimeOffset(2026, 3, 8, 2, 30, 0, TimeSpan.FromHours(-5));
        var normalized = resolver.NormalizeLocal(invalid, tzId);
        Assert.NotEqual(2, normalized.Hour);
    }

    private static FormCampaignScheduleRequest BaseSchedule(FormRecurrenceKind kind, DateTimeOffset first) =>
        new(kind, first, 1440, 60, 0, BusinessDayAdjustment.None, 1, 1, null, null, null, null, null, null);
}

public sealed class FormCampaignScheduleValidatorTests
{
    private readonly FormCampaignScheduleRequestValidator _validator = new();

    [Fact]
    public void Custom_dates_validator_allows_duplicates_when_distinct_count_within_limit()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var dates = Enumerable.Range(0, 100).Select(i => start.AddDays(i)).ToList();
        dates.Add(dates[0]);
        var schedule = new FormCampaignScheduleRequest(
            FormRecurrenceKind.CustomDates, start, 1440, 60, 0, BusinessDayAdjustment.None,
            null, null, null, null, null, null, null, dates);
        var result = _validator.Validate(schedule);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Custom_dates_validator_rejects_101_distinct()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var dates = Enumerable.Range(0, 101).Select(i => start.AddDays(i)).ToList();
        var schedule = new FormCampaignScheduleRequest(
            FormRecurrenceKind.CustomDates, start, 1440, 60, 0, BusinessDayAdjustment.None,
            null, null, null, null, null, null, null, dates);
        var result = _validator.Validate(schedule);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains(FormRecurrenceCalculator.MaxCustomDates.ToString(), StringComparison.Ordinal));
    }
}

public sealed class BusinessCalendarTests
{
    private static BusinessCalendar CreateCalendar() => new(new ThrowingDbContext());

    [Fact]
    public void Adjust_none_returns_unchanged()
    {
        var calendar = CreateCalendar();
        var local = new DateTimeOffset(2026, 1, 4, 9, 0, 0, TimeSpan.FromHours(3));
        var snapshot = Snapshot(local);
        var adjusted = calendar.Adjust(local, BusinessDayAdjustment.None, snapshot);
        Assert.Equal(local, adjusted);
    }

    [Fact]
    public void Adjust_next_business_day_skips_friday_saturday_weekend()
    {
        var calendar = CreateCalendar();
        var friday = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.FromHours(3));
        var snapshot = Snapshot(friday);
        var adjusted = calendar.Adjust(friday, BusinessDayAdjustment.NextBusinessDay, snapshot);
        Assert.Equal(DayOfWeek.Sunday, adjusted.DayOfWeek);
        Assert.Equal(4, adjusted.Day);
    }

    [Fact]
    public void Adjust_weekend_working_override()
    {
        var calendar = CreateCalendar();
        var friday = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.FromHours(3));
        var date = DateOnly.FromDateTime(friday.DateTime);
        var snapshot = Snapshot(friday, new Dictionary<DateOnly, bool> { [date] = true });
        var adjusted = calendar.Adjust(friday, BusinessDayAdjustment.NextBusinessDay, snapshot);
        Assert.Equal(friday, adjusted);
    }

    [Fact]
    public void Adjust_weekday_holiday_override_moves_next_business_day()
    {
        var calendar = CreateCalendar();
        var thursday = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var date = DateOnly.FromDateTime(thursday.DateTime);
        var snapshot = Snapshot(thursday, new Dictionary<DateOnly, bool> { [date] = false });
        var adjusted = calendar.Adjust(thursday, BusinessDayAdjustment.NextBusinessDay, snapshot);
        Assert.Equal(DayOfWeek.Sunday, adjusted.DayOfWeek);
    }

    [Fact]
    public void Adjust_previous_business_day_skips_weekend()
    {
        var calendar = CreateCalendar();
        var saturday = new DateTimeOffset(2026, 1, 3, 9, 0, 0, TimeSpan.FromHours(3));
        var snapshot = Snapshot(saturday);
        var adjusted = calendar.Adjust(saturday, BusinessDayAdjustment.PreviousBusinessDay, snapshot);
        Assert.Equal(DayOfWeek.Thursday, adjusted.DayOfWeek);
        Assert.Equal(1, adjusted.Day);
    }

    [Fact]
    public void Adjust_does_not_query_database()
    {
        var calendar = CreateCalendar();
        var local = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.FromHours(3));
        var snapshot = Snapshot(local);
        var adjusted = calendar.Adjust(local, BusinessDayAdjustment.NextBusinessDay, snapshot);
        Assert.Equal(local, adjusted);
    }

    private static BusinessCalendarSnapshot Snapshot(
        DateTimeOffset local,
        IReadOnlyDictionary<DateOnly, bool>? overrides = null)
    {
        var date = DateOnly.FromDateTime(local.DateTime);
        return new BusinessCalendarSnapshot(Guid.NewGuid(), date, date, overrides ?? new Dictionary<DateOnly, bool>());
    }

    private sealed class ThrowingDbContext : EmptyDbStub
    {
        public override IQueryable<OrganizationBusinessCalendarDate> OrganizationBusinessCalendarDates =>
            throw new InvalidOperationException("Adjust must not query the database.");
    }

    private class EmptyDbStub : IBaseeraDbContext
    {
        public virtual IQueryable<OrganizationBusinessCalendarDate> OrganizationBusinessCalendarDates =>
            Enumerable.Empty<OrganizationBusinessCalendarDate>().AsQueryable();

        public IQueryable<Domain.Organization.Organization> Organizations => Empty<Domain.Organization.Organization>();
        public IQueryable<Domain.Organization.Region> Regions => Empty<Domain.Organization.Region>();
        public IQueryable<Domain.Organization.Facility> Facilities => Empty<Domain.Organization.Facility>();
        public IQueryable<Domain.Organization.FacilityUnit> FacilityUnits => Empty<Domain.Organization.FacilityUnit>();
        public IQueryable<Domain.Organization.Building> Buildings => Empty<Domain.Organization.Building>();
        public IQueryable<Domain.Organization.FacilityAssetLocation> FacilityAssetLocations => Empty<Domain.Organization.FacilityAssetLocation>();
        public IQueryable<Domain.Organization.Department> Departments => Empty<Domain.Organization.Department>();
        public IQueryable<Domain.Identity.User> Users => Empty<Domain.Identity.User>();
        public IQueryable<Domain.Identity.User> UsersIncludingDeleted => Users;
        public IQueryable<Domain.Identity.Role> Roles => Empty<Domain.Identity.Role>();
        public IQueryable<Domain.Identity.Role> RolesIncludingDeleted => Roles;
        public IQueryable<Domain.Identity.Permission> Permissions => Empty<Domain.Identity.Permission>();
        public IQueryable<Domain.Identity.UserRole> UserRoles => Empty<Domain.Identity.UserRole>();
        public IQueryable<Domain.Identity.RolePermission> RolePermissions => Empty<Domain.Identity.RolePermission>();
        public IQueryable<Domain.Identity.UserScope> UserScopes => Empty<Domain.Identity.UserScope>();
        public IQueryable<Domain.Audit.AuditLog> AuditLogs => Empty<Domain.Audit.AuditLog>();
        public IQueryable<Domain.Attachments.Attachment> Attachments => Empty<Domain.Attachments.Attachment>();
        public IQueryable<Domain.Notes.NoteType> NoteTypes => Empty<Domain.Notes.NoteType>();
        public IQueryable<Domain.Notes.RoleNoteTypeGrant> RoleNoteTypeGrants => Empty<Domain.Notes.RoleNoteTypeGrant>();
        public IQueryable<Domain.Notes.UserNoteTypeOverride> UserNoteTypeOverrides => Empty<Domain.Notes.UserNoteTypeOverride>();
        public IQueryable<Domain.Notes.UserNoteIntakeProfile> UserNoteIntakeProfiles => Empty<Domain.Notes.UserNoteIntakeProfile>();
        public IQueryable<Domain.Notes.NoteRoutingRule> NoteRoutingRules => Empty<Domain.Notes.NoteRoutingRule>();
        public IQueryable<Domain.Notes.NoteRoutingRule> NoteRoutingRulesIncludingDeleted => NoteRoutingRules;
        public IQueryable<Domain.Notes.NoteRoutingDecision> NoteRoutingDecisions => Empty<Domain.Notes.NoteRoutingDecision>();
        public IQueryable<Domain.Notes.NoteRoutingRuleHistory> NoteRoutingRuleHistories => Empty<Domain.Notes.NoteRoutingRuleHistory>();
        public IQueryable<Domain.Notes.NoteTypeAccessChangeHistory> NoteTypeAccessChangeHistories => Empty<Domain.Notes.NoteTypeAccessChangeHistory>();
        public IQueryable<Domain.Notes.OperationalNote> OperationalNotes => Empty<Domain.Notes.OperationalNote>();
        public IQueryable<Domain.Notes.OperationalNote> OperationalNotesIncludingDeleted => OperationalNotes;
        public IQueryable<Domain.Notes.NoteAssignment> NoteAssignments => Empty<Domain.Notes.NoteAssignment>();
        public IQueryable<Domain.Notes.NoteStatusHistory> NoteStatusHistories => Empty<Domain.Notes.NoteStatusHistory>();
        public IQueryable<Domain.CorrectiveActions.CorrectiveAction> CorrectiveActions => Empty<Domain.CorrectiveActions.CorrectiveAction>();
        public IQueryable<Domain.CorrectiveActions.CorrectiveAction> CorrectiveActionsIncludingDeleted => CorrectiveActions;
        public IQueryable<Domain.CorrectiveActions.CorrectiveActionAssignment> CorrectiveActionAssignments => Empty<Domain.CorrectiveActions.CorrectiveActionAssignment>();
        public IQueryable<Domain.CorrectiveActions.CorrectiveActionStatusHistory> CorrectiveActionStatusHistories => Empty<Domain.CorrectiveActions.CorrectiveActionStatusHistory>();
        public IQueryable<Domain.Escalations.EscalationPolicy> EscalationPolicies => Empty<Domain.Escalations.EscalationPolicy>();
        public IQueryable<Domain.Escalations.EscalationPolicy> EscalationPoliciesIncludingDeleted => EscalationPolicies;
        public IQueryable<Domain.Escalations.EscalationRule> EscalationRules => Empty<Domain.Escalations.EscalationRule>();
        public IQueryable<Domain.Escalations.EscalationRule> EscalationRulesIncludingDeleted => EscalationRules;
        public IQueryable<Domain.Escalations.EscalationOccurrence> EscalationOccurrences => Empty<Domain.Escalations.EscalationOccurrence>();
        public IQueryable<Domain.Escalations.Notification> Notifications => Empty<Domain.Escalations.Notification>();
        public IQueryable<Domain.Escalations.NotificationDeliveryAttempt> NotificationDeliveryAttempts => Empty<Domain.Escalations.NotificationDeliveryAttempt>();
        public IQueryable<Domain.Escalations.BackgroundJobLease> BackgroundJobLeases => Empty<Domain.Escalations.BackgroundJobLease>();
        public IQueryable<FormDefinition> FormDefinitions => Empty<FormDefinition>();
        public IQueryable<FormDefinition> FormDefinitionsIncludingDeleted => FormDefinitions;
        public IQueryable<FormReviewDecision> FormReviewDecisions => Empty<FormReviewDecision>();
        public IQueryable<FormGovernancePolicy> FormGovernancePolicies => Empty<FormGovernancePolicy>();
        public IQueryable<FormAccessGrant> FormAccessGrants => Empty<FormAccessGrant>();
        public IQueryable<FormAccessGrant> FormAccessGrantsIncludingDeleted => FormAccessGrants;
        public IQueryable<FormVersion> FormVersions => Empty<FormVersion>();
        public IQueryable<FormSchemaSnapshot> FormSchemaSnapshots => Empty<FormSchemaSnapshot>();
        public IQueryable<FormVersionReviewDecision> FormVersionReviewDecisions => Empty<FormVersionReviewDecision>();
        public IQueryable<FormTemplate> FormTemplates => Empty<FormTemplate>();
        public IQueryable<FormTemplate> FormTemplatesIncludingDeleted => FormTemplates;
        public IQueryable<FormDefinitionVersionCounter> FormDefinitionVersionCounters => Empty<FormDefinitionVersionCounter>();
        public IQueryable<FormCampaign> FormCampaigns => Empty<FormCampaign>();
        public IQueryable<FormCampaign> FormCampaignsIncludingDeleted => FormCampaigns;
        public IQueryable<FormTargetRule> FormTargetRules => Empty<FormTargetRule>();
        public IQueryable<FormCampaignExclusion> FormCampaignExclusions => Empty<FormCampaignExclusion>();
        public IQueryable<FormCycle> FormCycles => Empty<FormCycle>();
        public IQueryable<FormFacilityAssignment> FormFacilityAssignments => Empty<FormFacilityAssignment>();

        public void Add<TEntity>(TEntity entity) where TEntity : class { }
        public void Update<TEntity>(TEntity entity) where TEntity : class { }
        public void Remove<TEntity>(TEntity entity) where TEntity : class { }
        public void Detach<TEntity>(TEntity entity) where TEntity : class { }
        public void ClearChanges() { }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<long> NextOperationalNoteSequenceValueAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
        public Task<long> NextCorrectiveActionSequenceValueAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
        public Task<int> AllocateFormVersionNumberAsync(Guid formDefinitionId, CancellationToken cancellationToken = default) => Task.FromResult(1);

        private static IQueryable<T> Empty<T>() => Enumerable.Empty<T>().AsQueryable();
    }
}

public sealed class FormTargetResolverTests
{
    [Fact]
    public async Task AllFacilities_includes_inactive_as_unavailable()
    {
        var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var facility = await db.Facilities.FindAsync(SeedIds.FacilityA2);
        facility!.IsActive = false;
        await db.SaveChangesAsync();

        var user = FormTestFixtures.AddUser(db);
        var current = FormTestFixtures.CurrentUser(user.Id, [], new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var resolver = new FormTargetResolver(db, new OrganizationalScopeService(current, db));

        var result = await resolver.ResolveAsync(
            SeedIds.Organization,
            [new FormCampaignTargetRequest(FormTargetRuleType.AllFacilities, null, null, null)],
            []);

        var inactive = Assert.Single(result.Included, f => f.FacilityId == SeedIds.FacilityA2);
        Assert.False(inactive.IsAvailable);
        Assert.Equal("الموقع غير نشط", inactive.UnavailableReason);
    }

    [Fact]
    public async Task Dynamic_is_active_true_excludes_inactive_facilities()
    {
        var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var facility = await db.Facilities.FindAsync(SeedIds.FacilityA2);
        facility!.IsActive = false;
        await db.SaveChangesAsync();

        var user = FormTestFixtures.AddUser(db);
        var current = FormTestFixtures.CurrentUser(user.Id, [], new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var resolver = new FormTargetResolver(db, new OrganizationalScopeService(current, db));

        var result = await resolver.ResolveAsync(
            SeedIds.Organization,
            [new FormCampaignTargetRequest(FormTargetRuleType.DynamicCriteria, null, null, new DynamicCriteriaRequest(null, null, true))],
            []);

        Assert.DoesNotContain(result.Included, f => f.FacilityId == SeedIds.FacilityA2);
        Assert.All(result.Included, f => Assert.True(f.IsAvailable));
    }

    [Fact]
    public async Task Dynamic_is_active_false_returns_only_inactive_facilities()
    {
        var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var facility = await db.Facilities.FindAsync(SeedIds.FacilityA2);
        facility!.IsActive = false;
        await db.SaveChangesAsync();

        var user = FormTestFixtures.AddUser(db);
        var current = FormTestFixtures.CurrentUser(user.Id, [], new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var resolver = new FormTargetResolver(db, new OrganizationalScopeService(current, db));

        var result = await resolver.ResolveAsync(
            SeedIds.Organization,
            [new FormCampaignTargetRequest(FormTargetRuleType.DynamicCriteria, null, null, new DynamicCriteriaRequest(null, null, false))],
            []);

        Assert.Single(result.Included);
        Assert.Equal(SeedIds.FacilityA2, result.Included[0].FacilityId);
        Assert.False(result.Included[0].IsAvailable);
    }
}

public sealed class SqlServerUniqueConstraintDetectorTests
{
    [Fact]
    public void IsOccurrenceDuplicate_returns_true_for_occurrence_index_violation()
    {
        var inner = new FakeSqlException(2601, "Cannot insert duplicate key row in object 'dbo.FormCycles'. The duplicate key value is (..). The statement has been terminated. Violation of UNIQUE KEY constraint 'IX_FormCycles_CampaignId_OccurrenceKey'.");
        var ex = new DbUpdateException("duplicate", inner);
        Assert.True(SqlServerUniqueConstraintDetector.IsOccurrenceDuplicate(ex));
    }

    [Fact]
    public void IsOccurrenceDuplicate_returns_false_for_other_sql_errors()
    {
        var inner = new FakeSqlException(547, "The INSERT statement conflicted with the FOREIGN KEY constraint.");
        var ex = new DbUpdateException("fk", inner);
        Assert.False(SqlServerUniqueConstraintDetector.IsOccurrenceDuplicate(ex));
    }

    [Fact]
    public void IsOccurrenceDuplicate_returns_false_for_non_sql_inner_exception()
    {
        Assert.False(SqlServerUniqueConstraintDetector.IsOccurrenceDuplicate(new DbUpdateException("other", new InvalidOperationException("x"))));
    }

    private sealed class FakeSqlException(int number, string message) : Exception(message)
    {
        public int Number { get; } = number;
    }
}

public sealed class FormCampaignSchedulerHostOptionsTests
{
    [Fact]
    public void ValidateOnStart_rejects_invalid_options()
    {
        var services = new ServiceCollection();
        services
            .AddOptions<FormCampaignSchedulerHostOptions>()
            .Configure(o =>
            {
                o.BatchSize = 0;
                o.IntervalSeconds = 60;
                o.MaxCatchUpOccurrencesPerRun = 10;
                o.MaximumAttempts = 3;
                o.RetryBaseSeconds = 30;
            })
            .Validate(x => x.MaxCatchUpOccurrencesPerRun > 0, "MaxCatchUpOccurrencesPerRun must be greater than zero.")
            .Validate(x => x.BatchSize > 0, "BatchSize must be greater than zero.")
            .Validate(x => x.IntervalSeconds > 0, "IntervalSeconds must be greater than zero.")
            .Validate(x => x.MaximumAttempts > 0, "MaximumAttempts must be greater than zero.")
            .Validate(x => x.RetryBaseSeconds > 0, "RetryBaseSeconds must be greater than zero.")
            .ValidateOnStart();

        var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<FormCampaignSchedulerHostOptions>>().Value);
    }
}

public sealed class FormCampaignSchedulerTests
{
    [Fact]
    public async Task Future_occurrence_sets_next_only_and_keeps_scheduled()
    {
        var now = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var firstOpenLocal = new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(3));
        var timeZones = new FormTimeZoneResolver();
        var firstUtc = timeZones.ToUtc(firstOpenLocal, "Asia/Riyadh");

        var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var user = FormTestFixtures.AddUser(db);
        var campaign = SeedCampaign(db, user.Id, firstOpenLocal, firstUtc, now.AddHours(-1));
        await db.SaveChangesAsync();

        var cycleGen = new NoOpCycleGenerationService();
        var scheduler = new FormCampaignScheduler(
            db,
            cycleGen,
            new FormRecurrenceCalculator(timeZones),
            timeZones,
            new FormTestFixtures.NoOpAudit(),
            new MutableTimeProvider(now),
            NullLogger<FormCampaignScheduler>.Instance);

        var result = await scheduler.RunAsync("test", new FormCampaignSchedulerOptions(10, 5, 3, 30));

        Assert.Equal(0, result.CyclesCreated);
        Assert.Equal(0, cycleGen.CallCount);
        var tracked = await db.FormCampaigns.FindAsync(campaign.Id);
        Assert.NotNull(tracked);
        Assert.Equal(FormCampaignStatus.Scheduled, tracked!.Status);
        Assert.NotNull(tracked.NextOccurrenceUtc);
        Assert.True(tracked.NextOccurrenceUtc > now);
    }

    private static FormCampaign SeedCampaign(
        BaseeraDbContext db,
        Guid userId,
        DateTimeOffset firstOpenLocal,
        DateTimeOffset firstUtc,
        DateTimeOffset nextOccurrenceUtc)
    {
        var form = FormTestFixtures.NewForm(userId);
        db.FormDefinitions.Add(form);
        var versionId = Guid.NewGuid();
        var version = new FormVersion
        {
            Id = versionId,
            FormDefinitionId = form.Id,
            VersionNumber = 1,
            Status = FormVersionStatus.Locked,
            CreatedByUserId = userId
        };
        db.FormVersions.Add(version);
        var snapshot = FormSchemaSnapshot.Create(new FormSchemaSnapshotData
        {
            FormVersionId = versionId,
            SchemaFormatVersion = 1,
            CanonicalSchemaJson = "{}",
            SchemaHash = "abc123def4567890123456789012345678901234567890123456789012345678",
            SchemaSizeBytes = 2,
            PageCount = 1,
            SectionCount = 1,
            FieldCount = 1,
            CalculatedFieldCount = 0,
            ConditionCount = 0,
            CreatedByUserId = userId
        });
        db.FormSchemaSnapshots.Add(snapshot);
        version.SnapshotId = snapshot.Id;

        var campaign = new FormCampaign
        {
            OrganizationId = SeedIds.Organization,
            FormDefinitionId = form.Id,
            FormVersionId = version.Id,
            FormSchemaSnapshotId = snapshot.Id,
            SchemaHash = snapshot.SchemaHash,
            Code = "SCHED-TEST",
            NameAr = "اختبار مجدول",
            Status = FormCampaignStatus.Scheduled,
            TimeZoneId = "Asia/Riyadh",
            RecurrenceKind = FormRecurrenceKind.Daily,
            RecurrenceConfigurationJson = "{}",
            FirstOpenAtLocal = firstOpenLocal,
            ResponseWindowMinutes = 1440,
            GracePeriodMinutes = 60,
            CloseAfterMinutes = 0,
            NextOccurrenceUtc = nextOccurrenceUtc,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.FormCampaigns.Add(campaign);
        return campaign;
    }

    private sealed class NoOpCycleGenerationService : IFormCycleGenerationService
    {
        public int CallCount { get; private set; }

        public Task<CycleGenerationResult> TryGenerateOccurrenceAsync(
            FormCampaign campaign,
            DateTimeOffset occurrenceLocal,
            string generatedBy,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("Cycle generation should not run for future occurrences.");
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

public sealed class FormCampaignAllowedActionsTests
{
    [Fact]
    public void Different_forms_receive_their_own_publish_capability()
    {
        var formA = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Draft,
            canManageCampaigns: true,
            hasPublishPermission: true,
            hasPublishCapabilityOnForm: true,
            canPauseCampaign: false,
            canCancelCampaign: false,
            canViewAssignments: false);
        var formB = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Draft,
            canManageCampaigns: true,
            hasPublishPermission: true,
            hasPublishCapabilityOnForm: false,
            canPauseCampaign: false,
            canCancelCampaign: false,
            canViewAssignments: false);

        Assert.Contains("publish", formA);
        Assert.DoesNotContain("publish", formB);
        Assert.Equal(formA.Where(a => a != "publish"), formB);
    }

    [Fact]
    public void Multiple_drafts_for_same_form_share_publish_capability_result()
    {
        var sharedCapability = true;
        var first = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Draft, true, true, sharedCapability, false, false, false);
        var second = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Draft, true, true, sharedCapability, false, false, false);

        Assert.Equal(first, second);
        Assert.Contains("publish", first);
    }

    [Fact]
    public void Without_publish_permission_publish_action_is_absent()
    {
        var actions = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Draft,
            canManageCampaigns: true,
            hasPublishPermission: false,
            hasPublishCapabilityOnForm: true,
            canPauseCampaign: false,
            canCancelCampaign: false,
            canViewAssignments: false);

        Assert.DoesNotContain("publish", actions);
        Assert.Contains("edit", actions);
        Assert.Contains("preview", actions);
        Assert.Contains("clone", actions);
    }

    [Fact]
    public void Non_draft_actions_ignore_form_publish_capability()
    {
        var withCapability = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Active, true, true, true, true, true, true);
        var withoutCapability = FormCampaignAllowedActions.Build(
            FormCampaignStatus.Active, true, true, false, true, true, true);

        Assert.Equal(withCapability, withoutCapability);
        Assert.DoesNotContain("publish", withCapability);
        Assert.Contains("pause", withCapability);
        Assert.Contains("cancel", withCapability);
        Assert.Contains("complete", withCapability);
        Assert.Contains("clone", withCapability);
        Assert.Contains("viewAssignments", withCapability);
    }
}
