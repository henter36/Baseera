namespace Baseera.Application.Workspaces;

using Baseera.Application.Abstractions;
using Baseera.Application.Dashboard;
using Baseera.Application.Forms.Compliance;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

internal interface IFacilityWorkspaceReadService
{
    Task<FacilityWorkspaceFacilityInfo> GetFacilityAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityWorkspaceMetrics> GetMetricsAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityNotesOverviewPayload> GetNotesOverviewAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityCorrectiveActionsPayload> GetCorrectiveActionsAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityAlertsEscalationsPayload> GetAlertsEscalationsAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityFormCompliancePayload> GetFormComplianceAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityPriorityQueuePayload> GetPriorityQueueAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityRecentActivityPayload> GetRecentActivityAsync(WorkspaceContext context, CancellationToken cancellationToken);
}

internal sealed class FacilityWorkspaceReadService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    OperationalDashboardFilterBuilder dashboardFilters,
    IOperationalDashboardQueryService dashboard,
    IFormComplianceQueryService formCompliance,
    TimeProvider timeProvider) : IFacilityWorkspaceReadService
{
    private const int PriorityLimit = 10;
    private const int RecentActivityLimit = 10;
    private readonly Dictionary<string, object> cache = new(StringComparer.Ordinal);

    public async Task<FacilityWorkspaceFacilityInfo> GetFacilityAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"facility:{context.FacilityId}", async () =>
        {
            var facilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context);
            var row = await db.Facilities.AsNoTracking()
                .Where(facility => facility.Id == facilityId && !facility.IsDeleted)
                .Select(facility => new FacilityWorkspaceFacilityInfo(
                    facility.Id,
                    facility.NameAr,
                    facility.RegionId,
                    facility.Region.NameAr,
                    facility.FacilityType))
                .SingleAsync(cancellationToken);
            return row;
        });
    }

    public async Task<FacilityWorkspaceMetrics> GetMetricsAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"metrics:{CacheKey(context)}", async () =>
        {
            var facility = await GetFacilityAsync(context, cancellationToken);
            var notes = await GetNotesOverviewAsync(context, cancellationToken);
            var actions = await GetCorrectiveActionsAsync(context, cancellationToken);
            var alerts = await GetAlertsEscalationsAsync(context, cancellationToken);
            var forms = await GetFormComplianceAsync(context, cancellationToken);
            return new FacilityWorkspaceMetrics(facility, notes, actions, alerts, forms);
        });
    }

    public async Task<FacilityNotesOverviewPayload> GetNotesOverviewAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"notes-overview:{CacheKey(context)}", async () =>
        {
            var dashboardSummary = await GetDashboardSummaryAsync(context, cancellationToken);
            return await BuildNotesOverviewAsync(context, dashboardSummary, cancellationToken);
        });
    }

    public async Task<FacilityCorrectiveActionsPayload> GetCorrectiveActionsAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"corrective-actions:{CacheKey(context)}", async () =>
        {
            var dashboardSummary = await GetDashboardSummaryAsync(context, cancellationToken);
            return await BuildCorrectiveActionsAsync(context, dashboardSummary, cancellationToken);
        });
    }

    public async Task<FacilityAlertsEscalationsPayload> GetAlertsEscalationsAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"alerts-escalations:{CacheKey(context)}", async () =>
            await BuildAlertsAsync(context, cancellationToken));
    }

    public async Task<FacilityFormCompliancePayload> GetFormComplianceAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"form-compliance:{CacheKey(context)}", async () =>
            await BuildFormComplianceAsync(context, cancellationToken));
    }

    public async Task<FacilityPriorityQueuePayload> GetPriorityQueueAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"priority:{CacheKey(context)}", async () =>
        {
            var now = timeProvider.GetUtcNow();
            var notes = await GetScopedNotesAsync(context, cancellationToken);
            var actions = await GetScopedActionsAsync(context, cancellationToken);
            var items = new List<FacilityPriorityItemPayload>(PriorityLimit * 4);

            items.AddRange(await BuildCriticalNotePriorityItemsAsync(notes, now, cancellationToken));
            items.AddRange(await BuildOverdueNotePriorityItemsAsync(notes, now, cancellationToken));
            items.AddRange(await BuildOverdueActionPriorityItemsAsync(actions, now, cancellationToken));
            items.AddRange(await BuildEscalationPriorityItemsAsync(context, cancellationToken));
            items.AddRange(await BuildFormPriorityItemsAsync(context, cancellationToken));

            return new FacilityPriorityQueuePayload(
                PriorityLimit,
                items
                    .OrderByDescending(item => item.PriorityRank)
                    .ThenBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(item => item.Reference, StringComparer.Ordinal)
                    .Take(PriorityLimit)
                    .ToList());
        });
    }

    public async Task<FacilityRecentActivityPayload> GetRecentActivityAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"activity:{CacheKey(context)}", async () =>
        {
            var notes = await GetScopedNotesAsync(context, cancellationToken);
            var actions = await GetScopedActionsAsync(context, cancellationToken);
            var events = new List<FacilityActivityItemPayload>(RecentActivityLimit * 4);

            events.AddRange(await BuildRecentNoteEventsAsync(notes, cancellationToken));
            events.AddRange(await BuildRecentActionEventsAsync(actions, cancellationToken));
            events.AddRange(await BuildRecentEscalationEventsAsync(context, cancellationToken));
            events.AddRange(await BuildRecentFormEventsAsync(context, cancellationToken));

            return new FacilityRecentActivityPayload(
                RecentActivityLimit,
                events
                    .OrderByDescending(item => item.OccurredAtUtc)
                    .Take(RecentActivityLimit)
                    .ToList());
        });
    }

    private async Task<OperationalDashboardSummaryDto> GetDashboardSummaryAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"dashboard:{CacheKey(context)}", async () =>
            await dashboard.GetSummaryAsync(ToDashboardQuery(context), cancellationToken));
    }

    private async Task<IQueryable<OperationalNote>> GetScopedNotesAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"notes-query:{CacheKey(context)}", async () =>
            await dashboardFilters.BuildScopedNotesAsync(ToDashboardQuery(context), cancellationToken));
    }

    private async Task<IQueryable<CorrectiveAction>> GetScopedActionsAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"actions-query:{CacheKey(context)}", async () =>
            await dashboardFilters.BuildScopedCorrectiveActionsAsync(ToDashboardQuery(context), cancellationToken));
    }

    private async Task<FacilityNotesOverviewPayload> BuildNotesOverviewAsync(
        WorkspaceContext context,
        OperationalDashboardSummaryDto dashboardSummary,
        CancellationToken cancellationToken)
    {
        var notes = await GetScopedNotesAsync(context, cancellationToken);
        var newInPeriod = await notes.CountAsync(
            note => note.CreatedAtUtc >= context.FromUtc && note.CreatedAtUtc <= context.ToUtc,
            cancellationToken);
        var requiresMyAction = currentUser.UserId is null
            ? 0
            : await notes.CountAsync(
                note => note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled &&
                        note.Assignments.Any(assignment => assignment.IsCurrent && assignment.AssignedToUserId == currentUser.UserId),
                cancellationToken);
        var topTypes = await notes
            .Where(note => note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled)
            .GroupBy(note => new { note.NoteTypeId, note.NoteType.NameAr })
            .Select(group => new FacilityTopBucketPayload(group.Key.NameAr, group.Count()))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.LabelAr)
            .Take(3)
            .ToListAsync(cancellationToken);

        return new FacilityNotesOverviewPayload(
            dashboardSummary.Workload?.OpenTotal ?? 0,
            await notes.CountAsync(note => note.Severity == NoteSeverity.Critical && note.Status != NoteStatus.Closed && note.Status != NoteStatus.Cancelled, cancellationToken),
            dashboardSummary.Risk?.Overdue ?? 0,
            dashboardSummary.Workload?.Unassigned ?? 0,
            requiresMyAction,
            newInPeriod,
            topTypes);
    }

    private async Task<FacilityCorrectiveActionsPayload> BuildCorrectiveActionsAsync(
        WorkspaceContext context,
        OperationalDashboardSummaryDto dashboardSummary,
        CancellationToken cancellationToken)
    {
        var actions = await GetScopedActionsAsync(context, cancellationToken);
        var averageClosureHours = await actions
            .Where(action =>
                action.Status == CorrectiveActionStatus.Completed &&
                action.CompletedAtUtc.HasValue &&
                action.SubmittedAtUtc.HasValue &&
                action.CompletedAtUtc >= action.SubmittedAtUtc)
            .Select(action => new
            {
                CompletedAtUtc = action.CompletedAtUtc ?? DateTimeOffset.MinValue,
                SubmittedAtUtc = action.SubmittedAtUtc ?? DateTimeOffset.MinValue
            })
            .AverageAsync(action => (double?)((action.CompletedAtUtc - action.SubmittedAtUtc).TotalHours), cancellationToken);

        return new FacilityCorrectiveActionsPayload(
            dashboardSummary.CorrectiveActions?.Active ?? 0,
            dashboardSummary.CorrectiveActions?.Overdue ?? 0,
            await actions.CountAsync(action => action.Status == CorrectiveActionStatus.InProgress, cancellationToken),
            dashboardSummary.CorrectiveActions?.PendingVerification ?? 0,
            dashboardSummary.CorrectiveActions?.Reopened ?? 0,
            await actions.CountAsync(action => action.Priority == CorrectiveActionPriority.Critical && action.Status != CorrectiveActionStatus.Completed && action.Status != CorrectiveActionStatus.Cancelled, cancellationToken),
            averageClosureHours);
    }

    private async Task<FacilityAlertsEscalationsPayload> BuildAlertsAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var notes = await GetScopedNotesAsync(context, cancellationToken);
        var actions = await GetScopedActionsAsync(context, cancellationToken);
        var occurrences = BuildScopedEscalations(notes, actions);
        var personalUnread = currentUser.UserId is null
            ? 0
            : await db.Notifications.AsNoTracking().CountAsync(
                notification =>
                    notification.RecipientUserId == currentUser.UserId &&
                    notification.Status == NotificationStatus.Unread &&
                    notification.EscalationOccurrenceId.HasValue &&
                    occurrences.Select(occurrence => occurrence.Id).Contains(notification.EscalationOccurrenceId.Value),
                cancellationToken);

        var openEscalations = await occurrences.CountAsync(
            occurrence => occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated,
            cancellationToken);
        var criticalEscalations = await occurrences.CountAsync(
            occurrence => occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated &&
                          occurrence.EscalationLevel >= 2,
            cancellationToken);
        var overdueAlerts = await occurrences.CountAsync(
            occurrence => occurrence.TriggerType == EscalationTriggerType.Overdue &&
                          occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated,
            cancellationToken);
        var latest = await occurrences
            .Where(occurrence => occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated)
            .MaxAsync(occurrence => (DateTimeOffset?)occurrence.DetectedAtUtc, cancellationToken);

        return new FacilityAlertsEscalationsPayload(
            personalUnread,
            openEscalations,
            criticalEscalations,
            overdueAlerts,
            latest);
    }

    private async Task<FacilityFormCompliancePayload> BuildFormComplianceAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var query = new FormComplianceQuery
        {
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            FacilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context),
            Page = 1,
            PageSize = 1
        };
        var summary = await formCompliance.GetSummaryAsync(query, cancellationToken);
        var pending = await formCompliance.GetPendingAsync(query, cancellationToken);
        var nearestDue = pending.Items.OrderBy(item => item.EffectiveDueAtUtc).FirstOrDefault()?.EffectiveDueAtUtc;

        return new FacilityFormCompliancePayload
        {
            TargetedForms = summary.TargetedAssignmentCount,
            CompletedForms = summary.CompletedCount,
            RemainingForms = summary.RemainingCount,
            OverdueForms = summary.OverdueCount,
            CompletionRate = summary.CompletionRate,
            NearestDueAtUtc = nearestDue,
            NotStartedForms = summary.NotStartedCount,
            PendingReviewForms = summary.SubmittedCount + summary.UnderReviewCount
        };
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildCriticalNotePriorityItemsAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await notes
            .Where(note =>
                note.Severity == NoteSeverity.Critical &&
                note.Status != NoteStatus.Closed &&
                note.Status != NoteStatus.Cancelled)
            .OrderBy(note => note.DueAtUtc ?? DateTimeOffset.MaxValue)
            .Take(PriorityLimit)
            .Select(note => new
            {
                note.Id,
                note.ReferenceNumber,
                note.Title,
                note.DueAtUtc,
                Owner = note.Assignments.Where(assignment => assignment.IsCurrent)
                    .OrderByDescending(assignment => assignment.AssignedAtUtc)
                    .Select(assignment => assignment.AssignedToUser != null
                        ? assignment.AssignedToUser.DisplayNameAr
                        : assignment.AssignedToDepartment != null ? assignment.AssignedToDepartment.NameAr : null)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FacilityPriorityItemPayload
        {
            Type = "note",
            Reference = row.ReferenceNumber,
            TitleAr = row.Title,
            SeverityAr = "حرجة",
            PriorityRank = 90,
            ReasonAr = "ملاحظة حرجة مفتوحة",
            DueAtUtc = row.DueAtUtc,
            OverdueDays = DaysOverdue(row.DueAtUtc, now),
            OwnerAr = row.Owner,
            ActionLabelAr = "فتح الملاحظة",
            DrillDownTarget = NoteTarget(row.Id)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildOverdueNotePriorityItemsAsync(
        IQueryable<OperationalNote> notes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await notes
            .Where(note =>
                note.DueAtUtc.HasValue &&
                note.DueAtUtc < now &&
                note.Status != NoteStatus.Closed &&
                note.Status != NoteStatus.Cancelled)
            .OrderBy(note => note.DueAtUtc)
            .Take(PriorityLimit)
            .Select(note => new { note.Id, note.ReferenceNumber, note.Title, note.Severity, note.DueAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FacilityPriorityItemPayload
        {
            Type = "note",
            Reference = row.ReferenceNumber,
            TitleAr = row.Title,
            SeverityAr = NoteDisplay.SeverityAr(row.Severity),
            PriorityRank = 80 + Math.Min(DaysOverdue(row.DueAtUtc, now) ?? 0, 9),
            ReasonAr = "ملاحظة متأخرة",
            DueAtUtc = row.DueAtUtc,
            OverdueDays = DaysOverdue(row.DueAtUtc, now),
            ActionLabelAr = "فتح الملاحظة",
            DrillDownTarget = NoteTarget(row.Id)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildOverdueActionPriorityItemsAsync(
        IQueryable<CorrectiveAction> actions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rows = await actions
            .Where(action =>
                action.DueAtUtc.HasValue &&
                action.DueAtUtc < now &&
                action.Status != CorrectiveActionStatus.Completed &&
                action.Status != CorrectiveActionStatus.Cancelled)
            .OrderBy(action => action.DueAtUtc)
            .Take(PriorityLimit)
            .Select(action => new { action.Id, action.ReferenceNumber, action.Title, action.Priority, action.DueAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FacilityPriorityItemPayload
        {
            Type = "corrective-action",
            Reference = row.ReferenceNumber,
            TitleAr = row.Title,
            SeverityAr = CorrectiveActionDisplay.PriorityAr(row.Priority),
            PriorityRank = 70 + Math.Min(DaysOverdue(row.DueAtUtc, now) ?? 0, 9),
            ReasonAr = "إجراء تصحيحي متأخر",
            DueAtUtc = row.DueAtUtc,
            OverdueDays = DaysOverdue(row.DueAtUtc, now),
            ActionLabelAr = "فتح الإجراء",
            DrillDownTarget = CorrectiveActionTarget(row.Id)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildEscalationPriorityItemsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var notes = await GetScopedNotesAsync(context, cancellationToken);
        var actions = await GetScopedActionsAsync(context, cancellationToken);
        var rows = await BuildScopedEscalations(notes, actions)
            .Where(occurrence => occurrence.Status == EscalationOccurrenceStatus.NotificationsCreated)
            .OrderByDescending(occurrence => occurrence.EscalationLevel)
            .ThenByDescending(occurrence => occurrence.DetectedAtUtc)
            .Take(PriorityLimit)
            .Select(occurrence => new
            {
                occurrence.Id,
                occurrence.TargetReferenceNumber,
                occurrence.TargetType,
                occurrence.TargetId,
                occurrence.EscalationLevel,
                occurrence.TriggerType,
                occurrence.DueAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FacilityPriorityItemPayload
        {
            Type = "escalation",
            Reference = row.TargetReferenceNumber,
            TitleAr = $"تصعيد {EscalationDisplay.TargetTypeAr(row.TargetType)}",
            SeverityAr = row.EscalationLevel >= 2 ? "حرج" : "عال",
            PriorityRank = 75 + row.EscalationLevel,
            ReasonAr = EscalationDisplay.TriggerTypeAr(row.TriggerType),
            DueAtUtc = row.DueAtUtc,
            ActionLabelAr = "فتح التصعيد",
            DrillDownTarget = EscalationsTarget()
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildFormPriorityItemsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var pending = await formCompliance.GetPendingAsync(new FormComplianceQuery
        {
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            FacilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context),
            IsOverdue = true,
            Page = 1,
            PageSize = PriorityLimit
        }, cancellationToken);

        return pending.Items.Select(item => new FacilityPriorityItemPayload
        {
            Type = "form",
            Reference = item.OccurrenceKey,
            TitleAr = item.CampaignNameAr,
            SeverityAr = "متأخر",
            PriorityRank = 65 + Math.Min(item.DaysOverdue ?? 0, 9),
            ReasonAr = "نموذج متأخر",
            DueAtUtc = item.EffectiveDueAtUtc,
            OverdueDays = item.DaysOverdue,
            OwnerAr = item.ResponsibleUserName,
            ActionLabelAr = "فتح التزام النماذج",
            DrillDownTarget = FormComplianceTarget(context)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentNoteEventsAsync(
        IQueryable<OperationalNote> notes,
        CancellationToken cancellationToken)
    {
        var created = await notes
            .OrderByDescending(note => note.CreatedAtUtc)
            .Take(5)
            .Select(note => new { note.Id, note.ReferenceNumber, note.Title, note.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return created.Select(note => new FacilityActivityItemPayload
        {
            EventType = "note.created",
            TitleAr = $"إنشاء ملاحظة {note.ReferenceNumber}",
            DescriptionAr = note.Title,
            OccurredAtUtc = note.CreatedAtUtc,
            EntityReference = note.ReferenceNumber,
            Tone = "info",
            DrillDownTarget = NoteTarget(note.Id)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentActionEventsAsync(
        IQueryable<CorrectiveAction> actions,
        CancellationToken cancellationToken)
    {
        var rows = await actions
            .OrderByDescending(action => action.CreatedAtUtc)
            .Take(5)
            .Select(action => new { action.Id, action.ReferenceNumber, action.Title, action.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(action => new FacilityActivityItemPayload
        {
            EventType = "corrective-action.created",
            TitleAr = $"إنشاء إجراء {action.ReferenceNumber}",
            DescriptionAr = action.Title,
            OccurredAtUtc = action.CreatedAtUtc,
            EntityReference = action.ReferenceNumber,
            Tone = "info",
            DrillDownTarget = CorrectiveActionTarget(action.Id)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentEscalationEventsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var notes = await GetScopedNotesAsync(context, cancellationToken);
        var actions = await GetScopedActionsAsync(context, cancellationToken);
        var rows = await BuildScopedEscalations(notes, actions)
            .OrderByDescending(occurrence => occurrence.DetectedAtUtc)
            .Take(5)
            .Select(occurrence => new
            {
                occurrence.TargetReferenceNumber,
                occurrence.TargetType,
                occurrence.TriggerType,
                occurrence.DetectedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FacilityActivityItemPayload
        {
            EventType = "escalation.created",
            TitleAr = $"تصعيد {row.TargetReferenceNumber}",
            DescriptionAr = $"{EscalationDisplay.TargetTypeAr(row.TargetType)} - {EscalationDisplay.TriggerTypeAr(row.TriggerType)}",
            OccurredAtUtc = row.DetectedAtUtc,
            EntityReference = row.TargetReferenceNumber,
            Tone = "warn",
            DrillDownTarget = EscalationsTarget()
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentFormEventsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var pending = await formCompliance.GetPendingAsync(new FormComplianceQuery
        {
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            FacilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context),
            Page = 1,
            PageSize = 5
        }, cancellationToken);

        return pending.Items.Select(item => new FacilityActivityItemPayload
        {
            EventType = "form.pending",
            TitleAr = $"نموذج مطلوب {item.OccurrenceKey}",
            DescriptionAr = item.CampaignNameAr,
            OccurredAtUtc = item.OpenAtUtc,
            ActorDisplayName = item.ResponsibleUserName,
            EntityReference = item.OccurrenceKey,
            Tone = item.IsOverdue ? "danger" : "muted",
            DrillDownTarget = FormComplianceTarget(context)
        }).ToList();
    }

    private IQueryable<EscalationOccurrence> BuildScopedEscalations(IQueryable<OperationalNote> notes, IQueryable<CorrectiveAction> actions)
    {
        var noteIds = notes.Select(note => note.Id);
        var actionIds = actions.Select(action => action.Id);

        return db.EscalationOccurrences.AsNoTracking().Where(occurrence =>
            (occurrence.TargetType == EscalationTargetType.OperationalNote && noteIds.Contains(occurrence.TargetId)) ||
            (occurrence.TargetType == EscalationTargetType.CorrectiveAction && actionIds.Contains(occurrence.TargetId)));
    }

    private OperationalDashboardQuery ToDashboardQuery(WorkspaceContext context) =>
        new()
        {
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            RegionId = context.RegionId,
            FacilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context)
        };

    private async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory)
        where T : notnull
    {
        if (cache.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }

        var created = await factory();
        cache[key] = created;
        return created;
    }

    private static string CacheKey(WorkspaceContext context) =>
        $"{context.WorkspaceKey}:{context.Level}:{context.FacilityId}:{context.FromUtc:O}:{context.ToUtc:O}";

    private static int? DaysOverdue(DateTimeOffset? dueAtUtc, DateTimeOffset now) =>
        dueAtUtc.HasValue && dueAtUtc.Value < now ? Math.Max(0, (int)Math.Floor((now - dueAtUtc.Value).TotalDays)) : null;

    private static DrillDownTarget NoteTarget(Guid noteId) =>
        new("notes.workspace", "فتح الملاحظة", new Dictionary<string, string> { ["noteId"] = noteId.ToString() }, new Dictionary<string, string>(), PermissionCodes.NotesView);

    private static DrillDownTarget CorrectiveActionTarget(Guid actionId) =>
        new("corrective-actions.list", "فتح الإجراء", new Dictionary<string, string> { ["id"] = actionId.ToString() }, new Dictionary<string, string>(), PermissionCodes.CorrectiveActionsView);

    private static DrillDownTarget EscalationsTarget() =>
        new("escalations.occurrences", "فتح التصعيدات", new Dictionary<string, string>(), new Dictionary<string, string>(), PermissionCodes.EscalationsViewOccurrences);

    private static DrillDownTarget FormComplianceTarget(WorkspaceContext context) =>
        new(
            "form-compliance.facility",
            "فتح التزام النماذج",
            new Dictionary<string, string> { ["facilityId"] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString() },
            FacilityWorkspaceDrillDownFilters.Preserve(context),
            PermissionCodes.FormsViewComplianceDashboard);

}

internal static class FacilityWorkspaceDrillDownFilters
{
    public static IReadOnlyDictionary<string, string> Preserve(WorkspaceContext context)
    {
        var filters = new Dictionary<string, string>
        {
            ["fromUtc"] = context.FromUtc.ToString("O"),
            ["toUtc"] = context.ToUtc.ToString("O")
        };

        if (context.RegionId.HasValue)
        {
            filters["regionId"] = context.RegionId.Value.ToString();
        }

        if (context.FacilityId.HasValue)
        {
            filters["facilityId"] = context.FacilityId.Value.ToString();
        }

        return filters;
    }
}
