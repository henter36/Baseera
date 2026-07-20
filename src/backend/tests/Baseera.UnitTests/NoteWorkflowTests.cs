using Baseera.Application.Notes;
using Baseera.Domain.Notes;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

// NOTE: NoteStateMachine transition/overdue/reference-format coverage lives in
// NoteStateMachineTests.cs (more exhaustive over the full transition matrix).

public sealed class NoteScopeShapeTests
{
    [Fact]
    public void Global_rejects_org_ids()
    {
        var service = new NoteScopeService(
            new OrganizationalScopeStub(),
            new FakeCurrentUser(true, Guid.NewGuid(), "u", "u", [], []),
            new EmptyDb());

        Assert.Throws<InvalidOperationException>(() =>
            service.ValidateScopeShape(Domain.Common.ScopeType.Global, Guid.NewGuid(), null, null));
    }

    [Fact]
    public void Region_requires_region_only()
    {
        var service = new NoteScopeService(
            new OrganizationalScopeStub(),
            new FakeCurrentUser(true, Guid.NewGuid(), "u", "u", [], []),
            new EmptyDb());

        service.ValidateScopeShape(Domain.Common.ScopeType.Region, Guid.NewGuid(), null, null);
        Assert.Throws<InvalidOperationException>(() =>
            service.ValidateScopeShape(Domain.Common.ScopeType.Region, Guid.NewGuid(), Guid.NewGuid(), null));
    }

    [Fact]
    public void Multiple_scopes_rejected()
    {
        var service = new NoteScopeService(
            new OrganizationalScopeStub(),
            new FakeCurrentUser(true, Guid.NewGuid(), "u", "u", [], []),
            new EmptyDb());

        Assert.Throws<InvalidOperationException>(() =>
            service.ValidateScopeShape(Domain.Common.ScopeType.MultipleRegions, Guid.NewGuid(), null, null));
    }

    private sealed class OrganizationalScopeStub : Application.Abstractions.IOrganizationalScopeService
    {
        public bool HasNationalAccess => true;
        public bool HasHeadquartersAccess => true;
        public bool CanAccessRegion(Guid regionId) => true;
        public bool CanAccessFacility(Guid facilityId) => true;
        public bool CanAccessFacilityUnit(Guid facilityUnitId) => true;
        public IQueryable<Domain.Organization.Region> FilterRegions(IQueryable<Domain.Organization.Region> query) => query;
        public IQueryable<Domain.Organization.Facility> FilterFacilities(IQueryable<Domain.Organization.Facility> query) => query;
        public bool CanAccess(Domain.Common.IScopedEntity entity) => true;
        public string SummarizeScopes() => "Global";
    }

