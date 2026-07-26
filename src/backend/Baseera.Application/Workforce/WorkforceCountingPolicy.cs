namespace Baseera.Application.Workforce;

using Baseera.Domain.Workforce;

/// <summary>
/// Deterministic counting invariants so readiness tallies never double-count people or treat assignment as presence.
/// </summary>
public static class WorkforceCountingPolicy
{
    /// <summary>A member scheduled on overlapping shifts counts once toward coverage, not once per overlapping shift.</summary>
    public static bool MemberCannotCountInOverlappingShifts => true;

    /// <summary>A replacement assignment substitutes for the original; it does not add an extra headcount.</summary>
    public static bool ReplacementReplacesNotAdds => true;

    /// <summary>Having an organizational assignment does not imply the member is present on duty.</summary>
    public static bool AssignmentDoesNotImplyPresence => true;

    /// <summary>Unknown availability must not be treated as available for operational coverage.</summary>
    public static bool UnknownNotAvailable => true;

    /// <summary>Leave that affects operational availability blocks counting the member as available.</summary>
    public static bool LeaveBlocksAvailability => true;

    /// <summary>Expired qualifications block qualified-coverage counts for the matching role.</summary>
    public static bool ExpiredQualificationBlocksQualifiedCoverage => true;

    /// <summary>Restricted-duty codes block incompatible roles (e.g. CannotDrive vs VehicleDriver).</summary>
    public static bool RestrictedDutyBlocksRole => true;

    public static int CountDistinctMembers(IEnumerable<Guid> memberIds) =>
        memberIds.Distinct().Count();

    public static int CountRosterHeadcount(IEnumerable<(Guid AssignmentId, Guid? ReplacementForAssignmentId)> assignments)
    {
        // Replacements substitute; originals that were replaced are excluded from headcount.
        var replacedIds = assignments
            .Select(a => a.ReplacementForAssignmentId)
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .ToHashSet();
        return assignments.Count(a => !replacedIds.Contains(a.AssignmentId));
    }

    /// <summary>
    /// Counts unique members across shift buckets without double-counting someone present in overlapping windows.
    /// </summary>
    public static int CountMembersWithoutOverlapDoubleCount(IEnumerable<Guid> memberIdsAcrossShifts) =>
        CountDistinctMembers(memberIdsAcrossShifts);

    public static bool IsPresentSignal(bool hasPublishedRosterPresence, bool hasActiveAssignment) =>
        hasPublishedRosterPresence; // assignment alone never implies presence

    public static bool CountsAsAvailable(
        EmploymentStatus employmentStatus,
        bool isOperational,
        AvailabilityType? availabilityType,
        bool affectsOperationalAvailability,
        bool availabilityKnown)
    {
        if (!availabilityKnown && UnknownNotAvailable)
        {
            return false;
        }

        if (!WorkforceReadinessPolicy.IsEmploymentOperationallyEligible(employmentStatus, isOperational))
        {
            return false;
        }

        if (availabilityType is null)
        {
            return availabilityKnown;
        }

        if (LeaveBlocksAvailability
            && WorkforceReadinessPolicy.IsAvailabilityBlocking(availabilityType.Value, affectsOperationalAvailability))
        {
            return false;
        }

        return true;
    }

    public static bool CountsAsQualified(
        QualificationStatus status,
        Guid? qualificationRoleId,
        Guid requiredRoleId,
        DateTimeOffset asOfUtc,
        DateTimeOffset? expiresAtUtc) =>
        WorkforceReadinessPolicy.IsQualificationValidForRole(status, qualificationRoleId, requiredRoleId, asOfUtc, expiresAtUtc);

    public static bool IsRoleBlockedByRestriction(OperationalRestrictionCode restriction, string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return false;
        }

        var code = roleCode.Trim();
        return restriction switch
        {
            OperationalRestrictionCode.CannotDrive =>
                code.Contains("Driver", StringComparison.OrdinalIgnoreCase)
                || code.Contains("Vehicle", StringComparison.OrdinalIgnoreCase)
                || code.Contains("سائق", StringComparison.Ordinal),
            OperationalRestrictionCode.CannotCarryWeapon =>
                code.Contains("Armed", StringComparison.OrdinalIgnoreCase)
                || code.Contains("Weapon", StringComparison.OrdinalIgnoreCase),
            OperationalRestrictionCode.CannotWorkNightShift =>
                code.Contains("Night", StringComparison.OrdinalIgnoreCase),
            OperationalRestrictionCode.CannotPerformEscort =>
                code.Contains("Escort", StringComparison.OrdinalIgnoreCase)
                || code.Contains("مرافقة", StringComparison.Ordinal),
            OperationalRestrictionCode.AdministrativeDutyOnly =>
                !code.Contains("Admin", StringComparison.OrdinalIgnoreCase)
                && !code.Contains("إدار", StringComparison.Ordinal),
            _ => false
        };
    }
}
