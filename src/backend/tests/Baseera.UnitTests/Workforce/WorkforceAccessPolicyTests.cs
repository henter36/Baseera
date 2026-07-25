namespace Baseera.UnitTests.Workforce;

using System.Globalization;
using Baseera.Application.Workforce;
using Baseera.Domain.Identity;
using Baseera.Domain.Workforce;

public sealed class WorkforceAccessPolicyTests
{
    [Fact]
    public void CanView_helpers_respect_grants()
    {
        var permissions = new[]
        {
            PermissionCodes.WorkforceViewSummary,
            PermissionCodes.WorkforceViewMembers
        };

        Assert.True(WorkforceAccessPolicy.CanViewSummary(permissions));
        Assert.True(WorkforceAccessPolicy.CanViewMembers(permissions));
        Assert.False(WorkforceAccessPolicy.CanViewSensitiveRestrictions(permissions));
    }

    [Fact]
    public void IsValidImportBatchState_matches_resource_contract()
    {
        Assert.True(WorkforceAccessPolicy.IsValidImportBatchState(WorkforceImportBatchStatuses.Confirmed, 3, DateTimeOffset.UtcNow));
        Assert.False(WorkforceAccessPolicy.IsValidImportBatchState(WorkforceImportBatchStatuses.Confirmed, 3, null));
        Assert.True(WorkforceAccessPolicy.IsValidImportBatchState(WorkforceImportBatchStatuses.Previewed, 0, null));
        Assert.False(WorkforceAccessPolicy.IsValidImportBatchState(WorkforceImportBatchStatuses.Previewed, 1, null));
        Assert.False(WorkforceAccessPolicy.IsValidImportBatchState("Pending", 0, null));
    }

    [Fact]
    public void IsValidImportBatchCounts_enforces_totals()
    {
        Assert.True(WorkforceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 0, WorkforceImportBatchStatuses.Previewed, null));
        Assert.True(WorkforceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 3, WorkforceImportBatchStatuses.Confirmed, DateTimeOffset.UtcNow));
        Assert.False(WorkforceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 0, 0, WorkforceImportBatchStatuses.Previewed, null));
    }

    [Fact]
    public void HasConflictingPrimaryAssignment_detects_overlap()
    {
        var existing = new[]
        {
            (IsPrimary: true, EffectiveFromUtc: DateTimeOffset.Parse("2026-01-01", CultureInfo.InvariantCulture), EffectiveToUtc: (DateTimeOffset?)null)
        };

        Assert.True(WorkforceAssignmentPolicy.HasConflictingPrimaryAssignment(
            existing,
            DateTimeOffset.Parse("2026-06-01", CultureInfo.InvariantCulture),
            null,
            candidateIsPrimary: true));
        Assert.False(WorkforceAssignmentPolicy.HasConflictingPrimaryAssignment(
            existing,
            DateTimeOffset.Parse("2026-06-01", CultureInfo.InvariantCulture),
            null,
            candidateIsPrimary: false));
    }

    [Fact]
    public void PeriodsOverlap_uses_half_open_semantics()
    {
        Assert.False(WorkforceAssignmentPolicy.PeriodsOverlap(
            DateTimeOffset.Parse("2026-01-01", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-03-01", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-03-01", CultureInfo.InvariantCulture),
            null));
        Assert.True(WorkforceAssignmentPolicy.PeriodsOverlap(
            DateTimeOffset.Parse("2026-01-01", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-03-01", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-02-01", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-02-15", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void NormalizeEmployeeNumber_trims_and_uppercases() =>
        Assert.Equal("WF-001", WorkforceAccessPolicy.NormalizeEmployeeNumber("  wf-001 "));
}
