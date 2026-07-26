namespace Baseera.UnitTests.Workforce;

using Baseera.Application.Workforce;

public sealed class WorkforceReconciliationDetectorTests
{
    [Fact]
    public void Detect_emits_duplicate_external_id_and_stale_issues()
    {
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        var issues = WorkforceReconciliationDetector.Detect(new WorkforceReconciliationScanInput(
            ExternalIds: [(memberA, "EXT-1"), (memberB, "EXT-1")],
            ConflictingPrimaryMemberIds: [memberA],
            LeaveWhileRostered: [],
            RetirementWhileRostered: [],
            StaleMemberIds: [memberB],
            InvalidUserLinkMemberIds: [],
            AssignmentOutsideFacilityIds: [],
            UnpublishedRosterIds: [Guid.NewGuid()],
            SourceConflictMemberIds: [],
            UnknownAvailabilityMemberIds: [],
            NoAlternateCriticalPositionIds: []));

        Assert.Contains(issues, i => i.IssueType == WorkforceReconciliationIssueType.DuplicateExternalId);
        Assert.Contains(issues, i => i.IssueType == WorkforceReconciliationIssueType.ConflictingAssignments);
        Assert.Contains(issues, i => i.IssueType == WorkforceReconciliationIssueType.StaleSourceRecord);
        Assert.Contains(issues, i => i.IssueType == WorkforceReconciliationIssueType.UnpublishedRoster);
    }

    [Fact]
    public void CountingPolicy_replacement_does_not_double_count()
    {
        var original = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        var count = WorkforceCountingPolicy.CountRosterHeadcount(
        [
            (original, null),
            (replacement, original)
        ]);
        Assert.Equal(1, count);
        Assert.True(WorkforceCountingPolicy.AssignmentDoesNotImplyPresence);
        Assert.False(WorkforceCountingPolicy.IsPresentSignal(hasPublishedRosterPresence: false, hasActiveAssignment: true));
    }
}
