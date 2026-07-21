namespace Baseera.Application.Dashboard;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Notes;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public sealed class OperationalDashboardQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    OperationalDashboardFilterBuilder filters,
    TimeProvider timeProvider) : IOperationalDashboardQueryService
{
    private static bool IsOpenStatus(NoteStatus status) =>
        status is not NoteStatus.Closed and not NoteStatus.Cancelled;

    public async Task<OperationalDashboardSummaryDto> GetSummaryAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureAnyDashboardPermission();
        var now = timeProvider.GetUtcNow();
        var (fromUtc, toUtc) = OperationalDashboardPeriodResolver.Resolve(query, now);
        var notes = await filters.BuildScopedNotesAsync(query, cancellationToken);
        var canOperational = HasPermission(PermissionCodes.DashboardViewOperational);
        var canRisk = HasPermission(PermissionCodes.DashboardViewRisk);
        var canCa = HasPermission(PermissionCodes.DashboardViewCorrectiveActions);
        var canRouting = HasPermission(PermissionCodes.DashboardViewRouting);

        OperationalDashboardWorkloadSummaryDto? workload = null;
        if (canOperational)
        {
            workload = new OperationalDashboardWorkloadSummaryDto(
                await CountOpenAsync(notes, cancellationToken),
                await notes.CountAsync(note => note.Status == NoteStatus.Assigned, cancellationToken),
                await notes.CountAsync(note => note.Status == NoteStatus.InProgress, cancellationToken),
                await notes.CountAsync(note => note.Status == NoteStatus.PendingVerification, cancellationToken),
                await notes.CountAsync(note => note.Status == NoteStatus.Reopened, cancellationToken),
                await OperationalDashboardFilterBuilder.ApplyUnassignedOpenFilter(notes).CountAsync(cancellationToken),
                await OperationalDashboardFilterBuilder.ApplyRequiresRoutingFilter(notes).CountAsync(cancellationToken));
        }

        OperationalDashboardRiskSummaryDto? risk = null;
        if (canRisk)
        {
            var overdue = await CountOverdueNotesAsync(notes, now, cancellationToken);
            var dueSoon = await CountDueSoonNotesAsync(notes, now, cancellationToken);
            var critical = await notes.CountAsync(
                note => (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled,
                cancellationToken);
            var overdueUnassigned = await OperationalDashboardFilterBuilder.ApplyUnassignedOpenFilter(notes)
                .CountAsync(note => note.DueAtUtc.HasValue &&
                                    note.DueAtUtc < now &&
                                    note.Status != NoteStatus.Closed &&
                                    note.Status != NoteStatus.Cancelled, cancellationToken);
            var activeEscalations = await CountActiveEscalationsAsync(notes, query, fromUtc, toUtc, cancellationToken);
            var routingFailures = await GetRoutingFailureCountsAsync(notes, fromUtc, toUtc, cancellationToken);
            risk = new OperationalDashboardRiskSummaryDto(
                overdue,
                dueSoon,
                critical,
                overdueUnassigned,
                activeEscalations,
                routingFailures.NoRule,
                routingFailures.NoEligibleUser,
                routingFailures.InvalidTarget);
        }

        OperationalDashboardCorrectiveActionsSummaryDto? correctiveActions = null;
        if (canCa)
        {
            var actions = await filters.BuildScopedCorrectiveActionsAsync(query, cancellationToken);
            var activeActions = actions.Where(action =>
                action.Status != CorrectiveActionStatus.Completed &&
                action.Status != CorrectiveActionStatus.Cancelled);
            var openNoteIds = notes.Where(note =>
                note.Status != NoteStatus.Closed &&
                note.Status != NoteStatus.Cancelled).Select(note => note.Id);
            var notesWithStalled = await activeActions
                .Where(action =>
                    action.DueAtUtc.HasValue &&
                    action.DueAtUtc < now &&
                    action.Status != CorrectiveActionStatus.Completed &&
                    action.Status != CorrectiveActionStatus.Cancelled &&
                    openNoteIds.Contains(action.OperationalNoteId))
                .Select(action => action.OperationalNoteId)
                .Distinct()
                .CountAsync(cancellationToken);

            correctiveActions = new OperationalDashboardCorrectiveActionsSummaryDto(
                await activeActions.CountAsync(cancellationToken),
                await activeActions.CountAsync(
                    action => action.DueAtUtc.HasValue &&
                              action.DueAtUtc < now &&
                              action.Status != CorrectiveActionStatus.Completed &&
                              action.Status != CorrectiveActionStatus.Cancelled,
                    cancellationToken),
                await activeActions.CountAsync(action => action.Status == CorrectiveActionStatus.PendingVerification, cancellationToken),
                await activeActions.CountAsync(action => action.Status == CorrectiveActionStatus.Reopened, cancellationToken),
                notesWithStalled);
        }

        OperationalDashboardRoutingSummaryDto? routing = null;
        if (canRouting)
        {
            var requiresRouting = await OperationalDashboardFilterBuilder.ApplyRequiresRoutingFilter(notes).CountAsync(cancellationToken);
            var routingFailures = await GetRoutingFailureCountsAsync(notes, fromUtc, toUtc, cancellationToken);
            routing = new OperationalDashboardRoutingSummaryDto(
                requiresRouting,
                routingFailures.NoRule,
                routingFailures.NoEligibleUser,
                routingFailures.InvalidTarget);
        }

        return new OperationalDashboardSummaryDto(
            workload,
            risk,
            correctiveActions,
            routing,
            fromUtc,
            toUtc,
            OperationalDashboardKpiDefinitions.DefaultDueSoonDays);
    }

    public async Task<OperationalDashboardTrendsDto> GetTrendsAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.DashboardViewOperational);
        var now = timeProvider.GetUtcNow();
        var (fromUtc, toUtc) = OperationalDashboardPeriodResolver.Resolve(query, now);
        var notes = await filters.BuildScopedNotesAsync(query, cancellationToken);
        var actions = await filters.BuildScopedCorrectiveActionsAsync(query, cancellationToken);
        var noteIdList = await notes.Select(note => note.Id).ToListAsync(cancellationToken);

        var periodDays = query.PeriodDays ?? (int)Math.Ceiling((toUtc - fromUtc).TotalDays);
        var granularity = periodDays >= 60 ? "weekly" : "daily";
        var bucketStarts = BuildBucketStarts(fromUtc, toUtc, granularity);
        var points = new List<OperationalDashboardTrendPointDto>();

        foreach (var bucketStart in bucketStarts)
        {
            var bucketEnd = granularity == "weekly"
                ? bucketStart.AddDays(7).AddTicks(-1)
                : OperationalDashboardPeriodResolver.EndOfSaudiDayUtc(bucketStart);
            if (bucketEnd > toUtc)
            {
                bucketEnd = toUtc;
            }

            var notesCreated = await notes.CountAsync(
                note => note.CreatedAtUtc >= bucketStart && note.CreatedAtUtc <= bucketEnd,
                cancellationToken);
            var notesCompleted = await notes.CountAsync(
                note => note.Status == NoteStatus.Closed &&
                        note.ClosedAtUtc.HasValue &&
                        note.ClosedAtUtc.Value >= bucketStart &&
                        note.ClosedAtUtc.Value <= bucketEnd,
                cancellationToken);
            var notesBecameOverdue = await notes.CountAsync(
                note => note.DueAtUtc.HasValue &&
                        note.DueAtUtc.Value >= bucketStart &&
                        note.DueAtUtc.Value <= bucketEnd &&
                        note.DueAtUtc.Value < now &&
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled,
                cancellationToken);
            var caCompleted = await actions.CountAsync(
                action => action.Status == CorrectiveActionStatus.Completed &&
                          action.CompletedAtUtc.HasValue &&
                          action.CompletedAtUtc.Value >= bucketStart &&
                          action.CompletedAtUtc.Value <= bucketEnd,
                cancellationToken);

            var routingQuery = db.NoteRoutingDecisions.AsNoTracking()
                .Where(decision => noteIdList.Contains(decision.OperationalNoteId) &&
                                   decision.DecidedAtUtc >= bucketStart &&
                                   decision.DecidedAtUtc <= bucketEnd);
            var routingSuccess = await routingQuery.CountAsync(
                decision => decision.ResultStatus == NoteRoutingResultStatus.AssignedToDepartment ||
                            decision.ResultStatus == NoteRoutingResultStatus.AssignedToUser,
                cancellationToken);
            var routingFailure = await routingQuery.CountAsync(
                decision => decision.ResultStatus == NoteRoutingResultStatus.NoMatchingRule ||
                            decision.ResultStatus == NoteRoutingResultStatus.NoEligibleUser ||
                            decision.ResultStatus == NoteRoutingResultStatus.InvalidTarget ||
                            decision.ResultStatus == NoteRoutingResultStatus.Failed,
                cancellationToken);

            points.Add(new OperationalDashboardTrendPointDto(
                bucketStart,
                bucketEnd,
                FormatBucketLabel(bucketStart, granularity),
                notesCreated,
                notesCompleted,
                notesBecameOverdue,
                caCompleted,
                routingSuccess,
                routingFailure));
        }

        return new OperationalDashboardTrendsDto(points, fromUtc, toUtc, granularity);
    }

    public async Task<OperationalDashboardBreakdownsDto> GetBreakdownsAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.DashboardViewOperational);
        if (!query.BreakdownBy.HasValue)
        {
            throw new ArgumentException("breakdownBy مطلوب.");
        }

        var now = timeProvider.GetUtcNow();
        var (fromUtc, toUtc) = OperationalDashboardPeriodResolver.Resolve(query, now);
        var notes = await filters.BuildScopedNotesAsync(query, cancellationToken);
        var actions = await filters.BuildScopedCorrectiveActionsAsync(query, cancellationToken);

        var rows = query.BreakdownBy.Value switch
        {
            OperationalDashboardBreakdownDimension.Region => await BuildRegionBreakdownAsync(notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.Facility => await BuildFacilityBreakdownAsync(notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.NoteType => await BuildNoteTypeBreakdownAsync(notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.Severity => await BuildSeverityBreakdownAsync(notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.Status => await BuildStatusBreakdownAsync(notes, actions, now, fromUtc, toUtc, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(query.BreakdownBy))
        };

        return new OperationalDashboardBreakdownsDto(query.BreakdownBy.Value, rows);
    }

    public async Task<OperationalDashboardPriorityQueuesDto> GetPriorityQueuesAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureAnyDashboardPermission();
        var now = timeProvider.GetUtcNow();
        _ = OperationalDashboardPeriodResolver.Resolve(query, now);
        var notes = await filters.BuildScopedNotesAsync(query, cancellationToken);
        var limit = OperationalDashboardKpiDefinitions.TopLimit;

        IReadOnlyList<OperationalDashboardOverdueNoteQueueItemDto>? mostOverdueNotes = null;
        IReadOnlyList<OperationalDashboardOverdueNoteQueueItemDto>? criticalUnassignedNotes = null;
        IReadOnlyList<OperationalDashboardOverdueLocationQueueItemDto>? topOverdueLocations = null;
        IReadOnlyList<OperationalDashboardOverdueCorrectiveActionQueueItemDto>? mostOverdueCorrectiveActions = null;
        IReadOnlyList<OperationalDashboardRoutingFailureQueueItemDto>? recentRoutingFailures = null;

        if (HasPermission(PermissionCodes.DashboardViewOperational) || HasPermission(PermissionCodes.DashboardViewRisk))
        {
            if (!query.Queue.HasValue || query.Queue == OperationalDashboardPriorityQueue.MostOverdueNotes)
            {
                mostOverdueNotes = await BuildMostOverdueNotesAsync(notes, now, limit, cancellationToken);
            }

            if (!query.Queue.HasValue || query.Queue == OperationalDashboardPriorityQueue.CriticalUnassignedNotes)
            {
                criticalUnassignedNotes = await BuildCriticalUnassignedNotesAsync(notes, now, limit, cancellationToken);
            }

            if (!query.Queue.HasValue || query.Queue == OperationalDashboardPriorityQueue.TopOverdueLocations)
            {
                topOverdueLocations = await BuildTopOverdueLocationsAsync(notes, now, limit, cancellationToken);
            }
        }

        if (HasPermission(PermissionCodes.DashboardViewCorrectiveActions))
        {
            if (!query.Queue.HasValue || query.Queue == OperationalDashboardPriorityQueue.MostOverdueCorrectiveActions)
            {
                var actions = await filters.BuildScopedCorrectiveActionsAsync(query, cancellationToken);
                mostOverdueCorrectiveActions = await BuildMostOverdueCorrectiveActionsAsync(actions, now, limit, cancellationToken);
            }
        }

        if (HasPermission(PermissionCodes.DashboardViewRouting) || HasPermission(PermissionCodes.DashboardViewRisk))
        {
            if (!query.Queue.HasValue || query.Queue == OperationalDashboardPriorityQueue.RecentRoutingFailures)
            {
                recentRoutingFailures = await BuildRecentRoutingFailuresAsync(notes, limit, cancellationToken);
            }
        }

        return new OperationalDashboardPriorityQueuesDto(
            mostOverdueNotes,
            criticalUnassignedNotes,
            topOverdueLocations,
            mostOverdueCorrectiveActions,
            recentRoutingFailures,
            limit);
    }

    private async Task<int> CountOpenAsync(IQueryable<OperationalNote> notes, CancellationToken cancellationToken) =>
        await notes.CountAsync(
            note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled,
            cancellationToken);

    private static async Task<int> CountOverdueNotesAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await notes.CountAsync(
            note => note.DueAtUtc.HasValue &&
                    note.DueAtUtc < now &&
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled,
            cancellationToken);

    private static async Task<int> CountDueSoonNotesAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await notes.CountAsync(
            note => note.DueAtUtc.HasValue &&
                    note.DueAtUtc >= now &&
                    note.DueAtUtc <= now.AddDays(OperationalDashboardKpiDefinitions.DefaultDueSoonDays) &&
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled,
            cancellationToken);

    private async Task<int> CountActiveEscalationsAsync(
        IQueryable<OperationalNote> notes,
        OperationalDashboardQuery query,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var noteIdList = await notes.Select(note => note.Id).ToListAsync(cancellationToken);
        if (noteIdList.Count == 0)
        {
            return 0;
        }

        var actions = await filters.BuildScopedCorrectiveActionsAsync(query, cancellationToken);
        var actionIds = await actions.Select(action => action.Id).ToListAsync(cancellationToken);

        return await db.EscalationOccurrences.AsNoTracking()
            .CountAsync(
                occurrence => occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated &&
                              occurrence.DetectedAtUtc >= fromUtc &&
                              occurrence.DetectedAtUtc <= toUtc &&
                              (
                                  (occurrence.TargetType == EscalationTargetType.OperationalNote &&
                                   noteIdList.Contains(occurrence.TargetId)) ||
                                  (occurrence.TargetType == EscalationTargetType.CorrectiveAction &&
                                   actionIds.Contains(occurrence.TargetId))),
                cancellationToken);
    }

    private async Task<(int NoRule, int NoEligibleUser, int InvalidTarget)> GetRoutingFailureCountsAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var noteIdList = await notes.Select(note => note.Id).ToListAsync(cancellationToken);
        if (noteIdList.Count == 0)
        {
            return (0, 0, 0);
        }

        var decisions = db.NoteRoutingDecisions.AsNoTracking()
            .Where(decision => noteIdList.Contains(decision.OperationalNoteId) &&
                               decision.DecidedAtUtc >= fromUtc &&
                               decision.DecidedAtUtc <= toUtc);

        var noRule = await decisions.CountAsync(decision => decision.ResultStatus == NoteRoutingResultStatus.NoMatchingRule, cancellationToken);
        var noEligible = await decisions.CountAsync(decision => decision.ResultStatus == NoteRoutingResultStatus.NoEligibleUser, cancellationToken);
        var invalidTarget = await decisions.CountAsync(
            decision => decision.ResultStatus == NoteRoutingResultStatus.InvalidTarget ||
                        decision.ResultStatus == NoteRoutingResultStatus.Failed,
            cancellationToken);
        return (noRule, noEligible, invalidTarget);
    }

    private async Task<IReadOnlyList<OperationalDashboardBreakdownRowDto>> BuildRegionBreakdownAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await notes
            .GroupBy(note => new { note.RegionId })
            .Select(group => new
            {
                group.Key.RegionId,
                OpenBurden = group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Overdue = group.Count(note => note.DueAtUtc.HasValue && note.DueAtUtc < now &&
                                              note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Critical = group.Count(note => (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                                               note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Unassigned = group.Count(note => note.Status != NoteStatus.Closed &&
                                                 note.Status != NoteStatus.Cancelled &&
                                                 !note.Assignments.Any(assignment => assignment.IsCurrent)),
                ClosedTotal = group.Count(note => note.Status == NoteStatus.Closed &&
                                                  note.ClosedAtUtc.HasValue &&
                                                  note.ClosedAtUtc >= fromUtc &&
                                                  note.ClosedAtUtc <= toUtc),
                ClosedWithinDue = group.Count(note => note.Status == NoteStatus.Closed &&
                                                      note.ClosedAtUtc.HasValue &&
                                                      note.ClosedAtUtc >= fromUtc &&
                                                      note.ClosedAtUtc <= toUtc &&
                                                      note.DueAtUtc.HasValue &&
                                                      note.ClosedAtUtc <= note.DueAtUtc)
            })
            .ToListAsync(cancellationToken);

        var regionIds = grouped.Where(row => row.RegionId.HasValue).Select(row => row.RegionId!.Value).ToList();
        var regions = await db.Regions.AsNoTracking()
            .Where(region => regionIds.Contains(region.Id))
            .ToDictionaryAsync(region => region.Id, region => region.NameAr, cancellationToken);

        var caOverdueByRegion = await actions
            .Where(action => action.DueAtUtc.HasValue &&
                             action.DueAtUtc < now &&
                             action.Status != CorrectiveActionStatus.Completed &&
                             action.Status != CorrectiveActionStatus.Cancelled)
            .Join(notes, action => action.OperationalNoteId, note => note.Id, (action, note) => note.RegionId)
            .GroupBy(regionId => regionId)
            .Select(group => new { RegionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.RegionId, row => row.Count, cancellationToken);

        return grouped
            .OrderByDescending(row => row.Overdue)
            .Select(row =>
            {
                var label = row.RegionId.HasValue && regions.TryGetValue(row.RegionId.Value, out var name)
                    ? name
                    : "بدون منطقة";
                caOverdueByRegion.TryGetValue(row.RegionId, out var caOverdue);
                return new OperationalDashboardBreakdownRowDto(
                    row.RegionId?.ToString() ?? "none",
                    label,
                    row.RegionId,
                    row.OpenBurden,
                    row.Overdue,
                    row.Critical,
                    row.Unassigned,
                    caOverdue,
                    row.ClosedTotal == 0 ? null : Math.Round((decimal)row.ClosedWithinDue / row.ClosedTotal, 4));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardBreakdownRowDto>> BuildFacilityBreakdownAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await notes
            .Where(note => note.FacilityId.HasValue)
            .GroupBy(note => new { note.FacilityId })
            .Select(group => new
            {
                group.Key.FacilityId,
                OpenBurden = group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Overdue = group.Count(note => note.DueAtUtc.HasValue && note.DueAtUtc < now &&
                                              note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Critical = group.Count(note => (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                                               note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Unassigned = group.Count(note => note.Status != NoteStatus.Closed &&
                                                 note.Status != NoteStatus.Cancelled &&
                                                 !note.Assignments.Any(assignment => assignment.IsCurrent)),
                ClosedTotal = group.Count(note => note.Status == NoteStatus.Closed &&
                                                  note.ClosedAtUtc.HasValue &&
                                                  note.ClosedAtUtc >= fromUtc &&
                                                  note.ClosedAtUtc <= toUtc),
                ClosedWithinDue = group.Count(note => note.Status == NoteStatus.Closed &&
                                                      note.ClosedAtUtc.HasValue &&
                                                      note.ClosedAtUtc >= fromUtc &&
                                                      note.ClosedAtUtc <= toUtc &&
                                                      note.DueAtUtc.HasValue &&
                                                      note.ClosedAtUtc <= note.DueAtUtc)
            })
            .ToListAsync(cancellationToken);

        var facilityIds = grouped.Where(row => row.FacilityId.HasValue).Select(row => row.FacilityId!.Value).ToList();
        var facilities = await db.Facilities.AsNoTracking()
            .Where(facility => facilityIds.Contains(facility.Id))
            .ToDictionaryAsync(facility => facility.Id, facility => facility.NameAr, cancellationToken);

        var caOverdueByFacility = await actions
            .Where(action => action.DueAtUtc.HasValue &&
                             action.DueAtUtc < now &&
                             action.Status != CorrectiveActionStatus.Completed &&
                             action.Status != CorrectiveActionStatus.Cancelled)
            .Join(notes.Where(note => note.FacilityId.HasValue), action => action.OperationalNoteId, note => note.Id, (action, note) => note.FacilityId)
            .GroupBy(facilityId => facilityId)
            .Select(group => new { FacilityId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.FacilityId, row => row.Count, cancellationToken);

        return grouped
            .OrderByDescending(row => row.Overdue)
            .Select(row =>
            {
                var label = row.FacilityId.HasValue && facilities.TryGetValue(row.FacilityId.Value, out var name)
                    ? name
                    : "بدون موقع";
                caOverdueByFacility.TryGetValue(row.FacilityId, out var caOverdue);
                return new OperationalDashboardBreakdownRowDto(
                    row.FacilityId?.ToString() ?? "none",
                    label,
                    row.FacilityId,
                    row.OpenBurden,
                    row.Overdue,
                    row.Critical,
                    row.Unassigned,
                    caOverdue,
                    row.ClosedTotal == 0 ? null : Math.Round((decimal)row.ClosedWithinDue / row.ClosedTotal, 4));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardBreakdownRowDto>> BuildNoteTypeBreakdownAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await notes
            .GroupBy(note => new { note.NoteTypeId })
            .Select(group => new
            {
                group.Key.NoteTypeId,
                OpenBurden = group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Overdue = group.Count(note => note.DueAtUtc.HasValue && note.DueAtUtc < now &&
                                              note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Critical = group.Count(note => (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                                               note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Unassigned = group.Count(note => note.Status != NoteStatus.Closed &&
                                                 note.Status != NoteStatus.Cancelled &&
                                                 !note.Assignments.Any(assignment => assignment.IsCurrent)),
                ClosedTotal = group.Count(note => note.Status == NoteStatus.Closed &&
                                                  note.ClosedAtUtc.HasValue &&
                                                  note.ClosedAtUtc >= fromUtc &&
                                                  note.ClosedAtUtc <= toUtc),
                ClosedWithinDue = group.Count(note => note.Status == NoteStatus.Closed &&
                                                      note.ClosedAtUtc.HasValue &&
                                                      note.ClosedAtUtc >= fromUtc &&
                                                      note.ClosedAtUtc <= toUtc &&
                                                      note.DueAtUtc.HasValue &&
                                                      note.ClosedAtUtc <= note.DueAtUtc)
            })
            .ToListAsync(cancellationToken);

        var typeIds = grouped.Select(row => row.NoteTypeId).ToList();
        var types = await db.NoteTypes.AsNoTracking()
            .Where(type => typeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, type => type.NameAr, cancellationToken);

        var caOverdueByType = await actions
            .Where(action => action.DueAtUtc.HasValue &&
                             action.DueAtUtc < now &&
                             action.Status != CorrectiveActionStatus.Completed &&
                             action.Status != CorrectiveActionStatus.Cancelled)
            .Join(notes, action => action.OperationalNoteId, note => note.Id, (action, note) => note.NoteTypeId)
            .GroupBy(typeId => typeId)
            .Select(group => new { NoteTypeId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.NoteTypeId, row => row.Count, cancellationToken);

        return grouped
            .OrderByDescending(row => row.Overdue)
            .Select(row =>
            {
                types.TryGetValue(row.NoteTypeId, out var label);
                caOverdueByType.TryGetValue(row.NoteTypeId, out var caOverdue);
                return new OperationalDashboardBreakdownRowDto(
                    row.NoteTypeId.ToString(),
                    label ?? row.NoteTypeId.ToString(),
                    row.NoteTypeId,
                    row.OpenBurden,
                    row.Overdue,
                    row.Critical,
                    row.Unassigned,
                    caOverdue,
                    row.ClosedTotal == 0 ? null : Math.Round((decimal)row.ClosedWithinDue / row.ClosedTotal, 4));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardBreakdownRowDto>> BuildSeverityBreakdownAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await notes
            .GroupBy(note => note.Severity)
            .Select(group => new
            {
                Severity = group.Key,
                OpenBurden = group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Overdue = group.Count(note => note.DueAtUtc.HasValue && note.DueAtUtc < now &&
                                              note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Critical = group.Count(note => (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                                               note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Unassigned = group.Count(note => note.Status != NoteStatus.Closed &&
                                                 note.Status != NoteStatus.Cancelled &&
                                                 !note.Assignments.Any(assignment => assignment.IsCurrent)),
                ClosedTotal = group.Count(note => note.Status == NoteStatus.Closed &&
                                                  note.ClosedAtUtc.HasValue &&
                                                  note.ClosedAtUtc >= fromUtc &&
                                                  note.ClosedAtUtc <= toUtc),
                ClosedWithinDue = group.Count(note => note.Status == NoteStatus.Closed &&
                                                      note.ClosedAtUtc.HasValue &&
                                                      note.ClosedAtUtc >= fromUtc &&
                                                      note.ClosedAtUtc <= toUtc &&
                                                      note.DueAtUtc.HasValue &&
                                                      note.ClosedAtUtc <= note.DueAtUtc)
            })
            .ToListAsync(cancellationToken);

        return grouped
            .OrderByDescending(row => row.Overdue)
            .Select(row => new OperationalDashboardBreakdownRowDto(
                ((int)row.Severity).ToString(),
                NoteDisplay.SeverityAr(row.Severity),
                null,
                row.OpenBurden,
                row.Overdue,
                row.Critical,
                row.Unassigned,
                0,
                row.ClosedTotal == 0 ? null : Math.Round((decimal)row.ClosedWithinDue / row.ClosedTotal, 4)))
            .ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardBreakdownRowDto>> BuildStatusBreakdownAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await notes
            .GroupBy(note => note.Status)
            .Select(group => new
            {
                Status = group.Key,
                OpenBurden = group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Overdue = group.Count(note => note.DueAtUtc.HasValue && note.DueAtUtc < now &&
                                              note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Critical = group.Count(note => (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                                               note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                Unassigned = group.Count(note => note.Status != NoteStatus.Closed &&
                                                 note.Status != NoteStatus.Cancelled &&
                                                 !note.Assignments.Any(assignment => assignment.IsCurrent)),
                ClosedTotal = group.Count(note => note.Status == NoteStatus.Closed &&
                                                  note.ClosedAtUtc.HasValue &&
                                                  note.ClosedAtUtc >= fromUtc &&
                                                  note.ClosedAtUtc <= toUtc),
                ClosedWithinDue = group.Count(note => note.Status == NoteStatus.Closed &&
                                                      note.ClosedAtUtc.HasValue &&
                                                      note.ClosedAtUtc >= fromUtc &&
                                                      note.ClosedAtUtc <= toUtc &&
                                                      note.DueAtUtc.HasValue &&
                                                      note.ClosedAtUtc <= note.DueAtUtc)
            })
            .ToListAsync(cancellationToken);

        return grouped
            .OrderBy(row => row.Status)
            .Select(row => new OperationalDashboardBreakdownRowDto(
                ((int)row.Status).ToString(),
                NoteDisplay.StatusAr(row.Status),
                null,
                row.OpenBurden,
                row.Overdue,
                row.Critical,
                row.Unassigned,
                0,
                row.ClosedTotal == 0 ? null : Math.Round((decimal)row.ClosedWithinDue / row.ClosedTotal, 4)))
            .ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardOverdueNoteQueueItemDto>> BuildMostOverdueNotesAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await notes
            .Where(note => note.DueAtUtc.HasValue &&
                           note.DueAtUtc < now &&
                           note.Status != NoteStatus.Closed &&
                           note.Status != NoteStatus.Cancelled)
            .OrderBy(note => note.DueAtUtc)
            .Take(limit)
            .Select(note => new
            {
                note.Id,
                note.ReferenceNumber,
                note.Title,
                note.Severity,
                note.Status,
                note.DueAtUtc,
                note.RegionId,
                note.FacilityId
            })
            .ToListAsync(cancellationToken);

        var facilityIds = rows.Where(row => row.FacilityId.HasValue).Select(row => row.FacilityId!.Value).Distinct().ToList();
        var facilities = facilityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Facilities.AsNoTracking()
                .Where(facility => facilityIds.Contains(facility.Id))
                .ToDictionaryAsync(facility => facility.Id, facility => facility.NameAr, cancellationToken);

        return rows.Select(row => new OperationalDashboardOverdueNoteQueueItemDto(
            row.Id,
            row.ReferenceNumber,
            row.Title,
            row.Severity,
            NoteDisplay.SeverityAr(row.Severity),
            row.Status,
            NoteDisplay.StatusAr(row.Status),
            row.DueAtUtc,
            row.DueAtUtc.HasValue ? (int?)Math.Max(0, (now - row.DueAtUtc.Value).TotalDays) : null,
            row.RegionId,
            row.FacilityId,
            row.FacilityId.HasValue && facilities.TryGetValue(row.FacilityId.Value, out var name) ? name : null)).ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardOverdueNoteQueueItemDto>> BuildCriticalUnassignedNotesAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var filtered = OperationalDashboardFilterBuilder.ApplyUnassignedOpenFilter(notes)
            .Where(note => note.Severity == NoteSeverity.Critical || note.Severity == NoteSeverity.High);

        return await BuildMostOverdueNotesAsync(filtered, now, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<OperationalDashboardOverdueLocationQueueItemDto>> BuildTopOverdueLocationsAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var grouped = await notes
            .Where(note => note.FacilityId.HasValue &&
                           note.DueAtUtc.HasValue &&
                           note.DueAtUtc < now &&
                           note.Status != NoteStatus.Closed &&
                           note.Status != NoteStatus.Cancelled)
            .GroupBy(note => new { note.FacilityId, note.RegionId })
            .Select(group => new
            {
                group.Key.FacilityId,
                group.Key.RegionId,
                OverdueCount = group.Count()
            })
            .OrderByDescending(row => row.OverdueCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var facilityIds = grouped.Where(row => row.FacilityId.HasValue).Select(row => row.FacilityId!.Value).ToList();
        var regionIds = grouped.Where(row => row.RegionId.HasValue).Select(row => row.RegionId!.Value).ToList();
        var facilities = await db.Facilities.AsNoTracking()
            .Where(facility => facilityIds.Contains(facility.Id))
            .ToDictionaryAsync(facility => facility.Id, facility => facility.NameAr, cancellationToken);
        var regions = await db.Regions.AsNoTracking()
            .Where(region => regionIds.Contains(region.Id))
            .ToDictionaryAsync(region => region.Id, region => region.NameAr, cancellationToken);

        return grouped
            .Where(row => row.FacilityId.HasValue)
            .Select(row => new OperationalDashboardOverdueLocationQueueItemDto(
                row.FacilityId!.Value,
                facilities[row.FacilityId.Value],
                row.RegionId,
                row.RegionId.HasValue && regions.TryGetValue(row.RegionId.Value, out var regionName) ? regionName : null,
                row.OverdueCount))
            .ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardOverdueCorrectiveActionQueueItemDto>> BuildMostOverdueCorrectiveActionsAsync(
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await actions
            .Where(action => action.DueAtUtc.HasValue &&
                             action.DueAtUtc < now &&
                             action.Status != CorrectiveActionStatus.Completed &&
                             action.Status != CorrectiveActionStatus.Cancelled)
            .OrderBy(action => action.DueAtUtc)
            .Take(limit)
            .Select(action => new
            {
                action.Id,
                action.ReferenceNumber,
                action.Title,
                action.Status,
                action.DueAtUtc,
                action.OperationalNoteId,
                NoteReferenceNumber = action.OperationalNote.ReferenceNumber
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new OperationalDashboardOverdueCorrectiveActionQueueItemDto(
            row.Id,
            row.ReferenceNumber,
            row.Title,
            row.Status,
            CorrectiveActionDisplay.StatusAr(row.Status),
            row.DueAtUtc,
            row.DueAtUtc.HasValue ? (int?)Math.Max(0, (now - row.DueAtUtc.Value).TotalDays) : null,
            row.OperationalNoteId,
            row.NoteReferenceNumber)).ToList();
    }

    private async Task<IReadOnlyList<OperationalDashboardRoutingFailureQueueItemDto>> BuildRecentRoutingFailuresAsync(
        IQueryable<OperationalNote> notes,
        int limit,
        CancellationToken cancellationToken)
    {
        var noteIdList = await notes.Select(note => note.Id).ToListAsync(cancellationToken);
        if (noteIdList.Count == 0)
        {
            return [];
        }

        var decisions = await db.NoteRoutingDecisions.AsNoTracking()
            .Where(decision => noteIdList.Contains(decision.OperationalNoteId) &&
                               (decision.ResultStatus == NoteRoutingResultStatus.NoMatchingRule ||
                                decision.ResultStatus == NoteRoutingResultStatus.NoEligibleUser ||
                                decision.ResultStatus == NoteRoutingResultStatus.InvalidTarget ||
                                decision.ResultStatus == NoteRoutingResultStatus.Failed))
            .OrderByDescending(decision => decision.DecidedAtUtc)
            .Take(limit)
            .Select(decision => new
            {
                decision.OperationalNoteId,
                decision.ResultStatus,
                decision.FailureCode,
                decision.FailureMessageSafe,
                decision.DecidedAtUtc,
                NoteReferenceNumber = decision.OperationalNote.ReferenceNumber,
                NoteTitle = decision.OperationalNote.Title
            })
            .ToListAsync(cancellationToken);

        return decisions.Select(decision => new OperationalDashboardRoutingFailureQueueItemDto(
            decision.OperationalNoteId,
            decision.NoteReferenceNumber,
            decision.NoteTitle,
            decision.FailureCode ?? decision.ResultStatus.ToString(),
            decision.FailureMessageSafe ?? decision.ResultStatus.ToString(),
            decision.DecidedAtUtc)).ToList();
    }

    private static List<DateTimeOffset> BuildBucketStarts(DateTimeOffset fromUtc, DateTimeOffset toUtc, string granularity)
    {
        var starts = new List<DateTimeOffset>();
        var cursor = OperationalDashboardPeriodResolver.StartOfSaudiDayUtc(fromUtc);
        var stepDays = granularity == "weekly" ? 7 : 1;
        while (cursor <= toUtc)
        {
            starts.Add(cursor);
            cursor = cursor.AddDays(stepDays);
        }

        return starts;
    }

    private static string FormatBucketLabel(DateTimeOffset bucketStartUtc, string granularity)
    {
        var local = TimeZoneInfo.ConvertTime(bucketStartUtc, TimeZones.SaudiArabia);
        return granularity == "weekly"
            ? local.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : local.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void EnsurePermission(string permissionCode)
    {
        if (!HasPermission(permissionCode))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private void EnsureAnyDashboardPermission()
    {
        if (!HasPermission(PermissionCodes.DashboardViewOperational) &&
            !HasPermission(PermissionCodes.DashboardViewRisk) &&
            !HasPermission(PermissionCodes.DashboardViewRouting) &&
            !HasPermission(PermissionCodes.DashboardViewCorrectiveActions))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private bool HasPermission(string permissionCode) => currentUser.HasPermission(permissionCode);
}
