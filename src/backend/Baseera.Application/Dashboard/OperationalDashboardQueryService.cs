namespace Baseera.Application.Dashboard;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Notes;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using static Baseera.Application.Dashboard.OperationalDashboardAggregationHelpers;

public sealed class OperationalDashboardQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    OperationalDashboardFilterBuilder filters,
    TimeProvider timeProvider) : IOperationalDashboardQueryService
{
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
        var needsNoteAggregate = canOperational || canRisk || canRouting;

        NoteSummaryAggregate? noteAggregate = null;
        if (needsNoteAggregate)
        {
            noteAggregate = await AggregateNotesSummaryAsync(notes, now, cancellationToken);
        }

        RoutingFailureAggregate? routingFailures = null;
        if (canRisk || canRouting)
        {
            routingFailures = await AggregateRoutingFailuresAsync(db, notes, fromUtc, toUtc, cancellationToken);
        }

        OperationalDashboardWorkloadSummaryDto? workload = null;
        if (canOperational && noteAggregate is not null)
        {
            workload = new OperationalDashboardWorkloadSummaryDto(
                noteAggregate.OpenTotal,
                noteAggregate.Assigned,
                noteAggregate.InProgress,
                noteAggregate.PendingVerification,
                noteAggregate.Reopened,
                noteAggregate.Unassigned,
                noteAggregate.RequiresRouting);
        }

        IQueryable<CorrectiveAction>? scopedActions = null;
        if (canRisk || canCa)
        {
            scopedActions = await filters.BuildScopedCorrectiveActionsAsync(query, cancellationToken);
        }

        OperationalDashboardRiskSummaryDto? risk = null;
        if (canRisk && noteAggregate is not null && routingFailures is not null && scopedActions is not null)
        {
            var activeEscalations = await CountActiveEscalationsAsync(
                db,
                notes,
                scopedActions,
                fromUtc,
                toUtc,
                cancellationToken);

            risk = new OperationalDashboardRiskSummaryDto(
                noteAggregate.Overdue,
                noteAggregate.DueSoon,
                noteAggregate.CriticalOrHigh,
                noteAggregate.OverdueUnassigned,
                activeEscalations,
                routingFailures.NoRule,
                routingFailures.NoEligibleUser,
                routingFailures.InvalidTarget);
        }

        OperationalDashboardCorrectiveActionsSummaryDto? correctiveActions = null;
        if (canCa && scopedActions is not null)
        {
            var actionAggregate = await AggregateCorrectiveActionsSummaryAsync(scopedActions, now, cancellationToken);
            var notesWithStalled = await CountNotesWithStalledActionsAsync(notes, scopedActions, now, cancellationToken);

            correctiveActions = new OperationalDashboardCorrectiveActionsSummaryDto(
                actionAggregate.Active,
                actionAggregate.Overdue,
                actionAggregate.PendingVerification,
                actionAggregate.Reopened,
                notesWithStalled);
        }

        OperationalDashboardRoutingSummaryDto? routing = null;
        if (canRouting && noteAggregate is not null && routingFailures is not null)
        {
            routing = new OperationalDashboardRoutingSummaryDto(
                noteAggregate.RequiresRouting,
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
        var scopedNoteIds = notes.Select(note => note.Id);

        var periodDays = query.PeriodDays ?? (int)Math.Ceiling((toUtc - fromUtc).TotalDays);
        var granularity = periodDays >= 60 ? "weekly" : "daily";
        var bucketStarts = BuildBucketStarts(fromUtc, toUtc, granularity);

        var notesCreatedDaily = ToDailyCountMap(await notes
            .Where(note => note.CreatedAtUtc >= fromUtc && note.CreatedAtUtc <= toUtc)
            .GroupBy(note => note.CreatedAtUtc.AddHours(3).Date)
            .Select(group => new DailyCountRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken));

        var notesCompletedDaily = ToDailyCountMap(await notes
            .Where(note =>
                note.Status == NoteStatus.Closed &&
                note.ClosedAtUtc.HasValue &&
                note.ClosedAtUtc >= fromUtc &&
                note.ClosedAtUtc <= toUtc)
            .GroupBy(note => note.ClosedAtUtc!.Value.AddHours(3).Date)
            .Select(group => new DailyCountRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken));

        var notesBecameOverdueDaily = ToDailyCountMap(await notes
            .Where(note =>
                note.DueAtUtc.HasValue &&
                note.DueAtUtc >= fromUtc &&
                note.DueAtUtc <= toUtc &&
                note.DueAtUtc < now &&
                note.Status != NoteStatus.Closed &&
                note.Status != NoteStatus.Cancelled)
            .GroupBy(note => note.DueAtUtc!.Value.AddHours(3).Date)
            .Select(group => new DailyCountRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken));

        var caCompletedDaily = ToDailyCountMap(await actions
            .Where(action =>
                action.Status == CorrectiveActionStatus.Completed &&
                action.CompletedAtUtc.HasValue &&
                action.CompletedAtUtc >= fromUtc &&
                action.CompletedAtUtc <= toUtc)
            .GroupBy(action => action.CompletedAtUtc!.Value.AddHours(3).Date)
            .Select(group => new DailyCountRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken));

        var routingDaily = ToDailyRoutingMap(await db.NoteRoutingDecisions.AsNoTracking()
            .Where(decision =>
                scopedNoteIds.Contains(decision.OperationalNoteId) &&
                decision.DecidedAtUtc >= fromUtc &&
                decision.DecidedAtUtc <= toUtc)
            .GroupBy(decision => decision.DecidedAtUtc.AddHours(3).Date)
            .Select(group => new DailyRoutingRow(
                group.Key,
                group.Count(decision =>
                    decision.ResultStatus == NoteRoutingResultStatus.AssignedToDepartment ||
                    decision.ResultStatus == NoteRoutingResultStatus.AssignedToUser),
                group.Count(decision =>
                    decision.ResultStatus == NoteRoutingResultStatus.NoMatchingRule ||
                    decision.ResultStatus == NoteRoutingResultStatus.NoEligibleUser ||
                    decision.ResultStatus == NoteRoutingResultStatus.InvalidTarget ||
                    decision.ResultStatus == NoteRoutingResultStatus.Failed)))
            .ToListAsync(cancellationToken));

        var points = new List<OperationalDashboardTrendPointDto>(bucketStarts.Count);
        foreach (var bucketStart in bucketStarts)
        {
            var bucketEnd = granularity == "weekly"
                ? bucketStart.AddDays(7).AddTicks(-1)
                : OperationalDashboardPeriodResolver.EndOfSaudiDayUtc(bucketStart);
            if (bucketEnd > toUtc)
            {
                bucketEnd = toUtc;
            }

            var routing = SumDailyRoutingForBucket(routingDaily, bucketStart, bucketEnd, granularity);
            points.Add(new OperationalDashboardTrendPointDto(
                bucketStart,
                bucketEnd,
                FormatBucketLabel(bucketStart, granularity),
                SumDailyCountsForBucket(notesCreatedDaily, bucketStart, bucketEnd, granularity),
                SumDailyCountsForBucket(notesCompletedDaily, bucketStart, bucketEnd, granularity),
                SumDailyCountsForBucket(notesBecameOverdueDaily, bucketStart, bucketEnd, granularity),
                SumDailyCountsForBucket(caCompletedDaily, bucketStart, bucketEnd, granularity),
                routing.Success,
                routing.Failure));
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
            OperationalDashboardBreakdownDimension.Region => await BuildRegionBreakdownAsync(
                notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.Facility => await BuildFacilityBreakdownAsync(
                notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.NoteType => await BuildNoteTypeBreakdownAsync(
                notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.Severity => await BuildSeverityBreakdownAsync(
                notes, actions, now, fromUtc, toUtc, cancellationToken),
            OperationalDashboardBreakdownDimension.Status => await BuildStatusBreakdownAsync(
                notes, actions, now, fromUtc, toUtc, cancellationToken),
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

    private async Task<IReadOnlyList<OperationalDashboardBreakdownRowDto>> BuildRegionBreakdownAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await LoadNoteBreakdownAggregatesAsync(
            notes,
            note => note.RegionId ?? Guid.Empty,
            now,
            fromUtc,
            toUtc,
            cancellationToken);

        var regionIds = grouped.Where(row => row.Key != Guid.Empty).Select(row => row.Key).ToList();
        var regions = regionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Regions.AsNoTracking()
                .Where(region => regionIds.Contains(region.Id))
                .ToDictionaryAsync(region => region.Id, region => region.NameAr, cancellationToken);

        var caOverdueByRegion = await LoadCorrectiveActionsOverdueByNoteKeyAsync(
            actions,
            notes,
            now,
            note => note.RegionId ?? Guid.Empty,
            cancellationToken);

        return grouped
            .OrderByDescending(row => row.Aggregate.Overdue)
            .Select(row =>
            {
                Guid? regionId = row.Key == Guid.Empty ? null : row.Key;
                var label = regionId.HasValue && regions.TryGetValue(regionId.Value, out var name)
                    ? name
                    : "بدون منطقة";
                caOverdueByRegion.TryGetValue(row.Key, out var caOverdue);
                return ToBreakdownRow(
                    regionId?.ToString() ?? "none",
                    label,
                    regionId,
                    row.Aggregate,
                    caOverdue);
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
        var grouped = await LoadNoteBreakdownAggregatesAsync(
            notes.Where(note => note.FacilityId.HasValue),
            note => note.FacilityId!.Value,
            now,
            fromUtc,
            toUtc,
            cancellationToken);

        var facilityIds = grouped.Select(row => row.Key).ToList();
        var facilities = facilityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Facilities.AsNoTracking()
                .Where(facility => facilityIds.Contains(facility.Id))
                .ToDictionaryAsync(facility => facility.Id, facility => facility.NameAr, cancellationToken);

        var caOverdueByFacility = await LoadCorrectiveActionsOverdueByNoteKeyAsync(
            actions,
            notes.Where(note => note.FacilityId.HasValue),
            now,
            note => note.FacilityId!.Value,
            cancellationToken);

        return grouped
            .OrderByDescending(row => row.Aggregate.Overdue)
            .Select(row =>
            {
                facilities.TryGetValue(row.Key, out var label);
                caOverdueByFacility.TryGetValue(row.Key, out var caOverdue);
                return ToBreakdownRow(
                    row.Key.ToString(),
                    label ?? "بدون موقع",
                    row.Key,
                    row.Aggregate,
                    caOverdue);
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
        var grouped = await LoadNoteBreakdownAggregatesAsync(
            notes,
            note => note.NoteTypeId,
            now,
            fromUtc,
            toUtc,
            cancellationToken);

        var typeIds = grouped.Select(row => row.Key).ToList();
        var types = typeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.NoteTypes.AsNoTracking()
                .Where(type => typeIds.Contains(type.Id))
                .ToDictionaryAsync(type => type.Id, type => type.NameAr, cancellationToken);

        var caOverdueByType = await LoadCorrectiveActionsOverdueByNoteKeyAsync(
            actions,
            notes,
            now,
            note => note.NoteTypeId,
            cancellationToken);

        return grouped
            .OrderByDescending(row => row.Aggregate.Overdue)
            .Select(row =>
            {
                types.TryGetValue(row.Key, out var label);
                caOverdueByType.TryGetValue(row.Key, out var caOverdue);
                return ToBreakdownRow(
                    row.Key.ToString(),
                    label ?? row.Key.ToString(),
                    row.Key,
                    row.Aggregate,
                    caOverdue);
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
        var grouped = await LoadNoteBreakdownAggregatesAsync(
            notes,
            note => note.Severity,
            now,
            fromUtc,
            toUtc,
            cancellationToken);

        var caOverdueBySeverity = await LoadCorrectiveActionsOverdueByNoteKeyAsync(
            actions,
            notes,
            now,
            note => note.Severity,
            cancellationToken);

        return grouped
            .OrderByDescending(row => row.Aggregate.Overdue)
            .Select(row =>
            {
                caOverdueBySeverity.TryGetValue(row.Key, out var caOverdue);
                return ToBreakdownRow(
                    ((int)row.Key).ToString(),
                    NoteDisplay.SeverityAr(row.Key),
                    null,
                    row.Aggregate,
                    caOverdue);
            })
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
        var grouped = await LoadNoteBreakdownAggregatesAsync(
            notes,
            note => note.Status,
            now,
            fromUtc,
            toUtc,
            cancellationToken);

        var caOverdueByStatus = await LoadCorrectiveActionsOverdueByNoteKeyAsync(
            actions,
            notes,
            now,
            note => note.Status,
            cancellationToken);

        return grouped
            .OrderBy(row => row.Key)
            .Select(row =>
            {
                caOverdueByStatus.TryGetValue(row.Key, out var caOverdue);
                return ToBreakdownRow(
                    ((int)row.Key).ToString(),
                    NoteDisplay.StatusAr(row.Key),
                    null,
                    row.Aggregate,
                    caOverdue);
            })
            .ToList();
    }

    private async Task<Dictionary<TKey, int>> LoadCorrectiveActionsOverdueByNoteKeyAsync<TKey>(
        IQueryable<CorrectiveAction> actions,
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        System.Linq.Expressions.Expression<Func<OperationalNote, TKey>> keySelector,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        return await FilterOverdueActions(actions, now)
            .Join(notes, action => action.OperationalNoteId, note => note.Id, (_, note) => note)
            .GroupBy(keySelector)
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken);
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
        var facilities = facilityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Facilities.AsNoTracking()
                .Where(facility => facilityIds.Contains(facility.Id))
                .ToDictionaryAsync(facility => facility.Id, facility => facility.NameAr, cancellationToken);
        var regions = regionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Regions.AsNoTracking()
                .Where(region => regionIds.Contains(region.Id))
                .ToDictionaryAsync(region => region.Id, region => region.NameAr, cancellationToken);

        return grouped
            .Where(row => row.FacilityId.HasValue)
            .Select(row => new OperationalDashboardOverdueLocationQueueItemDto(
                row.FacilityId!.Value,
                facilities.TryGetValue(row.FacilityId.Value, out var facilityName)
                    ? facilityName
                    : row.FacilityId.Value.ToString(),
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
        var rows = await FilterOverdueActions(actions, now)
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
        var scopedNoteIds = notes.Select(note => note.Id);
        var decisions = await db.NoteRoutingDecisions.AsNoTracking()
            .Where(decision => scopedNoteIds.Contains(decision.OperationalNoteId) &&
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
