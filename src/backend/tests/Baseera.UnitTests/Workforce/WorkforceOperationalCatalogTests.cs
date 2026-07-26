namespace Baseera.UnitTests.Workforce;

using Baseera.Application.Workforce;

public sealed class WorkforceOperationalCatalogTests
{
    [Fact]
    public void Intervention_catalog_contains_all_supported_workforce_rules()
    {
        var expected = new[]
        {
            "workforce.ShiftBelowMinimum",
            "workforce.CriticalRoleUncovered",
            "workforce.UnitStaffingGap",
            "workforce.NoShiftCommander",
            "workforce.NoQualifiedDriver",
            "workforce.QualificationExpired",
            "workforce.QualificationExpiring",
            "workforce.HighAbsenceRate",
            "workforce.ExcessiveOvertime",
            "workforce.ConsecutiveShiftRisk",
            "workforce.NoCriticalPositionAlternate",
            "workforce.ConflictingAssignments",
            "workforce.WorkforceDataStale",
            "workforce.WorkforceSourceConflict",
            "workforce.UnknownAvailability",
            "workforce.UnpublishedRoster"
        };

        Assert.Equal(expected.Length, WorkforceOperationalCatalog.Interventions.SupportedKeys.Count);
        foreach (var key in expected)
        {
            Assert.Contains(key, WorkforceOperationalCatalog.Interventions.SupportedKeys);
        }
    }

    [Fact]
    public void Data_quality_catalog_contains_all_supported_workforce_codes()
    {
        var expected = new[]
        {
            "missing_employee_number",
            "missing_operational_facility",
            "missing_operational_unit",
            "missing_operational_role",
            "unknown_employment_status",
            "unknown_availability",
            "expired_assignment",
            "conflicting_assignment",
            "roster_without_commander",
            "shift_without_minimum_requirement",
            "missing_qualification",
            "expired_qualification",
            "stale_source",
            "duplicate_external_id",
            "duplicate_employee_number",
            "invalid_user_link",
            "retired_member_in_active_roster",
            "member_on_leave_but_scheduled",
            "conflicting_roster_assignments",
            "coverage_gap_without_responsible_owner",
            "requirement_without_approval_reference"
        };

        Assert.Equal(expected.Length, WorkforceOperationalCatalog.DataQuality.SupportedCodes.Count);
        foreach (var code in expected)
        {
            Assert.Contains(code, WorkforceOperationalCatalog.DataQuality.SupportedCodes);
        }
    }
}