    private sealed class EmptyDb : Application.Abstractions.IBaseeraDbContext
    {
        public IQueryable<Domain.Organization.Organization> Organizations => Enumerable.Empty<Domain.Organization.Organization>().AsQueryable();
        public IQueryable<Domain.Organization.Region> Regions => Enumerable.Empty<Domain.Organization.Region>().AsQueryable();
        public IQueryable<Domain.Organization.Facility> Facilities => Enumerable.Empty<Domain.Organization.Facility>().AsQueryable();
        public IQueryable<Domain.Organization.FacilityUnit> FacilityUnits => Enumerable.Empty<Domain.Organization.FacilityUnit>().AsQueryable();
        public IQueryable<Domain.Organization.Building> Buildings => Enumerable.Empty<Domain.Organization.Building>().AsQueryable();
        public IQueryable<Domain.Organization.FacilityAssetLocation> FacilityAssetLocations => Enumerable.Empty<Domain.Organization.FacilityAssetLocation>().AsQueryable();
        public IQueryable<Domain.Organization.Department> Departments => Enumerable.Empty<Domain.Organization.Department>().AsQueryable();
        public IQueryable<Domain.Identity.User> Users => Enumerable.Empty<Domain.Identity.User>().AsQueryable();
        public IQueryable<Domain.Identity.User> UsersIncludingDeleted => Users;
        public IQueryable<Domain.Identity.Role> Roles => Enumerable.Empty<Domain.Identity.Role>().AsQueryable();
        public IQueryable<Domain.Identity.Permission> Permissions => Enumerable.Empty<Domain.Identity.Permission>().AsQueryable();
        public IQueryable<Domain.Identity.UserRole> UserRoles => Enumerable.Empty<Domain.Identity.UserRole>().AsQueryable();
        public IQueryable<Domain.Identity.RolePermission> RolePermissions => Enumerable.Empty<Domain.Identity.RolePermission>().AsQueryable();
        public IQueryable<Domain.Identity.UserScope> UserScopes => Enumerable.Empty<Domain.Identity.UserScope>().AsQueryable();
        public IQueryable<Domain.Audit.AuditLog> AuditLogs => Enumerable.Empty<Domain.Audit.AuditLog>().AsQueryable();
        public IQueryable<Domain.Attachments.Attachment> Attachments => Enumerable.Empty<Domain.Attachments.Attachment>().AsQueryable();
        public IQueryable<NoteType> NoteTypes => Enumerable.Empty<NoteType>().AsQueryable();
        public IQueryable<RoleNoteTypeGrant> RoleNoteTypeGrants => Enumerable.Empty<RoleNoteTypeGrant>().AsQueryable();
        public IQueryable<UserNoteTypeOverride> UserNoteTypeOverrides => Enumerable.Empty<UserNoteTypeOverride>().AsQueryable();
        public IQueryable<UserNoteIntakeProfile> UserNoteIntakeProfiles => Enumerable.Empty<UserNoteIntakeProfile>().AsQueryable();
        public IQueryable<NoteRoutingRule> NoteRoutingRules => Enumerable.Empty<NoteRoutingRule>().AsQueryable();
        public IQueryable<NoteRoutingRule> NoteRoutingRulesIncludingDeleted => NoteRoutingRules;
        public IQueryable<NoteRoutingDecision> NoteRoutingDecisions => Enumerable.Empty<NoteRoutingDecision>().AsQueryable();
        public IQueryable<NoteRoutingRuleHistory> NoteRoutingRuleHistories => Enumerable.Empty<NoteRoutingRuleHistory>().AsQueryable();
        public IQueryable<NoteTypeAccessChangeHistory> NoteTypeAccessChangeHistories => Enumerable.Empty<NoteTypeAccessChangeHistory>().AsQueryable();
        public IQueryable<OperationalNote> OperationalNotes => Enumerable.Empty<OperationalNote>().AsQueryable();
        public IQueryable<OperationalNote> OperationalNotesIncludingDeleted => OperationalNotes;
        public IQueryable<NoteAssignment> NoteAssignments => Enumerable.Empty<NoteAssignment>().AsQueryable();
        public IQueryable<NoteStatusHistory> NoteStatusHistories => Enumerable.Empty<NoteStatusHistory>().AsQueryable();
        public IQueryable<Domain.CorrectiveActions.CorrectiveAction> CorrectiveActions => Enumerable.Empty<Domain.CorrectiveActions.CorrectiveAction>().AsQueryable();
        public IQueryable<Domain.CorrectiveActions.CorrectiveAction> CorrectiveActionsIncludingDeleted => CorrectiveActions;
        public IQueryable<Domain.CorrectiveActions.CorrectiveActionAssignment> CorrectiveActionAssignments => Enumerable.Empty<Domain.CorrectiveActions.CorrectiveActionAssignment>().AsQueryable();
        public IQueryable<Domain.CorrectiveActions.CorrectiveActionStatusHistory> CorrectiveActionStatusHistories => Enumerable.Empty<Domain.CorrectiveActions.CorrectiveActionStatusHistory>().AsQueryable();
        public IQueryable<Domain.Escalations.EscalationPolicy> EscalationPolicies => Enumerable.Empty<Domain.Escalations.EscalationPolicy>().AsQueryable();
        public IQueryable<Domain.Escalations.EscalationPolicy> EscalationPoliciesIncludingDeleted => EscalationPolicies;
        public IQueryable<Domain.Escalations.EscalationRule> EscalationRules => Enumerable.Empty<Domain.Escalations.EscalationRule>().AsQueryable();
        public IQueryable<Domain.Escalations.EscalationRule> EscalationRulesIncludingDeleted => EscalationRules;
        public IQueryable<Domain.Escalations.EscalationOccurrence> EscalationOccurrences => Enumerable.Empty<Domain.Escalations.EscalationOccurrence>().AsQueryable();
        public IQueryable<Domain.Escalations.Notification> Notifications => Enumerable.Empty<Domain.Escalations.Notification>().AsQueryable();
        public IQueryable<Domain.Escalations.NotificationDeliveryAttempt> NotificationDeliveryAttempts => Enumerable.Empty<Domain.Escalations.NotificationDeliveryAttempt>().AsQueryable();
        public IQueryable<Domain.Escalations.BackgroundJobLease> BackgroundJobLeases => Enumerable.Empty<Domain.Escalations.BackgroundJobLease>().AsQueryable();
        public void Add<TEntity>(TEntity entity) where TEntity : class { }
        public void Update<TEntity>(TEntity entity) where TEntity : class { }
        public void Detach<TEntity>(TEntity entity) where TEntity : class { }
        public void ClearChanges() { }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<long> NextOperationalNoteSequenceValueAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
        public Task<long> NextCorrectiveActionSequenceValueAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
    }
}

