namespace Baseera.Application.Dashboard;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

internal static class OperationalDashboardAggregationHelpers
{
    internal sealed record NoteSummaryAggregate(
        int OpenTotal,
        int Assigned,
        int InProgress,
        int PendingVerification,
        int Reopened,
        int Unassigned,
        int RequiresRouting,
        int Overdue,
        int DueSoon,
        int CriticalOrHigh,
        int OverdueUnassigned)
    {
        public static NoteSummaryAggregate Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    internal sealed record CorrectiveActionSummaryAggregate(
        int Active,
        int Overdue,
        int PendingVerification,
        int Reopened)
    {
        public static CorrectiveActionSummaryAggregate Empty { get; } = new(0, 0, 0, 0);
    }

    internal sealed record RoutingFailureAggregate(
        int NoRule,
        int NoEligibleUser,
        int InvalidTarget)
    {
        public static RoutingFailureAggregate Empty { get; } = new(0, 0, 0);
    }

    internal sealed record NoteBreakdownAggregate(
        int OpenBurden,
        int Overdue,
        int Critical,
        int Unassigned,
        int ClosedTotal,
        int ClosedWithinDue);

    internal sealed record DailyCountRow(DateTime SaudiDay, int Count);

    internal sealed record DailyRoutingRow(DateTime SaudiDay, int Success, int Failure);

    internal static DateTime SaudiCalendarDay(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, TimeZones.SaudiArabia).Date;

    internal static decimal? ClosureRateWithinDue(int closedTotal, int closedWithinDue) =>
        closedTotal == 0 ? null : Math.Round((decimal)closedWithinDue / closedTotal, 4);

    internal static IQueryable<CorrectiveAction> FilterOverdueActions(
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now) =>
        actions.Where(action =>
            action.DueAtUtc.HasValue &&
            action.DueAtUtc < now &&
            action.Status != CorrectiveActionStatus.Completed &&
            action.Status != CorrectiveActionStatus.Cancelled);

    internal static async Task<NoteSummaryAggregate> AggregateNotesSummaryAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dueSoonUpper = now.AddDays(OperationalDashboardKpiDefinitions.DefaultDueSoonDays);
        var row = await notes
            .GroupBy(_ => 1)
            .Select(group => new NoteSummaryAggregate(
                group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                group.Count(note => note.Status == NoteStatus.Assigned),
                group.Count(note => note.Status == NoteStatus.InProgress),
                group.Count(note => note.Status == NoteStatus.PendingVerification),
                group.Count(note => note.Status == NoteStatus.Reopened),
                group.Count(note =>
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled &&
                    !note.Assignments.Any(assignment => assignment.IsCurrent)),
                group.Count(note =>
                    (note.Status == NoteStatus.Open || note.Status == NoteStatus.Reopened) &&
                    !note.Assignments.Any(assignment => assignment.IsCurrent) &&
                    (
                        !note.RoutingDecisions.Any() ||
                        note.RoutingDecisions
                            .OrderByDescending(decision => decision.DecidedAtUtc)
                            .Take(1)
                            .Any(decision =>
                                decision.ResultStatus == NoteRoutingResultStatus.NoMatchingRule ||
                                decision.ResultStatus == NoteRoutingResultStatus.NoEligibleUser ||
                                decision.ResultStatus == NoteRoutingResultStatus.InvalidTarget ||
                                decision.ResultStatus == NoteRoutingResultStatus.Failed))),
                group.Count(note =>
                    note.DueAtUtc.HasValue &&
                    note.DueAtUtc < now &&
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled),
                group.Count(note =>
                    note.DueAtUtc.HasValue &&
                    note.DueAtUtc >= now &&
                    note.DueAtUtc <= dueSoonUpper &&
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled),
                group.Count(note =>
                    (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled),
                group.Count(note =>
                    note.DueAtUtc.HasValue &&
                    note.DueAtUtc < now &&
                    note.Status != NoteStatus.Closed &&
                    note.Status != NoteStatus.Cancelled &&
                    !note.Assignments.Any(assignment => assignment.IsCurrent))))
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? NoteSummaryAggregate.Empty;
    }

    internal static async Task<CorrectiveActionSummaryAggregate> AggregateCorrectiveActionsSummaryAsync(
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var row = await actions
            .GroupBy(_ => 1)
            .Select(group => new CorrectiveActionSummaryAggregate(
                group.Count(action =>
                    action.Status != CorrectiveActionStatus.Completed &&
                    action.Status != CorrectiveActionStatus.Cancelled),
                group.Count(action =>
                    action.DueAtUtc.HasValue &&
                    action.DueAtUtc < now &&
                    action.Status != CorrectiveActionStatus.Completed &&
                    action.Status != CorrectiveActionStatus.Cancelled),
                group.Count(action => action.Status == CorrectiveActionStatus.PendingVerification),
                group.Count(action => action.Status == CorrectiveActionStatus.Reopened)))
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? CorrectiveActionSummaryAggregate.Empty;
    }

    internal static async Task<int> CountNotesWithStalledActionsAsync(
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var openNoteIds = notes.Where(note =>
            note.Status != NoteStatus.Closed &&
            note.Status != NoteStatus.Cancelled).Select(note => note.Id);

        return await FilterOverdueActions(actions, now)
            .Where(action => openNoteIds.Contains(action.OperationalNoteId))
            .Select(action => action.OperationalNoteId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    internal static async Task<RoutingFailureAggregate> AggregateRoutingFailuresAsync(
        IBaseeraDbContext db,
        IQueryable<OperationalNote> notes,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var scopedNoteIds = notes.Select(note => note.Id);
        var row = await db.NoteRoutingDecisions.AsNoTracking()
            .Where(decision =>
                scopedNoteIds.Contains(decision.OperationalNoteId) &&
                decision.DecidedAtUtc >= fromUtc &&
                decision.DecidedAtUtc <= toUtc)
            .GroupBy(_ => 1)
            .Select(group => new RoutingFailureAggregate(
                group.Count(decision => decision.ResultStatus == NoteRoutingResultStatus.NoMatchingRule),
                group.Count(decision => decision.ResultStatus == NoteRoutingResultStatus.NoEligibleUser),
                group.Count(decision =>
                    decision.ResultStatus == NoteRoutingResultStatus.InvalidTarget ||
                    decision.ResultStatus == NoteRoutingResultStatus.Failed)))
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? RoutingFailureAggregate.Empty;
    }

    internal static async Task<int> CountActiveEscalationsAsync(
        IBaseeraDbContext db,
        IQueryable<OperationalNote> notes,
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var scopedNoteIds = notes.Select(note => note.Id);
        var scopedActionIds = actions.Select(action => action.Id);

        return await db.EscalationOccurrences.AsNoTracking()
            .CountAsync(
                occurrence =>
                    occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated &&
                    occurrence.DetectedAtUtc >= fromUtc &&
                    occurrence.DetectedAtUtc <= toUtc &&
                    (
                        (occurrence.TargetType == EscalationTargetType.OperationalNote &&
                         scopedNoteIds.Contains(occurrence.TargetId)) ||
                        (occurrence.TargetType == EscalationTargetType.CorrectiveAction &&
                         scopedActionIds.Contains(occurrence.TargetId))),
                cancellationToken);
    }

    internal static async Task<List<(TKey Key, NoteBreakdownAggregate Aggregate)>> LoadNoteBreakdownAggregatesAsync<TKey>(
        IQueryable<OperationalNote> notes,
        Expression<Func<OperationalNote, TKey>> keySelector,
        DateTimeOffset now,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var rows = await notes
            .GroupBy(keySelector)
            .Select(group => new
            {
                Key = group.Key,
                Aggregate = new NoteBreakdownAggregate(
                    group.Count(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled),
                    group.Count(note =>
                        note.DueAtUtc.HasValue &&
                        note.DueAtUtc < now &&
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled),
                    group.Count(note =>
                        (note.Severity == NoteSeverity.High || note.Severity == NoteSeverity.Critical) &&
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled),
                    group.Count(note =>
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled &&
                        !note.Assignments.Any(assignment => assignment.IsCurrent)),
                    group.Count(note =>
                        note.Status == NoteStatus.Closed &&
                        note.ClosedAtUtc.HasValue &&
                        note.ClosedAtUtc >= fromUtc &&
                        note.ClosedAtUtc <= toUtc),
                    group.Count(note =>
                        note.Status == NoteStatus.Closed &&
                        note.ClosedAtUtc.HasValue &&
                        note.ClosedAtUtc >= fromUtc &&
                        note.ClosedAtUtc <= toUtc &&
                        note.DueAtUtc.HasValue &&
                        note.ClosedAtUtc <= note.DueAtUtc))
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.Key, row.Aggregate)).ToList();
    }

    internal static OperationalDashboardBreakdownRowDto ToBreakdownRow(
        string key,
        string labelAr,
        Guid? entityId,
        NoteBreakdownAggregate aggregate,
        int correctiveActionsOverdue) =>
        new(
            key,
            labelAr,
            entityId,
            aggregate.OpenBurden,
            aggregate.Overdue,
            aggregate.Critical,
            aggregate.Unassigned,
            correctiveActionsOverdue,
            ClosureRateWithinDue(aggregate.ClosedTotal, aggregate.ClosedWithinDue));

    internal static Dictionary<DateTime, int> ToDailyCountMap(IEnumerable<DailyCountRow> rows) =>
        rows.GroupBy(row => row.SaudiDay).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));

    internal static Dictionary<DateTime, DailyRoutingRow> ToDailyRoutingMap(IEnumerable<DailyRoutingRow> rows) =>
        rows.GroupBy(row => row.SaudiDay)
            .ToDictionary(
                group => group.Key,
                group => new DailyRoutingRow(
                    group.Key,
                    group.Sum(item => item.Success),
                    group.Sum(item => item.Failure)));

    internal static int SumDailyCountsForBucket(
        IReadOnlyDictionary<DateTime, int> dailyCounts,
        DateTimeOffset bucketStartUtc,
        DateTimeOffset bucketEndUtc,
        string granularity)
    {
        if (granularity == OperationalDashboardKpiDefinitions.DailyGranularity)
        {
            return dailyCounts.GetValueOrDefault(SaudiCalendarDay(bucketStartUtc));
        }

        var sum = 0;
        var cursor = bucketStartUtc;
        while (cursor <= bucketEndUtc)
        {
            sum += dailyCounts.GetValueOrDefault(SaudiCalendarDay(cursor));
            cursor = OperationalDashboardPeriodResolver.StartOfSaudiDayUtc(cursor.AddDays(1));
        }

        return sum;
    }

    internal static (int Success, int Failure) SumDailyRoutingForBucket(
        IReadOnlyDictionary<DateTime, DailyRoutingRow> dailyRouting,
        DateTimeOffset bucketStartUtc,
        DateTimeOffset bucketEndUtc,
        string granularity)
    {
        if (granularity == OperationalDashboardKpiDefinitions.DailyGranularity)
        {
            if (dailyRouting.TryGetValue(SaudiCalendarDay(bucketStartUtc), out var row))
            {
                return (row.Success, row.Failure);
            }

            return (0, 0);
        }

        var success = 0;
        var failure = 0;
        var cursor = bucketStartUtc;
        while (cursor <= bucketEndUtc)
        {
            if (dailyRouting.TryGetValue(SaudiCalendarDay(cursor), out var row))
            {
                success += row.Success;
                failure += row.Failure;
            }

            cursor = OperationalDashboardPeriodResolver.StartOfSaudiDayUtc(cursor.AddDays(1));
        }

        return (success, failure);
    }
}
