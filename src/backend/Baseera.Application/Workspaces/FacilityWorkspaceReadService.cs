namespace Baseera.Application.Workspaces;

using Baseera.Application.Abstractions;
using Baseera.Application.Dashboard;
using Baseera.Application.Forms.Compliance;
using Baseera.Application.Occupancy;
using Baseera.Application.Resources;
using Baseera.Application.Workforce;
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
    Task<OccupancyWorkspacePayload> GetOccupancyAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<ResourceWorkspacePayload> GetResourcesAsync(WorkspaceContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Resource workspace read is not implemented by this test double.");
    Task<WorkforceWorkspacePayload> GetWorkforceAsync(WorkspaceContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Workforce workspace read is not implemented by this test double.");
    Task<FacilityPriorityQueuePayload> GetPriorityQueueAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityRecentActivityPayload> GetRecentActivityAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityStructurePayload> GetStructureAsync(WorkspaceContext context, CancellationToken cancellationToken);
    Task<FacilityDataQualityPayload> GetDataQualityAsync(WorkspaceContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Groups facility-level operational domain query services so the workspace read service
/// constructor stays within Sonar parameter limits without using a service locator.
/// </summary>
internal sealed class FacilityWorkspaceFacilityDomainQueries(
    IOccupancyQueryService occupancy,
    IResourceReadinessQueryService resources,
    IWorkforceReadinessQueryService workforce)
{
    public IOccupancyQueryService Occupancy { get; } = occupancy;
    public IResourceReadinessQueryService Resources { get; } = resources;
    public IWorkforceReadinessQueryService Workforce { get; } = workforce;
}

internal sealed class FacilityWorkspaceReadService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    OperationalDashboardFilterBuilder dashboardFilters,
    IOperationalDashboardQueryService dashboard,
    IFormComplianceQueryService formCompliance,
    FacilityWorkspaceFacilityDomainQueries facilityDomain,
    TimeProvider timeProvider) : IFacilityWorkspaceReadService
{
    private const int PriorityLimit = 10;
    private const int RecentActivityLimit = 10;
    private const int UnitLimit = 12;
    private const string DomainKeyWorkforce = "workforce";
    private const string DataQualityComplete = "complete";
    private const string DataQualityPartial = "partial";
    private const string SeverityCriticalAr = "حرجة";
    private const string SeverityHighAr = "عالية";
    private const string SeverityMediumAr = "متوسطة";
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
            var occupancyPayload = currentUser.HasPermission(PermissionCodes.OccupancyViewSummary)
                ? await GetOccupancyAsync(context, cancellationToken)
                : null;
            var resourcePayload = currentUser.HasPermission(PermissionCodes.ResourcesViewSummary)
                ? await GetResourcesAsync(context, cancellationToken)
                : null;
            var workforcePayload = currentUser.HasPermission(PermissionCodes.WorkforceViewSummary)
                ? await GetWorkforceAsync(context, cancellationToken)
                : null;
            return new FacilityWorkspaceMetrics(facility, notes, actions, alerts, forms, occupancyPayload, resourcePayload, workforcePayload);
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

    public async Task<OccupancyWorkspacePayload> GetOccupancyAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"occupancy:{CacheKey(context)}", async () =>
            await facilityDomain.Occupancy.GetWorkspacePayloadAsync(
                FacilityWorkspaceContextGuard.RequireFacilityId(context),
                context.FromUtc,
                context.ToUtc,
                cancellationToken));
    }

    public async Task<ResourceWorkspacePayload> GetResourcesAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"resources:{CacheKey(context)}", async () =>
            await facilityDomain.Resources.GetWorkspacePayloadAsync(
                FacilityWorkspaceContextGuard.RequireFacilityId(context),
                cancellationToken));
    }

    public async Task<WorkforceWorkspacePayload> GetWorkforceAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"workforce:{CacheKey(context)}", async () =>
            await facilityDomain.Workforce.GetWorkspacePayloadAsync(
                FacilityWorkspaceContextGuard.RequireFacilityId(context),
                cancellationToken));
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
            if (currentUser.HasPermission(PermissionCodes.OccupancyViewSummary))
            {
                items.AddRange(await BuildOccupancyPriorityItemsAsync(context, now, cancellationToken));
            }
            if (currentUser.HasPermission(PermissionCodes.ResourcesViewSummary))
            {
                items.AddRange(await BuildResourcePriorityItemsAsync(context, cancellationToken));
            }
            if (currentUser.HasPermission(PermissionCodes.WorkforceViewSummary))
            {
                items.AddRange(await BuildWorkforcePriorityItemsAsync(context, cancellationToken));
            }

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
            if (currentUser.HasPermission(PermissionCodes.OccupancyViewMovements))
            {
                events.AddRange(await BuildRecentOccupancyEventsAsync(context, cancellationToken));
            }
            if (currentUser.HasPermission(PermissionCodes.ResourcesViewMaintenance))
            {
                events.AddRange(await BuildRecentResourceEventsAsync(context, cancellationToken));
            }
            if (currentUser.HasPermission(PermissionCodes.WorkforceViewCoverage))
            {
                events.AddRange(await BuildRecentWorkforceEventsAsync(context, cancellationToken));
            }

            return new FacilityRecentActivityPayload(
                RecentActivityLimit,
                events
                    .OrderByDescending(item => item.OccurredAtUtc)
                    .Take(RecentActivityLimit)
                    .ToList());
        });
    }

    public async Task<FacilityStructurePayload> GetStructureAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"structure:{CacheKey(context)}", async () =>
        {
            var facilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context);
            var notes = await GetScopedNotesAsync(context, cancellationToken);
            var actions = await GetScopedActionsAsync(context, cancellationToken);

            var unitsCount = await db.FacilityUnits.AsNoTracking()
                .CountAsync(unit => unit.FacilityId == facilityId && !unit.IsDeleted && unit.IsActive, cancellationToken);
            var buildingsCount = await db.Buildings.AsNoTracking()
                .CountAsync(building => building.FacilityId == facilityId && !building.IsDeleted && building.IsActive, cancellationToken);
            var assetLocationsCount = await db.FacilityAssetLocations.AsNoTracking()
                .CountAsync(location => !location.IsDeleted && location.IsActive && location.Building.FacilityId == facilityId, cancellationToken);

            var rows = await db.FacilityUnits.AsNoTracking()
                .Where(unit => unit.FacilityId == facilityId && !unit.IsDeleted && unit.IsActive)
                .OrderBy(unit => unit.Code)
                .ThenBy(unit => unit.NameAr)
                .Take(UnitLimit)
                .Select(unit => new FacilityUnitOperationsPayload
                {
                    UnitId = unit.Id,
                    Code = unit.Code,
                    NameAr = unit.NameAr,
                    ParentUnitNameAr = unit.ParentUnit != null ? unit.ParentUnit.NameAr : null,
                    OpenNotes = notes.Count(note =>
                        note.FacilityUnitId == unit.Id &&
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled),
                    OverdueNotes = notes.Count(note =>
                        note.FacilityUnitId == unit.Id &&
                        note.DueAtUtc.HasValue &&
                        note.DueAtUtc < context.ToUtc &&
                        note.Status != NoteStatus.Closed &&
                        note.Status != NoteStatus.Cancelled),
                    OpenCorrectiveActions = actions.Count(action =>
                        action.OperationalNote.FacilityUnitId == unit.Id &&
                        action.Status != CorrectiveActionStatus.Completed &&
                        action.Status != CorrectiveActionStatus.Cancelled)
                })
                .ToListAsync(cancellationToken);

            return new FacilityStructurePayload(unitsCount, buildingsCount, assetLocationsCount, rows);
        });
    }

    public async Task<FacilityDataQualityPayload> GetDataQualityAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        return await GetOrAddAsync($"data-quality:{CacheKey(context)}", async () =>
        {
            var metrics = await GetMetricsAsync(context, cancellationToken);
            var structure = await GetStructureAsync(context, cancellationToken);
            var domains = new List<FacilityDataQualityDomainPayload>
            {
                AvailableDomain("structure", "الهيكل التنظيمي والوحدات", structure.UnitsCount + structure.BuildingsCount + structure.AssetLocationsCount, context.ToUtc, "يدعم قراءة الوحدة والموقع لكنه لا يحتوي إشغالًا أو جاهزية موارد."),
                AvailableDomain("notes", "الملاحظات التشغيلية", metrics.Notes.OpenNotes, context.ToUtc, "يدخل في الحالة العامة والعمل العاجل."),
                AvailableDomain("corrective-actions", "الإجراءات التصحيحية", metrics.CorrectiveActions.OpenActions, context.ToUtc, "يدخل في العمل العاجل وخط الحالة."),
                AvailableDomain("escalations", "التصعيدات والتنبيهات", metrics.Alerts.OpenEscalations + metrics.Alerts.PersonalUnreadNotifications, metrics.Alerts.LastEscalationProcessedAtUtc ?? context.ToUtc, "التنبيهات الشخصية منفصلة عن حالة المنشأة."),
                AvailableDomain("forms", "النماذج والالتزام", metrics.FormCompliance.TargetedForms, context.ToUtc, "يعتمد على قواعد لوحة الالتزام الحالية."),
                MissingDomain("incidents", "الوقوعات والحوادث", "لا يوجد نموذج Incident/Occurrence مستقل خارج الملاحظات والتصعيدات.", "#127"),
                MissingDomain("risks", "المخاطر والمعالجات", "لا يوجد Risk/RiskTreatment engine في النطاق الحالي.", "#16"),
                MissingDomain("projects", "المشاريع والمبادرات", "لا توجد كيانات Project أو Initiative مرتبطة بالسجن.", "#126"),
                MissingDomain("plans", "الخطط والطوارئ", "لا توجد كيانات OperationalPlan أو EmergencyPlan.", "#128"),
                MissingDomain("decisions", "القرارات والتوجيهات", "لا توجد كيانات Decision أو Directive تنفيذية.", "#125")
            };

            if (metrics.Occupancy is not null)
            {
                domains.Insert(5, OccupancyDomain(metrics.Occupancy));
            }
            if (metrics.Resources is not null)
            {
                domains.Insert(6, ResourcesDomain(metrics.Resources));
            }
            else
            {
                domains.Insert(6, MissingDomain("resources", "الموارد والجاهزية", "لا يملك المستخدم صلاحية عرض الموارد أو لم تُحمّل بيانات المجال.", "#15"));
            }

            if (metrics.Workforce is not null)
            {
                domains.Insert(7, WorkforceDomain(metrics.Workforce));
            }
            else
            {
                domains.Insert(7, MissingDomain(DomainKeyWorkforce, "القوى البشرية والتغطية", "لا يملك المستخدم صلاحية عرض القوى البشرية أو لم تُحمّل بيانات المجال.", "#133"));
            }

            return new FacilityDataQualityPayload(domains);
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

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildOccupancyPriorityItemsAsync(
        WorkspaceContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payload = await GetOccupancyAsync(context, cancellationToken);
        return payload.Interventions
            .Take(PriorityLimit)
            .Select(item => new FacilityPriorityItemPayload
            {
                Type = "occupancy",
                Reference = item.Reference,
                TitleAr = item.TitleAr,
                SeverityAr = item.SeverityAr,
                PriorityRank = item.PriorityRank,
                ReasonAr = item.ReasonAr,
                DueAtUtc = item.DueAtUtc,
                OverdueDays = DaysOverdue(item.DueAtUtc, now),
                OwnerAr = null,
                ActionLabelAr = item.ActionLabelAr,
                DrillDownTarget = OccupancyTarget(
                    context,
                    item.UnitId,
                    item.UnitId.HasValue ? PermissionCodes.OccupancyViewUnitBreakdown : PermissionCodes.OccupancyViewSummary)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildResourcePriorityItemsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var payload = await GetResourcesAsync(context, cancellationToken);
        return payload.Exceptions
            .Take(PriorityLimit)
            .Select(item => new FacilityPriorityItemPayload
            {
                Type = "resource",
                Reference = item.Reference,
                TitleAr = item.TitleAr,
                SeverityAr = item.SeverityAr,
                PriorityRank = item.PriorityRank,
                ReasonAr = item.ReasonAr,
                DueAtUtc = item.DueAtUtc,
                OverdueDays = null,
                OwnerAr = item.OwnerAr,
                ActionLabelAr = item.ActionLabelAr,
                DrillDownTarget = ResourceTarget(context, item.ResourceAssetId, PermissionCodes.ResourcesViewSummary)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildWorkforcePriorityItemsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var payload = await GetWorkforceAsync(context, cancellationToken);
        var facilityId = payload.Summary.FacilityId;
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var items = new List<FacilityPriorityItemPayload>();
        items.AddRange(BuildWorkforceSummaryPriorityItems(context, payload));
        items.AddRange(await BuildWorkforceRosterPriorityItemsAsync(context, facilityId, today, cancellationToken));
        items.AddRange(BuildWorkforceFatiguePriorityItems(context, payload, items));
        items.AddRange(BuildWorkforceQualityPriorityItems(context, payload));
        return items
            .GroupBy(i => i.Type)
            .Select(g => g.OrderByDescending(i => i.PriorityRank).First())
            .OrderByDescending(i => i.PriorityRank)
            .Take(PriorityLimit)
            .ToList();
    }

    private static IEnumerable<FacilityPriorityItemPayload> BuildWorkforceSummaryPriorityItems(
        WorkspaceContext context,
        Application.Workforce.WorkforceWorkspacePayload payload)
    {
        var facilityId = payload.Summary.FacilityId;
        if (payload.Summary.SafeGap > 0)
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.ShiftBelowMinimum, $"safe-gap:{facilityId}", "التغطية دون الحد الأدنى الآمن", SeverityCriticalAr, 930, $"الفجوة الآمنة {payload.Summary.SafeGap} مقابل الحد الأدنى {payload.Summary.MinimumSafe}", "مراجعة التغطية", PermissionCodes.WorkforceViewCoverage));
        }

        if (payload.Summary.CriticalPositionsAtRisk > 0)
        {
            var canViewCoverage = context.Permissions.Contains(PermissionCodes.WorkforceViewCoverage);
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(
                WorkforceOperationalCatalog.Interventions.CriticalRoleUncovered,
                $"critical:{facilityId}",
                "مواقع حرجة غير مغطاة",
                SeverityCriticalAr,
                920,
                canViewCoverage ? $"عدد المواقع الحرجة المعرضة للخطر: {payload.Summary.CriticalPositionsAtRisk}" : "يوجد تنبيه ملخص على تغطية المواقع الحرجة.",
                canViewCoverage ? "فتح القوى البشرية" : "عرض الملخص",
                PermissionCodes.WorkforceViewCoverage));
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.NoCriticalPositionAlternate, $"critical-alt:{facilityId}", "منصب حرج بلا بديل كافٍ", SeverityHighAr, 910, "يوجد مواقع حرجة بدون بديل تشغيلي كافٍ.", "مراجعة المناصب الحرجة", PermissionCodes.WorkforceViewCoverage));
        }

        if (payload.Summary.StaleRecords > 0)
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.WorkforceDataStale, $"stale:{facilityId}", "بيانات قوى بشرية متقادمة", SeverityMediumAr, 780, $"سجلات تحتاج تحققًا: {payload.Summary.StaleRecords}", "تحديث البيانات", PermissionCodes.WorkforceViewSummary));
        }

        if (payload.Summary.Gap > 0)
        {
            var severity = payload.Summary.CoverageStatus is Domain.Workforce.WorkforceCoverageStatus.Unsafe
                ? SeverityCriticalAr
                : SeverityHighAr;
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(
                WorkforceOperationalCatalog.Interventions.ShiftBelowMinimum,
                $"gap:{facilityId}",
                "فجوة تغطية تشغيلية",
                severity,
                860,
                $"الفجوة الحالية {payload.Summary.Gap} مقابل الاحتياج {payload.Summary.Required}",
                "مراجعة التغطية",
                PermissionCodes.WorkforceViewCoverage));
        }

        if (payload.Units.Any(u => u.Gap > 0))
        {
            var worst = payload.Units.OrderByDescending(u => u.Gap).First();
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.UnitStaffingGap, $"unit-gap:{worst.FacilityUnitId}", "فجوة تغطية وحدة", SeverityHighAr, 850, $"{worst.UnitNameAr}: فجوة {worst.Gap}", "مراجعة الوحدة", PermissionCodes.WorkforceViewCoverage));
        }

        if (payload.Summary.OnLeave > 0
            && payload.Summary.TotalMembers > 0
            && payload.Summary.OnLeave * 100 / payload.Summary.TotalMembers >= 20)
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.HighAbsenceRate, $"absence:{facilityId}", "معدل غياب مرتفع", SeverityHighAr, 830, $"في إجازة: {payload.Summary.OnLeave} من {payload.Summary.TotalMembers}", "مراجعة الغياب", PermissionCodes.WorkforceViewSummary));
        }

        if (payload.Summary.CoverageStatus is Domain.Workforce.WorkforceCoverageStatus.Unknown)
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.UnknownAvailability, $"unknown:{facilityId}", "توفر مجهول", SeverityMediumAr, 750, "حالة التغطية Unknown — لا تُحسب كـ Available.", "تحديث التوفر", PermissionCodes.WorkforceViewSummary));
        }

        if (HasLowWorkforceSourceQuality(payload.Summary))
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.WorkforceSourceConflict, $"source:{facilityId}", "تعارض أو ضعف مصدر الحقيقة", SeverityMediumAr, 760, "الثقة أو الحداثة منخفضة — يلزم reconciliation.", "فتح المصالحة", PermissionCodes.WorkforceReconcile));
        }
    }

    private static bool HasLowWorkforceSourceQuality(Application.Workforce.WorkforceSummaryDto summary) =>
        string.Equals(summary.ConfidenceLevel, "low", StringComparison.OrdinalIgnoreCase)
        || string.Equals(summary.FreshnessStatus, "stale", StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<FacilityPriorityItemPayload>> BuildWorkforceRosterPriorityItemsAsync(
        WorkspaceContext context,
        Guid facilityId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var items = new List<FacilityPriorityItemPayload>();
        var unpublished = await db.DutyRosters.AsNoTracking()
            .CountAsync(r => r.FacilityId == facilityId
                && !r.IsDeleted
                && r.Status == Domain.Workforce.DutyRosterStatuses.Draft
                && r.DutyDate >= today
                && r.DutyDate <= today.AddDays(1), cancellationToken);
        if (unpublished > 0)
        {
            items.Add(WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.UnpublishedRoster, $"unpub:{facilityId}", "جداول مناوبة غير منشورة", SeverityMediumAr, 800, $"مسودات لليوم/الغد: {unpublished}", "مراجعة الجداول", PermissionCodes.WorkforceViewCoverage)));
        }

        var commanderRoleIds = await db.WorkforceRoleDefinitions.AsNoTracking()
            .Where(r => !r.IsDeleted && r.Category == Domain.Workforce.WorkforceRoleCategory.Command)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (commanderRoleIds.Count > 0
            && !await HasPublishedRoleCoverageAsync(facilityId, today, commanderRoleIds, requirePresentLike: true, cancellationToken))
        {
            items.Add(WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.NoShiftCommander, $"commander:{facilityId}", "قائد مناوبة غير متوفر", SeverityCriticalAr, 925, "لا يوجد دور قيادي حاضر/مؤكد في مناوبات اليوم المنشورة.", "تعيين قائد مناوبة", PermissionCodes.WorkforceViewCoverage)));
        }

        var driverRoleIds = await db.WorkforceRoleDefinitions.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.Code.Contains("Driver") || r.NameAr.Contains("سائق")))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (driverRoleIds.Count > 0
            && !await HasPublishedRoleCoverageAsync(facilityId, today, driverRoleIds, requirePresentLike: false, cancellationToken))
        {
            items.Add(WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.NoQualifiedDriver, $"driver:{facilityId}", "سائق مؤهل غير متوفر", SeverityHighAr, 870, "لا تغطية لدور سائق في مناوبات اليوم.", "تأمين سائق مؤهل", PermissionCodes.WorkforceViewCoverage)));
        }

        return items;
    }

    private async Task<bool> HasPublishedRoleCoverageAsync(
        Guid facilityId,
        DateOnly today,
        IReadOnlyList<Guid> roleIds,
        bool requirePresentLike,
        CancellationToken cancellationToken)
    {
        var query = db.DutyRosterAssignments.AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.DutyRoster.FacilityId == facilityId
                && a.DutyRoster.DutyDate == today
                && a.DutyRoster.Status == Domain.Workforce.DutyRosterStatuses.Published
                && roleIds.Contains(a.RoleDefinitionId));
        if (requirePresentLike)
        {
            query = query.Where(a =>
                a.Status == Domain.Workforce.RosterAssignmentStatus.Present
                || a.Status == Domain.Workforce.RosterAssignmentStatus.Confirmed
                || a.Status == Domain.Workforce.RosterAssignmentStatus.Late);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private static IEnumerable<FacilityPriorityItemPayload> BuildWorkforceFatiguePriorityItems(
        WorkspaceContext context,
        Application.Workforce.WorkforceWorkspacePayload payload,
        IReadOnlyList<FacilityPriorityItemPayload> existing)
    {
        var facilityId = payload.Summary.FacilityId;
        var indicators = payload.Summary.FatigueIndicators;
        if (indicators.Contains(Application.Workforce.WorkforceFatiguePolicy.QualificationExpiringSoon))
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.QualificationExpired, $"qual:{facilityId}", "مؤهلات منتهية أو قاربت الانتهاء", SeverityHighAr, 840, "توجد مؤشرات انتهاء مؤهلات تؤثر على الجاهزية.", "مراجعة المؤهلات", PermissionCodes.WorkforceViewMembers));
        }

        if (indicators.Contains(Application.Workforce.WorkforceFatiguePolicy.ExcessiveOvertimeHours)
            || indicators.Contains(Application.Workforce.WorkforceFatiguePolicy.ConsecutiveShiftsWithoutRest))
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.ExcessiveOvertime, $"ot:{facilityId}", "إرهاق/ساعات إضافية مرتفعة", SeverityMediumAr, 770, "مؤشرات إرهاق تشغيلية نشطة.", "مراجعة المناوبات", PermissionCodes.WorkforceViewCoverage));
        }

        if (indicators.Contains(Application.Workforce.WorkforceFatiguePolicy.ConsecutiveShiftsWithoutRest))
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.ConsecutiveShiftRisk, $"consecutive:{facilityId}", "خطر مناوبات متتالية", SeverityMediumAr, 765, "مؤشر إرهاق: مناوبات متتالية دون راحة كافية.", "مراجعة الجداول", PermissionCodes.WorkforceViewCoverage));
        }

        if (indicators.Contains(Application.Workforce.WorkforceFatiguePolicy.QualificationExpiringSoon)
            && existing.All(i => i.Type != WorkforceOperationalCatalog.Interventions.QualificationExpiring))
        {
            yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.QualificationExpiring, $"qual-expiring:{facilityId}", "مؤهلات قاربت الانتهاء", SeverityMediumAr, 835, "توجد مؤهلات ضمن نافذة الانتهاء القريب.", "تجديد المؤهلات", PermissionCodes.WorkforceViewMembers));
        }
    }

    private static IEnumerable<FacilityPriorityItemPayload> BuildWorkforceQualityPriorityItems(
        WorkspaceContext context,
        Application.Workforce.WorkforceWorkspacePayload payload)
    {
        if (!payload.DataQuality.Issues.Any(i => i.Code is WorkforceOperationalCatalog.DataQuality.ConflictingAssignments
                or WorkforceOperationalCatalog.DataQuality.MemberOnLeaveButScheduled
                or WorkforceOperationalCatalog.DataQuality.ConflictingRosterAssignments))
        {
            yield break;
        }

        yield return WorkforcePriority(context, new WorkforcePrioritySpec(WorkforceOperationalCatalog.Interventions.ConflictingAssignments, $"conflict:{payload.Summary.FacilityId}", "تكليفات متعارضة", SeverityHighAr, 845, "جودة البيانات تشير إلى تعارضات تكليف/جدولة.", "فتح المصالحة", PermissionCodes.WorkforceReconcile));
    }

    private readonly record struct WorkforcePrioritySpec(
        string Type,
        string Reference,
        string TitleAr,
        string SeverityAr,
        int PriorityRank,
        string ReasonAr,
        string ActionLabelAr,
        string Permission);

    private static FacilityPriorityItemPayload WorkforcePriority(
        WorkspaceContext context,
        WorkforcePrioritySpec spec) =>
        new()
        {
            Type = spec.Type,
            Reference = spec.Reference,
            TitleAr = spec.TitleAr,
            SeverityAr = spec.SeverityAr,
            PriorityRank = spec.PriorityRank,
            ReasonAr = spec.ReasonAr,
            DueAtUtc = null,
            OverdueDays = null,
            OwnerAr = null,
            ActionLabelAr = spec.ActionLabelAr,
            DrillDownTarget = WorkforceTarget(context, spec.Permission)
        };

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

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentOccupancyEventsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var facilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context);
        var rows = await db.InmateCensusSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.FacilityId == facilityId && snapshot.CapturedAtUtc <= context.ToUtc)
            .OrderByDescending(snapshot => snapshot.CapturedAtUtc)
            .Take(5)
            .Select(snapshot => new
            {
                snapshot.Id,
                snapshot.FacilityUnitId,
                snapshot.InmateCount,
                snapshot.CapturedAtUtc,
                UnitName = snapshot.FacilityUnit != null ? snapshot.FacilityUnit.NameAr : null
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new FacilityActivityItemPayload
        {
            EventType = "occupancy.snapshot",
            TitleAr = row.FacilityUnitId.HasValue ? $"تحديث إشغال {row.UnitName}" : "تحديث إشغال السجن",
            DescriptionAr = $"Snapshot إحصائي بعدد {row.InmateCount} دون عرض هوية نزيل.",
            OccurredAtUtc = row.CapturedAtUtc,
            EntityReference = row.FacilityUnitId?.ToString() ?? facilityId.ToString(),
            Tone = "info",
            DrillDownTarget = OccupancyTarget(context, row.FacilityUnitId, PermissionCodes.OccupancyViewMovements)
        }).ToList();
    }

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentResourceEventsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var payload = await GetResourcesAsync(context, cancellationToken);
        return payload.Timeline
            .Take(5)
            .Select(item => new FacilityActivityItemPayload
            {
                EventType = item.EventType,
                TitleAr = item.TitleAr,
                DescriptionAr = item.DescriptionAr,
                OccurredAtUtc = item.OccurredAtUtc,
                EntityReference = item.EntityReference,
                Tone = item.Tone,
                DrillDownTarget = ResourceTarget(context, item.ResourceAssetId, PermissionCodes.ResourcesViewMaintenance)
            })
            .ToList();
    }

    private async Task<IReadOnlyList<FacilityActivityItemPayload>> BuildRecentWorkforceEventsAsync(
        WorkspaceContext context,
        CancellationToken cancellationToken)
    {
        var facilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context);
        var drill = WorkforceTarget(context, PermissionCodes.WorkforceViewCoverage);
        var events = new List<FacilityActivityItemPayload>();

        var rosters = await db.DutyRosters.AsNoTracking()
            .Where(roster => roster.FacilityId == facilityId && roster.PublishedAtUtc.HasValue)
            .OrderByDescending(roster => roster.PublishedAtUtc)
            .Take(5)
            .Select(roster => new { roster.Id, roster.DutyDate, roster.PublishedAtUtc })
            .ToListAsync(cancellationToken);
        events.AddRange(rosters.Select(row => new FacilityActivityItemPayload
        {
            EventType = "workforce.roster.published",
            TitleAr = $"نشر جدول مناوبة {row.DutyDate:yyyy-MM-dd}",
            DescriptionAr = "تحديث تغطية القوى البشرية بدون عرض أسماء الأعضاء.",
            OccurredAtUtc = row.PublishedAtUtc ?? context.ToUtc,
            EntityReference = row.Id.ToString(),
            Tone = "info",
            DrillDownTarget = drill
        }));

        var auditActions = new[]
        {
            "WorkforceMemberCreated",
            "WorkforceMemberUpdated",
            "WorkforceImportConfirmed",
            "DutyRosterPublished",
            "WorkforceAvailabilityRecorded"
        };
        var workforceAuditEntityIds = FacilityWorkforceAuditEntityIds(facilityId);
        var audits = await db.AuditLogs.AsNoTracking()
            .Where(log => log.Module == "Workforce"
                && auditActions.Contains(log.Action)
                && log.EntityId != null
                && workforceAuditEntityIds.Contains(log.EntityId))
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(20)
            .Select(log => new { log.Action, log.EntityId, log.OccurredAtUtc, log.UserDisplayName })
            .ToListAsync(cancellationToken);

        events.AddRange(audits.Select(log => new FacilityActivityItemPayload
        {
            EventType = $"workforce.audit.{log.Action}",
            TitleAr = log.Action switch
            {
                "WorkforceMemberCreated" => "إنشاء عضو قوى بشرية",
                "WorkforceMemberUpdated" => "تحديث عضو قوى بشرية",
                "WorkforceImportConfirmed" => "تأكيد استيراد قوى بشرية",
                "DutyRosterPublished" => "نشر جدول مناوبة",
                "WorkforceAvailabilityRecorded" => "تسجيل توفر تشغيلي",
                _ => "حدث قوى بشرية"
            },
            DescriptionAr = "سجل تشغيلي دون بيانات حساسة.",
            OccurredAtUtc = log.OccurredAtUtc,
            ActorDisplayName = log.UserDisplayName,
            EntityReference = log.EntityId ?? facilityId.ToString(),
            Tone = "info",
            DrillDownTarget = drill
        }));

        return events
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(20)
            .ToList();
    }

    private IQueryable<string> FacilityWorkforceAuditEntityIds(Guid facilityId)
    {
        var memberIds = db.WorkforceMembers.AsNoTracking()
            .Where(member => !member.IsDeleted
                && (member.CurrentOperationalFacilityId == facilityId || member.HomeFacilityId == facilityId))
            .Select(member => member.Id.ToString());
        var batchIds = db.WorkforceImportBatches.AsNoTracking()
            .Where(batch => batch.FacilityId == facilityId)
            .Select(batch => batch.Id.ToString());
        var rosterIds = db.DutyRosters.AsNoTracking()
            .Where(roster => roster.FacilityId == facilityId)
            .Select(roster => roster.Id.ToString());
        var availabilityIds = db.WorkforceAvailabilityEvents.AsNoTracking()
            .Where(evt => !evt.IsDeleted
                && (evt.WorkforceMember.CurrentOperationalFacilityId == facilityId
                    || evt.WorkforceMember.HomeFacilityId == facilityId))
            .Select(evt => evt.Id.ToString());

        return memberIds.Concat(batchIds).Concat(rosterIds).Concat(availabilityIds);
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

    private static FacilityDataQualityDomainPayload AvailableDomain(
        string key,
        string labelAr,
        int count,
        DateTimeOffset? lastUpdatedAtUtc,
        string impactAr) =>
        new()
        {
            Key = key,
            LabelAr = labelAr,
            StatusCode = count > 0 ? DataQualityComplete : DataQualityPartial,
            StatusAr = count > 0 ? "متاح" : "جزئي",
            ConfidenceAr = count > 0 ? "مرتفعة" : "متوسطة",
            LastUpdatedAtUtc = lastUpdatedAtUtc,
            ImpactAr = count > 0 ? impactAr : $"{impactAr} لا توجد سجلات حالية ضمن الفترة أو النطاق.",
            FollowUpIssue = null
        };

    private static FacilityDataQualityDomainPayload MissingDomain(
        string key,
        string labelAr,
        string impactAr,
        string followUpIssue) =>
        new()
        {
            Key = key,
            LabelAr = labelAr,
            StatusCode = "unavailable",
            StatusAr = "غير متاح",
            ConfidenceAr = "غير معروفة",
            LastUpdatedAtUtc = null,
            ImpactAr = impactAr,
            FollowUpIssue = followUpIssue
        };

    private static FacilityDataQualityDomainPayload OccupancyDomain(OccupancyWorkspacePayload payload) =>
        new()
        {
            Key = "occupancy",
            LabelAr = "الإشغال والنزلاء",
            StatusCode = payload.Summary.IsPartial ? DataQualityPartial : DataQualityComplete,
            StatusAr = payload.Summary.IsPartial ? "جزئي" : "متاح",
            ConfidenceAr = FacilityWorkspaceConfidenceMapper.ToArabic(payload.Summary.ConfidenceLevel),
            LastUpdatedAtUtc = payload.Summary.LatestSnapshotAtUtc,
            ImpactAr = payload.Summary.IsPartial
                ? string.Join(" ", payload.Summary.Warnings)
                : "يدخل في الحالة العامة والعمل العاجل وقسم الإشغال.",
            FollowUpIssue = null
        };

    private static FacilityDataQualityDomainPayload ResourcesDomain(ResourceWorkspacePayload payload)
    {
        var (statusCode, statusAr) = ResolveResourcesDataQualityStatus(payload.Summary);
        return new()
        {
            Key = "resources",
            LabelAr = "الموارد والجاهزية",
            StatusCode = statusCode,
            StatusAr = statusAr,
            ConfidenceAr = FacilityWorkspaceConfidenceMapper.ToArabic(payload.Summary.ConfidenceLevel),
            LastUpdatedAtUtc = payload.Summary.DataEffectiveAtUtc,
            ImpactAr = payload.Summary.Warnings.Count > 0
                ? string.Join(" ", payload.Summary.Warnings)
                : "يدخل في الحالة العامة والعمل العاجل وقسم الموارد.",
            FollowUpIssue = null
        };
    }

    private static (string StatusCode, string StatusAr) ResolveResourcesDataQualityStatus(ResourceSummaryDto summary)
    {
        if (summary.TotalRegistered == 0)
        {
            return ("missing", "مفقود");
        }

        if (summary.IsPartial)
        {
            return (DataQualityPartial, "جزئي");
        }

        return (DataQualityComplete, "متاح");
    }

    private static FacilityDataQualityDomainPayload WorkforceDomain(WorkforceWorkspacePayload payload)
    {
        var (statusCode, statusAr) = ResolveWorkforceDataQualityStatus(payload.Summary);
        return new()
        {
            Key = DomainKeyWorkforce,
            LabelAr = "القوى البشرية والتغطية",
            StatusCode = statusCode,
            StatusAr = statusAr,
            ConfidenceAr = FacilityWorkspaceConfidenceMapper.ToArabic(payload.Summary.ConfidenceLevel),
            LastUpdatedAtUtc = payload.Summary.DataEffectiveAtUtc,
            ImpactAr = payload.Summary.Warnings.Count > 0
                ? string.Join(" ", payload.Summary.Warnings)
                : "يدخل في الحالة العامة والعمل العاجل وقسم القوى البشرية.",
            FollowUpIssue = null
        };
    }

    private static (string StatusCode, string StatusAr) ResolveWorkforceDataQualityStatus(WorkforceSummaryDto summary)
    {
        if (summary.TotalMembers == 0)
        {
            return ("missing", "مفقود");
        }

        if (summary.IsPartial)
        {
            return (DataQualityPartial, "جزئي");
        }

        return (DataQualityComplete, "متاح");
    }

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
            new Dictionary<string, string> { [FacilityWorkspaceDrillDownFilters.FacilityIdParameterName] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString() },
            FacilityWorkspaceDrillDownFilters.Preserve(context),
            PermissionCodes.FormsViewComplianceDashboard);

    private static DrillDownTarget OccupancyTarget(WorkspaceContext context, Guid? unitId, string requiredPermission) =>
        new(
            "facility.occupancy",
            unitId.HasValue ? "فتح وحدة الإشغال" : "فتح الإشغال",
            unitId.HasValue
                ? new Dictionary<string, string>
                {
                    [FacilityWorkspaceDrillDownFilters.FacilityIdParameterName] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString(),
                    ["unitId"] = unitId.Value.ToString()
                }
                : new Dictionary<string, string> { [FacilityWorkspaceDrillDownFilters.FacilityIdParameterName] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString() },
            FacilityWorkspaceDrillDownFilters.Preserve(context),
            requiredPermission);

    private static DrillDownTarget ResourceTarget(WorkspaceContext context, Guid? assetId, string requiredPermission) =>
        new(
            "facility.resources",
            assetId.HasValue ? "فتح المورد" : "فتح الموارد",
            assetId.HasValue
                ? new Dictionary<string, string>
                {
                    [FacilityWorkspaceDrillDownFilters.FacilityIdParameterName] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString(),
                    ["assetId"] = assetId.Value.ToString()
                }
                : new Dictionary<string, string> { [FacilityWorkspaceDrillDownFilters.FacilityIdParameterName] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString() },
            FacilityWorkspaceDrillDownFilters.Preserve(context),
            requiredPermission);

    private static DrillDownTarget WorkforceTarget(WorkspaceContext context, string requiredPermission) =>
        new(
            "facility.workforce",
            "فتح القوى البشرية",
            new Dictionary<string, string> { [FacilityWorkspaceDrillDownFilters.FacilityIdParameterName] = FacilityWorkspaceContextGuard.RequireFacilityId(context).ToString() },
            FacilityWorkspaceDrillDownFilters.Preserve(context),
            requiredPermission);

}

internal static class FacilityWorkspaceConfidenceMapper
{
    public static ConfidenceLevel ToLevel(string? code) =>
        code switch
        {
            "high" => ConfidenceLevel.High,
            "medium" => ConfidenceLevel.Medium,
            "low" => ConfidenceLevel.Low,
            _ => ConfidenceLevel.Unknown
        };

    public static string ToArabic(string? code) =>
        code switch
        {
            "high" => "مرتفعة",
            "medium" => "متوسطة",
            "low" => "منخفضة",
            _ => "غير معروفة"
        };
}

internal static class FacilityWorkspaceDrillDownFilters
{
    internal const string FacilityIdParameterName = "facilityId";

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
            filters[FacilityIdParameterName] = context.FacilityId.Value.ToString();
        }

        return filters;
    }
}
