namespace Baseera.Application.Dashboard;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public sealed class OperationalDashboardFilterBuilder(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    INoteTypeAccessService typeAccess)
{
    public bool CanViewSensitive => NoteAccessHelper.CanViewSensitive(currentUser);

    public async Task<IQueryable<OperationalNote>> BuildScopedNotesAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var notes = await noteScope.FilterQueryableAsync(db.OperationalNotes.AsNoTracking(), cancellationToken);
        notes = await typeAccess.FilterViewableNotesAsync(notes, cancellationToken);

        if (!CanViewSensitive)
        {
            notes = notes.Where(note => note.Classification < ClassificationLevel.Confidential);
        }

        return ApplyDashboardFilters(notes, query);
    }

    public async Task<IQueryable<CorrectiveAction>> BuildScopedCorrectiveActionsAsync(
        OperationalDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var notes = await BuildScopedNotesAsync(query, cancellationToken);
        var noteIds = await notes.Select(note => note.Id).ToListAsync(cancellationToken);
        if (noteIds.Count == 0)
        {
            return db.CorrectiveActions.AsNoTracking().Where(_ => false);
        }

        var actions = db.CorrectiveActions.AsNoTracking().Where(action => noteIds.Contains(action.OperationalNoteId));

        if (!currentUser.HasPermission(PermissionCodes.CorrectiveActionsViewSensitive))
        {
            actions = actions.Where(action => action.Classification < ClassificationLevel.Confidential);
        }

        return actions;
    }

    public static IQueryable<OperationalNote> ApplyDashboardFilters(
        IQueryable<OperationalNote> notes,
        OperationalDashboardQuery query)
    {
        if (query.RegionId.HasValue)
        {
            notes = notes.Where(note => note.RegionId == query.RegionId.Value);
        }

        if (query.FacilityId.HasValue)
        {
            notes = notes.Where(note => note.FacilityId == query.FacilityId.Value);
        }

        if (query.FacilityUnitId.HasValue)
        {
            notes = notes.Where(note => note.FacilityUnitId == query.FacilityUnitId.Value);
        }

        if (query.NoteTypeId.HasValue)
        {
            notes = notes.Where(note => note.NoteTypeId == query.NoteTypeId.Value);
        }

        if (query.Severity.HasValue)
        {
            notes = notes.Where(note => note.Severity == query.Severity.Value);
        }

        if (query.Status.HasValue)
        {
            notes = notes.Where(note => note.Status == query.Status.Value);
        }

        return notes;
    }

    public static IQueryable<OperationalNote> ApplyRequiresRoutingFilter(IQueryable<OperationalNote> notes) =>
        notes.Where(note =>
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
                        decision.ResultStatus == NoteRoutingResultStatus.Failed)));

    public static IQueryable<OperationalNote> ApplyUnassignedOpenFilter(IQueryable<OperationalNote> notes) =>
        notes.Where(note =>
            note.Status != NoteStatus.Closed &&
            note.Status != NoteStatus.Cancelled &&
            !note.Assignments.Any(assignment => assignment.IsCurrent));
}

internal static class OperationalDashboardPeriodResolver
{
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) Resolve(
        OperationalDashboardQuery query,
        DateTimeOffset nowUtc)
    {
        if (query.FromUtc.HasValue || query.ToUtc.HasValue)
        {
            var from = query.FromUtc ?? nowUtc.AddDays(-OperationalDashboardKpiDefinitions.DefaultDueSoonDays);
            var to = query.ToUtc ?? nowUtc;
            ValidateRange(from, to);
            return (from, to);
        }

        var days = query.PeriodDays ?? 30;
        if (!OperationalDashboardKpiDefinitions.AllowedPeriodDays.Contains(days))
        {
            days = 30;
        }

        var periodTo = nowUtc;
        var periodFrom = nowUtc.AddDays(-days);
        ValidateRange(periodFrom, periodTo);
        return (periodFrom, periodTo);
    }

    public static void ValidateRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (fromUtc > toUtc)
        {
            throw new ArgumentException("from يجب أن يكون قبل to أو يساويه.");
        }

        if ((toUtc - fromUtc).TotalDays > OperationalDashboardKpiDefinitions.MaxTrendDays)
        {
            throw new ArgumentException($"الفترة الزمنية لا يمكن أن تتجاوز {OperationalDashboardKpiDefinitions.MaxTrendDays} يومًا.");
        }
    }

    public static DateTimeOffset StartOfSaudiDayUtc(DateTimeOffset utcInstant)
    {
        var saudi = TimeZoneInfo.ConvertTime(utcInstant, TimeZones.SaudiArabia);
        var midnight = new DateTime(saudi.Year, saudi.Month, saudi.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, TimeZones.SaudiArabia.GetUtcOffset(midnight)).ToUniversalTime();
    }

    public static DateTimeOffset EndOfSaudiDayUtc(DateTimeOffset utcInstant)
    {
        var start = StartOfSaudiDayUtc(utcInstant);
        return start.AddDays(1).AddTicks(-1);
    }
}
