using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;

namespace Baseera.UnitTests;

/// <summary>
/// Pure-logic coverage for NoteWorkspaceQueryService.ComputeAllowedActions/ResolveProgress/ResolveBlocker —
/// no database, no I/O. See NoteWorkspaceIntegrationTests for the full read-model wiring
/// (redaction, timeline bound, DTO shape) against a live SQL Server.
/// </summary>
public sealed class NoteWorkspaceQueryServiceTests
{
    [Theory]
    [InlineData(NoteStatus.PendingVerification, true)]
    [InlineData(NoteStatus.Draft, false)]
    [InlineData(NoteStatus.Open, false)]
    [InlineData(NoteStatus.Assigned, false)]
    [InlineData(NoteStatus.InProgress, false)]
    [InlineData(NoteStatus.Closed, false)]
    [InlineData(NoteStatus.Reopened, false)]
    [InlineData(NoteStatus.Cancelled, false)]
    public void VerifyClosure_is_allowed_only_from_PendingVerification_with_permission(NoteStatus status, bool expected)
    {
        var note = NewDetail(status);
        var user = FakeUser(PermissionCodes.NotesVerifyClosure);

        var allowed = NoteWorkspaceQueryService.ComputeAllowedActions(note, user);

        Assert.Equal(expected, allowed.Contains("VERIFY_CLOSURE"));
    }

    [Fact]
    public void VerifyClosure_is_never_allowed_without_the_permission_even_from_PendingVerification()
    {
        var note = NewDetail(NoteStatus.PendingVerification);
        var user = FakeUser(/* no permissions */);

        var allowed = NoteWorkspaceQueryService.ComputeAllowedActions(note, user);

        Assert.DoesNotContain("VERIFY_CLOSURE", allowed);
    }

    [Fact]
    public void Reassign_requires_an_existing_current_assignment()
    {
        var withoutAssignment = NewDetail(NoteStatus.Assigned, currentAssignment: null);
        var withAssignment = NewDetail(NoteStatus.Assigned, currentAssignment: NewAssignment());
        var user = FakeUser(PermissionCodes.NotesAssign);

        Assert.DoesNotContain("REASSIGN", NoteWorkspaceQueryService.ComputeAllowedActions(withoutAssignment, user));
        Assert.Contains("REASSIGN", NoteWorkspaceQueryService.ComputeAllowedActions(withAssignment, user));
    }

    [Theory]
    [InlineData(NoteStatus.Closed)]
    [InlineData(NoteStatus.Cancelled)]
    public void Terminal_locked_statuses_never_allow_cancel_or_reassign(NoteStatus status)
    {
        var note = NewDetail(status, currentAssignment: NewAssignment());
        var user = FakeUser(PermissionCodes.NotesAssign, PermissionCodes.NotesCancel);

        var allowed = NoteWorkspaceQueryService.ComputeAllowedActions(note, user);

        Assert.DoesNotContain("REASSIGN", allowed);
        Assert.DoesNotContain("CANCEL", allowed);
    }

    [Fact]
    public void Allowed_actions_are_permission_gated_independently_of_status()
    {
        var note = NewDetail(NoteStatus.Open);
        var noPermissions = FakeUser();
        var withAssign = FakeUser(PermissionCodes.NotesAssign);

        Assert.Empty(NoteWorkspaceQueryService.ComputeAllowedActions(note, noPermissions));
        Assert.Contains("ASSIGN", NoteWorkspaceQueryService.ComputeAllowedActions(note, withAssign));
    }

    [Theory]
    [InlineData(NoteStatus.Draft, 5)]
    [InlineData(NoteStatus.Open, 15)]
    [InlineData(NoteStatus.Assigned, 30)]
    [InlineData(NoteStatus.PendingVerification, 82)]
    [InlineData(NoteStatus.Closed, 100)]
    [InlineData(NoteStatus.Reopened, 40)]
    [InlineData(NoteStatus.Cancelled, 0)]
    public void ResolveProgress_maps_each_status_to_a_fixed_percentage(NoteStatus status, int expected)
    {
        Assert.Equal(expected, NoteWorkspaceQueryService.ResolveProgress(status, openActions: 0));
    }

    [Fact]
    public void ResolveProgress_in_progress_depends_on_open_corrective_actions()
    {
        Assert.Equal(65, NoteWorkspaceQueryService.ResolveProgress(NoteStatus.InProgress, openActions: 0));
        Assert.Equal(55, NoteWorkspaceQueryService.ResolveProgress(NoteStatus.InProgress, openActions: 2));
    }

    [Fact]
    public void ResolveBlocker_prioritizes_overdue_over_other_blockers()
    {
        var overdueNote = NewDetail(NoteStatus.InProgress, isOverdue: true);

        Assert.Equal("متجاوزة للموعد", NoteWorkspaceQueryService.ResolveBlocker(overdueNote, openActions: 3));
    }

    [Fact]
    public void ResolveBlocker_reports_pending_verification_when_not_overdue()
    {
        var note = NewDetail(NoteStatus.PendingVerification, isOverdue: false);

        Assert.Equal("بانتظار التحقق", NoteWorkspaceQueryService.ResolveBlocker(note, openActions: 0));
    }

    [Fact]
    public void ResolveBlocker_reports_open_corrective_actions_while_in_progress()
    {
        var note = NewDetail(NoteStatus.InProgress, isOverdue: false);

        Assert.Equal("بانتظار إكمال الإجراءات المفتوحة", NoteWorkspaceQueryService.ResolveBlocker(note, openActions: 1));
    }

    [Fact]
    public void ResolveBlocker_is_null_when_nothing_is_blocking()
    {
        var note = NewDetail(NoteStatus.Assigned, isOverdue: false);

        Assert.Null(NoteWorkspaceQueryService.ResolveBlocker(note, openActions: 0));
    }

    private static ICurrentUser FakeUser(params string[] permissions) =>
        new FakeCurrentUser(true, Guid.NewGuid(), Guid.NewGuid().ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);

    private static NoteAssignmentDto NewAssignment() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "المكلَّف", null, null, Guid.NewGuid(), "المُكلِّف",
        DateTimeOffset.UtcNow, null, "سبب التكليف", null, null, null, null, true);

    private static NoteDetailDto NewDetail(
        NoteStatus status,
        NoteAssignmentDto? currentAssignment = null,
        bool isOverdue = false) => new(
        Guid.NewGuid(),
        "OBS-00000001",
        "عنوان تجريبي",
        "وصف تجريبي",
        status,
        NoteDisplay.StatusAr(status),
        NoteSeverity.Medium,
        NoteDisplay.SeverityAr(NoteSeverity.Medium),
        Guid.NewGuid(),
        "OPERATIONAL",
        "تشغيلية",
        null,
        null,
        true,
        NoteSourceType.Manual,
        NoteDisplay.SourceAr(NoteSourceType.Manual),
        null,
        ClassificationLevel.Internal,
        ScopeType.Facility,
        null,
        Guid.NewGuid(),
        null,
        null,
        Guid.NewGuid(),
        "المُبلِّغ",
        DateTimeOffset.UtcNow.AddDays(-1),
        null,
        isOverdue,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        currentAssignment,
        DateTimeOffset.UtcNow.AddDays(-1),
        Convert.ToBase64String([1, 2, 3, 4]),
        false,
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, false);
}