public sealed class NoteValidatorTests
{
    [Fact]
    public void Create_rejects_whitespace_title()
    {
        var validator = new CreateNoteRequestValidator();
        var result = validator.TestValidate(new CreateNoteRequest(
            "   ",
            "وصف صالح للملاحظة",
            NoteTestFixtures.DefaultNoteTypeId,
            NoteSeverity.Medium,
            NoteSourceType.Manual,
            null,
            Domain.Attachments.ClassificationLevel.Internal,
            Domain.Common.ScopeType.Global,
            null, null, null, null, null));
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Assign_requires_xor_target()
    {
        var validator = new AssignNoteRequestValidator();
        var both = validator.TestValidate(new AssignNoteRequest(Guid.NewGuid(), Guid.NewGuid(), null, "سبب", "AAA="));
        both.ShouldHaveValidationErrorFor(x => x);
        var neither = validator.TestValidate(new AssignNoteRequest(null, null, null, "سبب", "AAA="));
        neither.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Close_requires_reason_and_summary()
    {
        var validator = new CloseNoteRequestValidator();
        var result = validator.TestValidate(new CloseNoteRequest(" ", " ", "AAA="));
        result.ShouldHaveValidationErrorFor(x => x.Reason);
        result.ShouldHaveValidationErrorFor(x => x.ClosureSummary);
    }
}

public sealed class NoteStatusHistoryAppendOnlyTests
{
    [Fact]
    public void Modified_status_history_is_rejected()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new Domain.Identity.User
        {
            Id = userId,
            ExternalSubject = "h1",
            UserName = "h1",
            DisplayNameAr = "مستخدم"
        });
        var note = new OperationalNote
        {
            ReferenceNumber = "OBS-00000001",
            Title = "ت",
            Description = "و",
            ReportedByUserId = userId,
            ReportedAtUtc = DateTimeOffset.UtcNow,
            ScopeType = Domain.Common.ScopeType.Global
        };
        db.OperationalNotes.Add(note);
        var history = new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            ToStatus = NoteStatus.Draft,
            ChangedByUserId = userId
        };
        db.NoteStatusHistories.Add(history);
        db.SaveChanges();

        history.Reason = "hack";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Deleted_status_history_is_rejected()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new Domain.Identity.User
        {
            Id = userId,
            ExternalSubject = "h2",
            UserName = "h2",
            DisplayNameAr = "مستخدم"
        });
        var note = new OperationalNote
        {
            ReferenceNumber = "OBS-00000002",
            Title = "ت",
            Description = "و",
            ReportedByUserId = userId,
            ReportedAtUtc = DateTimeOffset.UtcNow,
            ScopeType = Domain.Common.ScopeType.Global
        };
        db.OperationalNotes.Add(note);
        var history = new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            ToStatus = NoteStatus.Draft,
            ChangedByUserId = userId
        };
        db.NoteStatusHistories.Add(history);
        db.SaveChanges();

        db.NoteStatusHistories.Remove(history);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    private static Infrastructure.Persistence.BaseeraDbContext CreateDb()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Infrastructure.Persistence.BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Infrastructure.Persistence.BaseeraDbContext(options);
    }
}

