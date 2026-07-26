namespace Baseera.Application.Workforce;

public static class WorkforceOperationalCatalog
{
    public static class Interventions
    {
        public const string ShiftBelowMinimum = "workforce.ShiftBelowMinimum";
        public const string CriticalRoleUncovered = "workforce.CriticalRoleUncovered";
        public const string UnitStaffingGap = "workforce.UnitStaffingGap";
        public const string NoShiftCommander = "workforce.NoShiftCommander";
        public const string NoQualifiedDriver = "workforce.NoQualifiedDriver";
        public const string QualificationExpired = "workforce.QualificationExpired";
        public const string QualificationExpiring = "workforce.QualificationExpiring";
        public const string HighAbsenceRate = "workforce.HighAbsenceRate";
        public const string ExcessiveOvertime = "workforce.ExcessiveOvertime";
        public const string ConsecutiveShiftRisk = "workforce.ConsecutiveShiftRisk";
        public const string NoCriticalPositionAlternate = "workforce.NoCriticalPositionAlternate";
        public const string ConflictingAssignments = "workforce.ConflictingAssignments";
        public const string WorkforceDataStale = "workforce.WorkforceDataStale";
        public const string WorkforceSourceConflict = "workforce.WorkforceSourceConflict";
        public const string UnknownAvailability = "workforce.UnknownAvailability";
        public const string UnpublishedRoster = "workforce.UnpublishedRoster";

        public static readonly IReadOnlySet<string> SupportedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            ShiftBelowMinimum,
            CriticalRoleUncovered,
            UnitStaffingGap,
            NoShiftCommander,
            NoQualifiedDriver,
            QualificationExpired,
            QualificationExpiring,
            HighAbsenceRate,
            ExcessiveOvertime,
            ConsecutiveShiftRisk,
            NoCriticalPositionAlternate,
            ConflictingAssignments,
            WorkforceDataStale,
            WorkforceSourceConflict,
            UnknownAvailability,
            UnpublishedRoster
        };
    }

    public static class DataQuality
    {
        public const string MissingEmployeeNumber = "missing_employee_number";
        public const string MissingOperationalFacility = "missing_operational_facility";
        public const string MissingOperationalUnit = "missing_operational_unit";
        public const string MissingOperationalRole = "missing_operational_role";
        public const string UnknownEmploymentStatus = "unknown_employment_status";
        public const string UnknownAvailability = "unknown_availability";
        public const string ExpiredAssignment = "expired_assignment";
        public const string ConflictingAssignments = "conflicting_assignment";
        public const string RosterWithoutCommander = "roster_without_commander";
        public const string ShiftWithoutMinimumRequirement = "shift_without_minimum_requirement";
        public const string MissingQualification = "missing_qualification";
        public const string ExpiredQualification = "expired_qualification";
        public const string StaleSource = "stale_source";
        public const string DuplicateExternalId = "duplicate_external_id";
        public const string DuplicateEmployeeNumber = "duplicate_employee_number";
        public const string InvalidUserLink = "invalid_user_link";
        public const string RetiredMemberInActiveRoster = "retired_member_in_active_roster";
        public const string MemberOnLeaveButScheduled = "member_on_leave_but_scheduled";
        public const string ConflictingRosterAssignments = "conflicting_roster_assignments";
        public const string CoverageGapWithoutResponsibleOwner = "coverage_gap_without_responsible_owner";
        public const string RequirementWithoutApprovalReference = "requirement_without_approval_reference";

        public static readonly IReadOnlySet<string> SupportedCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            MissingEmployeeNumber,
            MissingOperationalFacility,
            MissingOperationalUnit,
            MissingOperationalRole,
            UnknownEmploymentStatus,
            UnknownAvailability,
            ExpiredAssignment,
            ConflictingAssignments,
            RosterWithoutCommander,
            ShiftWithoutMinimumRequirement,
            MissingQualification,
            ExpiredQualification,
            StaleSource,
            DuplicateExternalId,
            DuplicateEmployeeNumber,
            InvalidUserLink,
            RetiredMemberInActiveRoster,
            MemberOnLeaveButScheduled,
            ConflictingRosterAssignments,
            CoverageGapWithoutResponsibleOwner,
            RequirementWithoutApprovalReference
        };
    }
}
