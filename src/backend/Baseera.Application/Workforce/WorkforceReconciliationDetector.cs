namespace Baseera.Application.Workforce;

using Baseera.Domain.Workforce;

public enum WorkforceReconciliationIssueType
{
    DuplicateExternalId = 0,
    ConflictingAssignments = 1,
    LeaveWhileRostered = 2,
    RetirementWhileRostered = 3,
    StaleSourceRecord = 4,
    InvalidUserLink = 5,
    AssignmentOutsideFacility = 6,
    UnpublishedRoster = 7,
    WorkforceSourceConflict = 8,
    UnknownAvailability = 9,
    NoCriticalPositionAlternate = 10,
    ConflictingRosterSlots = 11
}

public sealed record WorkforceReconciliationIssue(
    string IssueKey,
    WorkforceReconciliationIssueType IssueType,
    string TitleAr,
    string Severity,
    Guid? EntityId,
    string? EntityType);

public static class WorkforceReconciliationDetector
{
    private const string WorkforceMemberEntityType = "WorkforceMember";
    private const string SeverityCriticalValue = "critical";
    private const string SeverityMediumValue = "medium";

    public static IReadOnlyList<WorkforceReconciliationIssue> Detect(WorkforceReconciliationScanInput input)
    {
        var issues = new List<WorkforceReconciliationIssue>();

        foreach (var group in input.ExternalIds
                     .Where(x => !string.IsNullOrWhiteSpace(x.ExternalPersonnelId))
                     .GroupBy(x => x.ExternalPersonnelId, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"dup-ext:{group.Key}",
                WorkforceReconciliationIssueType.DuplicateExternalId,
                "معرّف خارجي مكرر",
                "high",
                group.First().MemberId,
                WorkforceMemberEntityType));
        }

        foreach (var conflict in input.ConflictingPrimaryAssignments)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"conflict-assign:{conflict.MemberId:N}",
                WorkforceReconciliationIssueType.ConflictingAssignments,
                "تكليفات أساسية متداخلة",
                SeverityCriticalValue,
                conflict.AssignmentId,
                "WorkforceAssignment"));
        }

        foreach (var rosterAssignmentId in input.LeaveWhileRostered.Select(row => row.RosterAssignmentId))
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"leave-roster:{rosterAssignmentId:N}",
                WorkforceReconciliationIssueType.LeaveWhileRostered,
                "إجازة أثناء التواجد في جدول مناوبة",
                "high",
                rosterAssignmentId,
                "DutyRosterAssignment"));
        }

        foreach (var rosterAssignmentId in input.RetirementWhileRostered.Select(row => row.RosterAssignmentId))
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"retired-roster:{rosterAssignmentId:N}",
                WorkforceReconciliationIssueType.RetirementWhileRostered,
                "تقاعد أو إنهاء خدمة مع بقاء في المناوبة",
                SeverityCriticalValue,
                rosterAssignmentId,
                "DutyRosterAssignment"));
        }

        foreach (var memberId in input.StaleMemberIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"stale:{memberId:N}",
                WorkforceReconciliationIssueType.StaleSourceRecord,
                "سجل مصدر قديم يحتاج تحققًا",
                SeverityMediumValue,
                memberId,
                WorkforceMemberEntityType));
        }

        foreach (var memberId in input.InvalidUserLinkMemberIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"bad-user:{memberId:N}",
                WorkforceReconciliationIssueType.InvalidUserLink,
                "ربط مستخدم غير صالح",
                SeverityMediumValue,
                memberId,
                WorkforceMemberEntityType));
        }

        foreach (var assignmentId in input.AssignmentOutsideFacilityIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"assign-out:{assignmentId:N}",
                WorkforceReconciliationIssueType.AssignmentOutsideFacility,
                "تكليف خارج منشأة التشغيل",
                "high",
                assignmentId,
                "WorkforceAssignment"));
        }

        foreach (var rosterId in input.UnpublishedRosterIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"unpub:{rosterId:N}",
                WorkforceReconciliationIssueType.UnpublishedRoster,
                "جدول مناوبة غير منشور لليوم التشغيلي",
                SeverityMediumValue,
                rosterId,
                "DutyRoster"));
        }

        foreach (var memberId in input.SourceConflictMemberIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"src-conflict:{memberId:N}",
                WorkforceReconciliationIssueType.WorkforceSourceConflict,
                "تعارض مصادر التواجد",
                "high",
                memberId,
                WorkforceMemberEntityType));
        }

        foreach (var memberId in input.UnknownAvailabilityMemberIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"unk-emp:{memberId:N}",
                WorkforceReconciliationIssueType.UnknownAvailability,
                "توفر غير معروف",
                SeverityMediumValue,
                memberId,
                WorkforceMemberEntityType));
        }

        foreach (var criticalId in input.NoAlternateCriticalPositionIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"no-alt:{criticalId:N}",
                WorkforceReconciliationIssueType.NoCriticalPositionAlternate,
                "موقع حرج بلا بديل",
                SeverityCriticalValue,
                criticalId,
                "CriticalPositionRequirement"));
        }

        foreach (var memberId in input.DuplicateRosterSlotMemberIds)
        {
            issues.Add(new WorkforceReconciliationIssue(
                $"roster-slots:{memberId:N}",
                WorkforceReconciliationIssueType.ConflictingRosterSlots,
                "تعارض خانات مناوبة",
                "high",
                memberId,
                WorkforceMemberEntityType));
        }

        return issues;
    }
}

public sealed record WorkforceReconciliationScanInput(
    IReadOnlyList<(Guid MemberId, string? ExternalPersonnelId)> ExternalIds,
    IReadOnlyList<(Guid MemberId, Guid AssignmentId)> ConflictingPrimaryAssignments,
    IReadOnlyList<(Guid MemberId, Guid RosterAssignmentId)> LeaveWhileRostered,
    IReadOnlyList<(Guid MemberId, Guid RosterAssignmentId)> RetirementWhileRostered,
    IReadOnlyList<Guid> StaleMemberIds,
    IReadOnlyList<Guid> InvalidUserLinkMemberIds,
    IReadOnlyList<Guid> AssignmentOutsideFacilityIds,
    IReadOnlyList<Guid> UnpublishedRosterIds,
    IReadOnlyList<Guid> SourceConflictMemberIds,
    IReadOnlyList<Guid> UnknownAvailabilityMemberIds,
    IReadOnlyList<Guid> NoAlternateCriticalPositionIds,
    IReadOnlyList<Guid> DuplicateRosterSlotMemberIds);