public sealed class NoteCriticalSoDTests
{
    [Fact]
    public async Task Critical_processor_cannot_verify_own_closure()
    {
        var processorId = Guid.NewGuid();
        var (db, scope, current, audit) = CreateHarness(processorId, Domain.Identity.PermissionCodes.NotesVerifyClosure);
        var note = SeedCriticalPending(db, processorId);
        var typeAccess = new NoteTypeAccessService(db, current);
        var workflow = new NoteWorkflowService(db, current, scope, typeAccess, audit, new StubQueryService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("إغلاق", "ملخص", Convert.ToBase64String(note.RowVersion))));
        Assert.Contains("فصل الواجبات", ex.Message);
    }

    [Fact]
    public async Task Different_verifier_can_close_critical_note()
    {
        var processorId = Guid.NewGuid();
        var verifierId = Guid.NewGuid();
        var (db, scope, current, audit) = CreateHarness(verifierId, Domain.Identity.PermissionCodes.NotesVerifyClosure);
        var note = SeedCriticalPending(db, processorId);

        var typeAccess = new NoteTypeAccessService(db, current);
        var workflow = new NoteWorkflowService(db, current, scope, typeAccess, audit, new StubQueryService());
        var result = await workflow.VerifyClosureAsync(
            note.Id,
            new CloseNoteRequest("إغلاق معتمد", "تم التحقق", Convert.ToBase64String(note.RowVersion)));

        Assert.Equal(NoteStatus.Closed, db.OperationalNotes.First(n => n.Id == note.Id).Status);
        Assert.NotNull(result);
    }

    private static OperationalNote SeedCriticalPending(Infrastructure.Persistence.BaseeraDbContext db, Guid processorId)
    {
        if (!db.Users.Any(u => u.Id == processorId))
        {
            db.Users.Add(new Domain.Identity.User
            {
                Id = processorId,
                ExternalSubject = "proc",
                UserName = "proc",
                DisplayNameAr = "معالج"
            });
        }

        var note = new OperationalNote
        {
            ReferenceNumber = "OBS-00000999",
            Title = "حرجة",
            Description = "وصف",
            NoteTypeId = NoteTestFixtures.DefaultNoteTypeId,
            Severity = NoteSeverity.Critical,
            Status = NoteStatus.PendingVerification,
            ScopeType = Domain.Common.ScopeType.Global,
            ReportedByUserId = processorId,
            ReportedAtUtc = DateTimeOffset.UtcNow,
            LastProcessedByUserId = processorId,
            RowVersion = [1, 2, 3, 4]
        };
        db.OperationalNotes.Add(note);
        db.SaveChanges();
        db.NoteStatusHistories.Add(new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            FromStatus = NoteStatus.Assigned,
            ToStatus = NoteStatus.InProgress,
            ChangedByUserId = processorId,
            ChangedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            Reason = "بدء"
        });
        db.NoteStatusHistories.Add(new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            FromStatus = NoteStatus.InProgress,
            ToStatus = NoteStatus.PendingVerification,
            ChangedByUserId = processorId,
            ChangedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Reason = "إرسال للتحقق"
        });
        db.SaveChanges();
        return note;
    }

    private static (
        Infrastructure.Persistence.BaseeraDbContext db,
        INoteScopeService scope,
        FakeCurrentUser current,
        RecordingAudit audit) CreateHarness(Guid userId, string permission)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Infrastructure.Persistence.BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new Infrastructure.Persistence.BaseeraDbContext(options);
        var user = new Domain.Identity.User
        {
            Id = userId,
            ExternalSubject = "actor",
            UserName = "actor",
            DisplayNameAr = "actor"
        };
        db.Users.Add(user);
        NoteTestFixtures.GrantPermissions(db, userId, "CriticalHarness", permission, Domain.Identity.PermissionCodes.NotesView);
        var current = new FakeCurrentUser(
            true,
            userId,
            "actor",
            "actor",
            [permission],
            [new Application.Abstractions.UserScopeSnapshot(Domain.Common.ScopeType.Global, null, null, null)]);
        var org = new Application.Security.OrganizationalScopeService(current, db);
        var scope = new NoteScopeService(org, current, db);
        return (db, scope, current, new RecordingAudit());
    }

    private sealed class RecordingAudit : Application.Abstractions.IAuditService
    {
        public Task WriteAsync(Application.Abstractions.AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubQueryService : INoteQueryService
    {
        public Task<Application.Common.PagedResult<NoteListItemDto>> ListAsync(NoteListQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<NoteDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<NoteDetailDto?>(new NoteDetailDto(
                id, "OBS-00000999", "حرجة", "وصف", NoteStatus.Closed, "مغلقة",
                NoteSeverity.Critical, "حرجة", NoteTestFixtures.DefaultNoteTypeId, "OPERATIONAL", "تشغيلية", null, null, true,
                NoteSourceType.Manual, "يدوي", null, Domain.Attachments.ClassificationLevel.Internal,
                Domain.Common.ScopeType.Global, null, null, null, null, Guid.Empty, null,
                DateTimeOffset.UtcNow, null, false, null, null, null, DateTimeOffset.UtcNow, null, null,
                null, null, null, DateTimeOffset.UtcNow, "AQIDBA==", false));

        public Task<IReadOnlyList<NoteStatusHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NoteStatusHistoryDto>>([]);

        public Task<IReadOnlyList<NoteAssignmentDto>> GetAssignmentsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NoteAssignmentDto>>([]);
    }
}
