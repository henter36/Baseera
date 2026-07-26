namespace Baseera.UnitTests.Workforce;

using Baseera.Application.Workforce;

public sealed class WorkforceOperationalCatalogTests
{
    [Fact]
    public void Intervention_catalog_contains_all_supported_workforce_rules()
    {
        var expected = new[]
        {
            WorkforceOperationalCatalog.Interventions.ShiftBelowMinimum,
            WorkforceOperationalCatalog.Interventions.CriticalRoleUncovered,
            WorkforceOperationalCatalog.Interventions.UnitStaffingGap,
            WorkforceOperationalCatalog.Interventions.NoShiftCommander,
            WorkforceOperationalCatalog.Interventions.NoQualifiedDriver,
            WorkforceOperationalCatalog.Interventions.QualificationExpired,
            WorkforceOperationalCatalog.Interventions.QualificationExpiring,
            WorkforceOperationalCatalog.Interventions.HighAbsenceRate,
            WorkforceOperationalCatalog.Interventions.ExcessiveOvertime,
            WorkforceOperationalCatalog.Interventions.ConsecutiveShiftRisk,
            WorkforceOperationalCatalog.Interventions.NoCriticalPositionAlternate,
            WorkforceOperationalCatalog.Interventions.ConflictingAssignments,
            WorkforceOperationalCatalog.Interventions.WorkforceDataStale,
            WorkforceOperationalCatalog.Interventions.WorkforceSourceConflict,
            WorkforceOperationalCatalog.Interventions.UnknownAvailability,
            WorkforceOperationalCatalog.Interventions.UnpublishedRoster
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
            WorkforceOperationalCatalog.DataQuality.MissingEmployeeNumber,
            WorkforceOperationalCatalog.DataQuality.MissingOperationalFacility,
            WorkforceOperationalCatalog.DataQuality.MissingOperationalUnit,
            WorkforceOperationalCatalog.DataQuality.MissingOperationalRole,
            WorkforceOperationalCatalog.DataQuality.UnknownEmploymentStatus,
            WorkforceOperationalCatalog.DataQuality.UnknownAvailability,
            WorkforceOperationalCatalog.DataQuality.ExpiredAssignment,
            WorkforceOperationalCatalog.DataQuality.ConflictingAssignments,
            WorkforceOperationalCatalog.DataQuality.RosterWithoutCommander,
            WorkforceOperationalCatalog.DataQuality.ShiftWithoutMinimumRequirement,
            WorkforceOperationalCatalog.DataQuality.MissingQualification,
            WorkforceOperationalCatalog.DataQuality.ExpiredQualification,
            WorkforceOperationalCatalog.DataQuality.StaleSource,
            WorkforceOperationalCatalog.DataQuality.DuplicateExternalId,
            WorkforceOperationalCatalog.DataQuality.DuplicateEmployeeNumber,
            WorkforceOperationalCatalog.DataQuality.InvalidUserLink,
            WorkforceOperationalCatalog.DataQuality.RetiredMemberInActiveRoster,
            WorkforceOperationalCatalog.DataQuality.MemberOnLeaveButScheduled,
            WorkforceOperationalCatalog.DataQuality.ConflictingRosterAssignments,
            WorkforceOperationalCatalog.DataQuality.CoverageGapWithoutResponsibleOwner,
            WorkforceOperationalCatalog.DataQuality.RequirementWithoutApprovalReference
        };

        Assert.Equal(expected.Length, WorkforceOperationalCatalog.DataQuality.SupportedCodes.Count);
        foreach (var code in expected)
        {
            Assert.Contains(code, WorkforceOperationalCatalog.DataQuality.SupportedCodes);
        }
    }
}
