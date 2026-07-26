namespace Baseera.Application.Workforce;

using Baseera.Domain.Workforce;

/// <summary>
/// Resolves the strongest operational presence signal for coverage calculations.
/// Assignment alone never means present — it only indicates planned placement.
/// </summary>
public interface IWorkforceSourceResolver
{
    WorkforcePresenceSource Resolve(
        bool hasPublishedRosterAssignment,
        RosterAssignmentStatus? rosterStatus,
        bool hasAttendanceOrImportAvailability,
        bool hasAvailabilityEvent,
        bool hasActiveAssignment);
}

public enum WorkforcePresenceSource
{
    Unknown = 0,
    Assignment = 1,
    AvailabilityEvent = 2,
    AttendanceOrImport = 3,
    PublishedRoster = 4
}

public sealed class WorkforceSourceResolver : IWorkforceSourceResolver
{
    public WorkforcePresenceSource Resolve(
        bool hasPublishedRosterAssignment,
        RosterAssignmentStatus? rosterStatus,
        bool hasAttendanceOrImportAvailability,
        bool hasAvailabilityEvent,
        bool hasActiveAssignment)
    {
        // Precedence: Published roster > attendance/import availability > availability events > assignment > Unknown.
        // Assignment ≠ present: it is a planning signal only.
        if (hasPublishedRosterAssignment && rosterStatus is not null)
        {
            return WorkforcePresenceSource.PublishedRoster;
        }

        if (hasAttendanceOrImportAvailability)
        {
            return WorkforcePresenceSource.AttendanceOrImport;
        }

        if (hasAvailabilityEvent)
        {
            return WorkforcePresenceSource.AvailabilityEvent;
        }

        if (hasActiveAssignment)
        {
            return WorkforcePresenceSource.Assignment;
        }

        return WorkforcePresenceSource.Unknown;
    }
}
